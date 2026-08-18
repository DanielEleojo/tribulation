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

    // One loadable scenery piece. targetHeight > 0 = normalize the instance to that
    // world height (the Godot prop glbs are authored at arbitrary scales; heights come
    // from scenery.gd TARGET_H). natural = keep the glb's own materials instead of the
    // ink-silhouette repaint (which exists for the NatureStarterKit legacy prefabs).
    struct PropEntry
    {
        public GameObject prefab;
        public float targetHeight;
        public bool natural;
    }

    Transform _player;
    PropEntry[] _prefabs;
    readonly List<SceneryRow> _rows = new List<SceneryRow>();
    readonly Dictionary<Transform, float> _baseScale = new Dictionary<Transform, float>(); // per-instance height-normalize scale (recycles re-randomize AROUND this)

    void Start()
    {
        // --- Load prefabs ---------------------------------------------------
        // Primary: the Godot-era prop glbs (Resources/Props, tracked in git) — the
        // roadside pines/trees/rocks of the original game.
        var loaded = new List<PropEntry>();
        (string path, float h)[] props = { ("Props/pine", 7.5f), ("Props/tree", 6.0f), ("Props/rock", 1.8f) };
        foreach (var (path, h) in props)
        {
            var go = Resources.Load<GameObject>(path);
            if (go != null)
                loaded.Add(new PropEntry { prefab = go, targetHeight = h, natural = true });
            else
                Debug.LogWarning("[Scenery] Could not load Resources/" + path + " — skipping.");
        }

        // Legacy fallback: NatureStarterKit2 prefabs (usable only with that Asset
        // Store pack installed) — silhouette-tinted like before.
        if (loaded.Count == 0)
        {
            string[] paths = { "Scenery/tree01", "Scenery/tree02", "Scenery/tree03", "Scenery/bush03" };
            foreach (string p in paths)
            {
                var go = Resources.Load<GameObject>(p);
                if (go != null)
                    loaded.Add(new PropEntry { prefab = go, targetHeight = 0f, natural = false });
            }
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

    // Instantiate one prefab, strip colliders, normalize height, return its Transform.
    Transform SpawnObject(Vector3 worldPos)
    {
        var entry = _prefabs[Random.Range(0, _prefabs.Length)];
        var go = Instantiate(entry.prefab, worldPos, Quaternion.identity, transform);

        // Strip all colliders — purely cosmetic.
        foreach (var col in go.GetComponentsInChildren<Collider>(true))
            Destroy(col);

        // Height-normalize prop glbs to their scenery.gd target heights.
        float baseScale = 1f;
        if (entry.targetHeight > 0f)
        {
            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                if (b.size.y > 0.01f) baseScale = entry.targetHeight / b.size.y;
            }
        }
        _baseScale[go.transform] = baseScale;

        // Random Y-rotation and uniform variety 0.8..1.6 around the normalized base.
        go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        float s = baseScale * Random.Range(0.8f, 1.6f);
        go.transform.localScale = new Vector3(s, s, s);

        // Legacy NatureStarterKit prefabs get the ink-silhouette repaint (their
        // Tree-Creator shaders render magenta in URP); prop glbs keep their own look.
        if (!entry.natural) DarkenRenderers(go);

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

        // Re-randomise visual variety on recycle — around the height-normalized base.
        float baseScale;
        if (!_baseScale.TryGetValue(t, out baseScale)) baseScale = 1f;
        t.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        float s = baseScale * Random.Range(0.8f, 1.6f);
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
