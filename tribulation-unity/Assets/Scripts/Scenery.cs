using System.Collections.Generic;
using UnityEngine;

// Drifting tree/rock silhouettes on both sides of the track for depth.
// Fixed pool, recycled forward when behind the player — mirrors Ground.cs exactly.
// Forward = -Z. Player starts near z=0, track extends toward -Z.
public class Scenery : MonoBehaviour
{
    const int   ROWS          = 14;
    const float ROW_GAP       = 12f;
    const float RECYCLE_BEHIND = 20f;
    const float SIDE_MIN      = 6f;   // minimum |x| offset from centre (track edge ≈ 3.75)
    const float SIDE_SPREAD   = 8f;   // random additional spread (so trees sit ~6..14 out)

    // One row = two GameObjects (left + right) sharing the same z-anchor.
    struct SceneryRow
    {
        public Transform left;
        public Transform right;
        public float z;           // world z of this row
    }

    Transform _player;
    GameObject[] _prefabs;        // loaded from Resources (may be smaller than 4 if some fail)
    readonly List<SceneryRow> _rows = new List<SceneryRow>();

    void Start()
    {
        // --- Load prefabs ---------------------------------------------------
        string[] paths = { "Scenery/tree01", "Scenery/tree02", "Scenery/tree03", "Scenery/bush03" };
        var loaded = new List<GameObject>();
        foreach (string p in paths)
        {
            var go = Resources.Load<GameObject>(p);
            if (go != null)
                loaded.Add(go);
            else
                Debug.LogWarning("[Scenery] Could not load Resources/" + p + " — skipping.");
        }
        _prefabs = loaded.ToArray();

        if (_prefabs.Length == 0)
        {
            Debug.LogWarning("[Scenery] No scenery prefabs loaded — component will no-op.");
            enabled = false;   // graceful no-op
            return;
        }

        // --- Build pool -----------------------------------------------------
        // Rows extend ahead of the player (toward -Z), matching Ground.cs layout.
        for (int i = 0; i < ROWS; i++)
        {
            float rowZ = -i * ROW_GAP;
            SceneryRow row;
            row.z     = rowZ;
            row.left  = SpawnObject(new Vector3(-SideX(), 0f, rowZ));
            row.right = SpawnObject(new Vector3( SideX(), 0f, rowZ));
            _rows.Add(row);
        }
    }

    void Update()
    {
        // Lazy-init player ref — mirrors Ground.cs Update() pattern exactly.
        if (_player == null)
        {
            var p = FindObjectOfType<PlayerRunner>();
            if (p == null) return;
            _player = p.transform;
        }

        float behindZ = _player.position.z + RECYCLE_BEHIND;

        for (int i = 0; i < _rows.Count; i++)
        {
            SceneryRow row = _rows[i];
            if (row.z > behindZ)
            {
                float newZ = FrontmostZ() - ROW_GAP;
                row.z = newZ;

                // Move both objects to the new row z, re-randomise x for variety.
                SetRowZ(row.left,  newZ, -SideX());
                SetRowZ(row.right, newZ,  SideX());

                _rows[i] = row;   // struct — write back
            }
        }
    }

    // Returns the frontmost (most negative) row z — mirrors FrontmostZ() in Ground.cs.
    float FrontmostZ()
    {
        float m = Mathf.Infinity;
        foreach (var row in _rows) m = Mathf.Min(m, row.z);
        return m;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    // Random x magnitude for one side (6..14).
    float SideX() => SIDE_MIN + Random.value * SIDE_SPREAD;

    // Instantiate one prefab, strip colliders, darken, return its Transform.
    Transform SpawnObject(Vector3 worldPos)
    {
        var prefab = _prefabs[Random.Range(0, _prefabs.Length)];
        var go = Instantiate(prefab, worldPos, Quaternion.identity, transform);

        // Random Y-rotation and uniform scale 0.8..1.6.
        go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        float s = Random.Range(0.8f, 1.6f);
        go.transform.localScale = new Vector3(s, s, s);

        // Strip all colliders — purely cosmetic.
        foreach (var col in go.GetComponentsInChildren<Collider>(true))
            Destroy(col);

        // Darken to silhouette tint via MaterialPropertyBlock (no material leaks).
        DarkenRenderers(go);

        return go.transform;
    }

    // Move an existing object to a new z with a new random x offset.
    void SetRowZ(Transform t, float newZ, float newX)
    {
        if (t == null) return;
        Vector3 p = t.position;
        p.x = newX;
        p.z = newZ;
        t.position = p;

        // Re-randomise visual variety on recycle.
        t.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        float s = Random.Range(0.8f, 1.6f);
        t.localScale = new Vector3(s, s, s);
    }

    // Replace every renderer's materials with a dark URP alpha-clipped silhouette material.
    // We can't just tint via MaterialPropertyBlock: the NatureStarterKit2 trees use legacy
    // Tree-Creator leaf shaders (Hidden/Nature/...) that render MAGENTA in URP. So we rebuild
    // each material as URP/Lit, keep the original cutout texture (so leaf cards stay leaf-shaped),
    // and tint it dark for a fogged-silhouette read. ~2 mats × 28 instances = cheap on mobile.
    static readonly Color SilhouetteTint = new Color(0.09f, 0.09f, 0.13f, 1f); // darker + cooler ink silhouette

    static void DarkenRenderers(GameObject root)
    {
        var urp = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (urp == null) return;

        foreach (var rend in root.GetComponentsInChildren<Renderer>(true))
        {
            if (rend == null) continue;
            var src = rend.sharedMaterials;
            var dst = new Material[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                var m = new Material(urp);
                // Preserve the original main texture (leaf atlas carries the alpha cutout).
                Texture tex = null;
                if (src[i] != null)
                {
                    if (src[i].HasProperty("_MainTex")) tex = src[i].GetTexture("_MainTex");
                    if (tex == null && src[i].HasProperty("_BaseMap")) tex = src[i].GetTexture("_BaseMap");
                }
                if (tex != null) { m.SetTexture("_BaseMap", tex); m.SetTexture("_MainTex", tex); }
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", SilhouetteTint);
                m.color = SilhouetteTint;
                // Alpha-clip so leaf cards keep their shape instead of rendering as solid blobs.
                m.SetFloat("_AlphaClip", 1f);
                m.EnableKeyword("_ALPHATEST_ON");
                if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.4f);
                dst[i] = m;
            }
            rend.sharedMaterials = dst;
        }
    }
}
