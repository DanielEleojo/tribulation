using UnityEngine;
using UnityEngine.Rendering;

// Procedural Korean palace-gate (일주문) silhouette — a distant fogged landmark
// the player runs toward. Built entirely from primitive cubes in Start(); the single
// root is RECYCLED (repositioned) so the gate reappears periodically as a rare
// landmark. No imported assets, no per-frame allocation, no colliders.
// Forward = -Z. Player runs toward -Z. Gate is centred on x=0, player passes through.
public class GateLandmark : MonoBehaviour
{
    // --- Geometry constants --------------------------------------------------
    const float PILLAR_X   = 5f;   // pillar x offset (outside track half-width 3.75)
    const float GATE_H     = 6f;   // pillar height

    // --- Placement / recycle constants ---------------------------------------
    const float GATE_AHEAD    = 130f;  // initial z ahead of player (player.z - GATE_AHEAD)
    const float GATE_GAP      = 220f;  // distance between successive appearances
    const float RECYCLE_BEHIND = 25f;  // recycle when gate.z > player.z + this

    // --- Light-shaft constants (Part B — focal beacon effect) ----------------
    // A fake-volumetric beam + halo centred in the gate opening.
    // Values tuned so the beam reads as a focused shaft of light in the doorway,
    // NOT a glowing slab that drowns the gate silhouette.
    static readonly Color SHAFT_COLOR      = new Color(1.00f, 0.92f, 0.70f, 0.28f); // warm gold-white beam — dim enough that gate architecture dominates
    static readonly Color HALO_COLOR       = new Color(1.00f, 0.88f, 0.60f, 0.22f); // softer halo — just a glow in the opening, not a wall
    const float SHAFT_WIDTH    = 1.6f;   // narrow — reads as shaft between pillars, not a covering slab
    const float SHAFT_HEIGHT   = 10f;    // confined to the doorway opening — doesn't extend above the lintel
    // Vertical centre of the beam: put it in the opening (between ground and lintel)
    // localPos.y = GATE_H * SHAFT_Y_FACTOR — lintel is at GATE_H*0.88, so 0.38 centres the beam in the opening
    const float SHAFT_Y_FACTOR = 0.38f;  // centred in the doorway opening (below lintel)
    const float SHAFT_Z_OFFSET = 0.5f;   // slight +Z push so it sits in front of the pillars
    const float HALO_SIZE      = 3.5f;   // small — pool of warm light on the ground/opening, not above the roof
    // Halo sits at the opening centre (roughly mid-pillar height)
    const float HALO_Y_FACTOR  = 0.35f;

    // --- Runtime state -------------------------------------------------------
    Transform _player;
    Transform _gateRoot;

    // -------------------------------------------------------------------------
    void Start()
    {
        // Build the silhouette material once; share it across all pieces.
        var mat = BuildSilhouetteMat();

        // Root container — reposition this single object for recycling.
        var rootGO = new GameObject("GateRoot");
        _gateRoot = rootGO.transform;
        _gateRoot.SetParent(transform, false);

        // ---- Pillars (two vertical columns, player runs between them) --------
        // Left pillar
        AddPiece(_gateRoot, "PillarL",
            localPos:   new Vector3(-PILLAR_X, GATE_H * 0.5f, 0f),
            localScale: new Vector3(0.6f, GATE_H, 0.6f),
            mat: mat);

        // Right pillar
        AddPiece(_gateRoot, "PillarR",
            localPos:   new Vector3( PILLAR_X, GATE_H * 0.5f, 0f),
            localScale: new Vector3(0.6f, GATE_H, 0.6f),
            mat: mat);

        // ---- Lintel beam (horizontal top span) ------------------------------
        // Spans pillar centres + small overhang; near top of pillars.
        AddPiece(_gateRoot, "Lintel",
            localPos:   new Vector3(0f, GATE_H - 0.7f, 0f),
            localScale: new Vector3(PILLAR_X * 2f + 1.4f, 0.6f, 0.8f),
            mat: mat);

        // ---- Tie beam / changbang (lower horizontal band) -------------------
        AddPiece(_gateRoot, "TieBeam",
            localPos:   new Vector3(0f, GATE_H * 0.62f, 0f),
            localScale: new Vector3(PILLAR_X * 2f + 0.4f, 0.35f, 0.6f),
            mat: mat);

        // ---- Roof / eave slab (wide, overhangs pillars — tiled-roof read) ---
        AddPiece(_gateRoot, "RoofEave",
            localPos:   new Vector3(0f, GATE_H + 0.2f, 0f),
            localScale: new Vector3(PILLAR_X * 2f + 3.5f, 0.5f, 2.4f),
            mat: mat);

        // ---- Ridge slab on top (second tier — two-tier roof silhouette) -----
        AddPiece(_gateRoot, "RoofRidge",
            localPos:   new Vector3(0f, GATE_H + 0.7f, 0f),
            localScale: new Vector3(PILLAR_X * 2f + 1.5f, 0.4f, 1.4f),
            mat: mat);

        // Build the light shaft — a fake-volumetric beacon in the gate opening.
        // Must be called after _gateRoot exists so the quads parent under it and
        // recycle for free when _gateRoot is repositioned in Update.
        BuildShaft(_gateRoot);

        // Place well ahead of the player to start.
        // Player starts near z=0; gate at z = 0 - GATE_AHEAD (in the -Z direction).
        _gateRoot.position = new Vector3(0f, 0f, -GATE_AHEAD);
    }

