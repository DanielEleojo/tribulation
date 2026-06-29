using System.Collections.Generic;
using UnityEngine;

// Port of ground.gd. Infinite runway by RECYCLING tiles along Z (forward = -Z).
// Each tile is a 3-lane stone path (top at y=0) with glowing lane-divider lines.
// Tiles are built from primitives in code so there's nothing to wire in the editor.
// (Tracer-bullet simplification of the Godot version's shoulders/rungs/edge frame.)
public class Ground : MonoBehaviour
{
    const float TILE_LENGTH = 20f;
    const int TILE_COUNT = 10;
    const float RECYCLE_BEHIND = 25f;
    const float LANE_WIDTH = 2.5f;
    const float PATH_WIDTH = LANE_WIDTH * 3f;

    Transform _player;
    readonly List<Transform> _tiles = new List<Transform>();
    Material _pathMat, _lineMat;

    Transform _bed;

    void Start()
    {
        _pathMat = StoneMat();
        _lineMat = SolidMat(new Color(0.55f, 0.60f, 0.40f), true);
        for (int i = 0; i < TILE_COUNT; i++)
        {
            Transform t = MakeTile();
            t.position = new Vector3(0f, 0f, -i * TILE_LENGTH);
            _tiles.Add(t);
        }

        // Dark ground bed so the road no longer floats in a void — fog swallows its edges.
        var bed = GameObject.CreatePrimitive(PrimitiveType.Plane);
        bed.name = "GroundBed";
        Destroy(bed.GetComponent<Collider>());
        bed.transform.SetParent(transform, false);
        bed.transform.localScale = new Vector3(40f, 1f, 80f); // Plane is 10u → 400×800
        bed.transform.localPosition = new Vector3(0f, -0.06f, 0f);
        var bedMat = SolidMat(new Color(0.03f, 0.03f, 0.045f), false);
        bedMat.SetFloat("_Smoothness", 0.05f); // matte dark earth, no sun streak
        bed.GetComponent<Renderer>().sharedMaterial = bedMat;
        _bed = bed.transform;
    }

    Transform MakeTile()
    {
        var tile = new GameObject("Tile").transform;
        tile.SetParent(transform, false);

        // Stone runway (top at y=0): a 1-thick slab centered at y=-0.5.
        var path = GameObject.CreatePrimitive(PrimitiveType.Cube);
        path.name = "Path";
        path.transform.SetParent(tile, false);
        path.transform.localPosition = new Vector3(0f, -0.5f, 0f);
        path.transform.localScale = new Vector3(PATH_WIDTH, 1f, TILE_LENGTH);
        var pr = path.GetComponent<Renderer>();
        pr.sharedMaterial = _pathMat;
        // Tile the stone ~2.5m per repeat (cube UVs span one face): width 7.5→3 reps, length 20→8 reps.
        pr.sharedMaterial.mainTextureScale = new Vector2(3f, 8f);

        // Two glowing lane dividers (no collider needed — strip them).
        foreach (float sx in new[] { -LANE_WIDTH * 0.5f, LANE_WIDTH * 0.5f })
        {
            var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.transform.SetParent(tile, false);
            line.transform.localPosition = new Vector3(sx, 0.01f, 0f);
            line.transform.localScale = new Vector3(0.1f, 0.02f, TILE_LENGTH);
            line.GetComponent<Renderer>().sharedMaterial = _lineMat;
            Destroy(line.GetComponent<Collider>());
        }
        return tile;
    }

    void Update()
    {
        if (_player == null) { var p = FindObjectOfType<PlayerRunner>(); if (p == null) return; _player = p.transform; }
        if (_bed != null) _bed.position = new Vector3(0f, -0.06f, _player.position.z);
        float behindZ = _player.position.z + RECYCLE_BEHIND;
        foreach (Transform t in _tiles)
            if (t.position.z > behindZ)
            {
                Vector3 pos = t.position;
                pos.z = FrontmostZ() - TILE_LENGTH;
                t.position = pos;
            }
    }

    float FrontmostZ()
    {
        float m = Mathf.Infinity;
        foreach (Transform t in _tiles) m = Mathf.Min(m, t.position.z);
        return m;
    }

    // Textured dark-stone path with a faint wet sheen so sun + hazard bloom catch on it.
    static Material StoneMat()
    {
        var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(sh);
        var alb = Resources.Load<Texture2D>("road/road_albedo");
        var nrm = Resources.Load<Texture2D>("road/road_normal");
        if (alb != null) { m.mainTexture = alb; alb.wrapMode = TextureWrapMode.Repeat; }
        if (nrm != null)
        {
            m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_BumpMap", nrm);
            m.SetTextureScale("_BumpMap", new Vector2(3f, 8f));
            nrm.wrapMode = TextureWrapMode.Repeat;
        }
        m.SetColor("_BaseColor", new Color(0.55f, 0.55f, 0.62f)); // dim cool tint, keeps the dusk mood
        m.SetFloat("_Smoothness", 0.45f);                          // wet sheen
        m.SetFloat("_Metallic", 0f);
        return m;
    }

    // URP/Lit material helper. note: Shader.Find at runtime instead of a serialized asset.
    static Material SolidMat(Color c, bool emissive)
    {
        var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(sh) { color = c };
        if (emissive) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * 1.6f); }
        return m;
    }
}