    void Update()
    {
        // Lazy-init player ref — mirrors Scenery.cs / Ground.cs pattern exactly.
        if (_player == null)
        {
            var p = FindObjectOfType<PlayerRunner>();
            if (p == null) return;
            _player = p.transform;
        }

        // Recycle: if the gate has fallen behind the player, move it far ahead again.
        if (_gateRoot.position.z > _player.position.z + RECYCLE_BEHIND)
        {
            Vector3 pos = _gateRoot.position;
            pos.z -= GATE_GAP;   // jump forward (more negative z) by one gap
            _gateRoot.position = pos;
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    // Create one primitive cube piece, strip its BoxCollider, apply the shared mat.
    static void AddPiece(Transform parent, string name, Vector3 localPos, Vector3 localScale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale    = localScale;

        // Strip the BoxCollider — purely cosmetic, no gameplay impact.
        var col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);

        // Apply the shared dark silhouette material.
        var rend = go.GetComponent<Renderer>();
        if (rend != null) rend.sharedMaterial = mat;
    }

    // Build a dark URP/Lit material tinted to match the fogged silhouette palette.
    // Falls back to Standard if URP shader is absent; builds untinted if both are null
    // (rather than crashing) — matches Scenery.cs / Ground.cs null-guard pattern.
    static Material BuildSilhouetteMat()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null)
        {
            Debug.LogWarning("[GateLandmark] Could not find URP/Lit or Standard shader — gate will be unlit.");
            return new Material(Shader.Find("Hidden/InternalErrorShader") ?? Shader.Find("Unlit/Color"));
        }

        var mat = new Material(shader);
        // Slightly warmer tint than pure fog-grey so the edges read as a deliberate landmark.
        // Still very dark and moody — just enough to separate from the background at 100m+.
        var tint = new Color(0.22f, 0.19f, 0.24f, 1f);   // dark warm-purple, a touch lighter/warmer than pure fog
        mat.color = tint;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
        // Faint warm emission so the gate silhouette reads against the dusky sky at distance.
        // Kept very low (0.06) — barely visible but enough to trace the architecture outline.
        mat.EnableKeyword("_EMISSION");
        var emitColor = new Color(0.06f, 0.04f, 0.08f);  // dim warm-purple glow
        mat.SetColor("_EmissionColor", emitColor);
        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emitColor);
        return mat;
    }

    // -------------------------------------------------------------------------
    // Light shaft — fake volumetric beacon (Part B)
    // -------------------------------------------------------------------------

    // Procedurally build the light-shaft effect as two crossed Quad primitives (beam)
    // plus one round halo Quad, all parented under gateRoot so they recycle automatically.
    // No colliders, no per-frame allocation — just three MeshRenderer quads.
    static void BuildShaft(Transform gateRoot)
    {
        var glowTex = InkArt.SoftGlow(64).texture;
        if (glowTex == null)
        {
            Debug.LogWarning("[GateLandmark] SoftGlow texture unavailable — skipping light shaft.");
            return;
        }

        var shaftMat = BuildAdditiveMat(glowTex, SHAFT_COLOR);
        var haloMat  = BuildAdditiveMat(glowTex, HALO_COLOR);

        // Centre position of the beam within the gate (local to gateRoot).
        float beamY = GATE_H * SHAFT_Y_FACTOR;
        var   beamPos = new Vector3(0f, beamY, SHAFT_Z_OFFSET);
        var   beamScale = new Vector3(SHAFT_WIDTH, SHAFT_HEIGHT, 1f);

        // Quad A — faces +Z (toward the approaching player).
        // A Unity Quad's lit/textured face looks toward -Z by default; rotate 180° on Y
        // so it looks toward +Z (toward the player coming from that direction).
        AddShaftQuad(gateRoot, "ShaftBeamA", beamPos, beamScale,
            rotation: Quaternion.Euler(0f, 180f, 0f), mat: shaftMat);

        // Quad B — rotated 90° on Y relative to A, forming an X cross so the beam reads
        // from slight side-angles too (common as the player drifts left/right on track).
        AddShaftQuad(gateRoot, "ShaftBeamB", beamPos, beamScale,
            rotation: Quaternion.Euler(0f, 90f, 0f), mat: shaftMat);

        // Round glow halo centred in the opening — catches URP bloom for a luminous read.
        float haloY = GATE_H * HALO_Y_FACTOR;
        AddShaftQuad(gateRoot, "ShaftHalo",
            localPos:  new Vector3(0f, haloY, SHAFT_Z_OFFSET),
            localScale: new Vector3(HALO_SIZE, HALO_SIZE, 1f),
            rotation:  Quaternion.Euler(0f, 180f, 0f), mat: haloMat);
    }

    // Create one additive-transparent Quad, strip its MeshCollider, parent under gateRoot.
    static void AddShaftQuad(Transform parent, string name,
        Vector3 localPos, Vector3 localScale, Quaternion rotation, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale    = localScale;
        go.transform.localRotation = rotation;

        // Strip the MeshCollider that CreatePrimitive adds — purely visual.
        var col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);

        var rend = go.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.sharedMaterial    = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows    = false;
        }
    }

    // Additive transparent unlit material — mirrors Atmosphere.cs ParticleMat(additive:true)
    // exactly, adapted for MeshRenderer quads (same shader, same blend state).
    // Null-guard / fallback matches BuildSilhouetteMat pattern used above.
    static Material BuildAdditiveMat(Texture2D tex, Color tint)
    {
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                  ?? Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Debug.LogWarning("[GateLandmark] Could not find additive shader — light shaft will be invisible.");
            shader = Shader.Find("Hidden/InternalErrorShader");
            if (shader == null) return new Material(Shader.Find("Unlit/Color"));
        }

        var m = new Material(shader);
        m.SetTexture("_BaseMap", tex);
        m.mainTexture = tex;
        m.color = tint;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_ZWrite", 0f);
        m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)BlendMode.One);   // additive
        m.renderQueue = (int)RenderQueue.Transparent;
        return m;
    }
}
