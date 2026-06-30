// InkCultivator.cs — fully procedural cloaked demonic-cultivator for Tribulation.
// Attach to the player root (Bootstrap does this). Builds a "InkFigure" child rig
// from Unity primitives + code — NO imported assets. Reads PlayerRunner state hooks
// (Grounded, IsSliding, Lane, Vy, Slashed) and drives pose each frame.
//
// Visual theme: near-black ink robe with jade trim, cinnabar qi core (fake-glow via
// unlit material + additive halo quad), pale face peeking from a dark cowl/hood.
//
// REDESIGN NOTES (v2)
//   • Robe body: Cylinder for tapering skirt hem + angled shoulder block on top.
//   • Hood/cowl: dark sphere wrapping head, pale face-sliver inside.
//   • Two arms with jade cuff trim.
//   • Jade collar trim across shoulders + jade hem trim ring.
//   • Qi core: Unlit bright cinnabar sphere + additive billboard halo quad for
//     fake glow that reads without bloom post-processing.
//
// COMPILE-SAFE NOTES
//   • Shader.Find chain: URP/Lit → Standard for lit mats; Sprites/Default → URP/Unlit
//     → Unlit/Color for unlit/trail mats; never null-used unchecked.
//   • Every CreatePrimitive collider is Destroyed; every GetComponent null-guarded.
//   • No member name collides with PlayerRunner or InkArt.
//   • _inkMat / _coreMat / _coreUnlitMat / _haloMat / _jadeMat / _headMat /
//     _cowlMat / _trailMat / _arcMat — all local to this class.
//   • TrailRenderer API: time, startWidth, endWidth, startColor, endColor, material,
//     alignment, autodestruct — valid in Unity 6 / URP 17.
//   • LineRenderer for arc: positionCount, SetPosition, startColor, endColor,
//     startWidth, endWidth, useWorldSpace, material — valid.
//   • Quad primitive: PrimitiveType.Quad — valid, yields a Quad mesh with MeshRenderer.
//   • Material.mainColor is the _Color property alias — we use .color which maps to _BaseColor
//     on URP/Lit and _Color on Standard. For Unlit we set _Color explicitly.

using UnityEngine;
using UnityEngine.Rendering; // BlendMode

[RequireComponent(typeof(PlayerRunner))]
public class InkCultivator : MonoBehaviour, IFeelPose
{
    // ── Palette ───────────────────────────────────────────────────────────────
    static readonly Color ColInkRobe  = new Color(0.07f, 0.05f, 0.08f, 1f); // near-black
    static readonly Color ColHead     = new Color(0.82f, 0.76f, 0.70f, 1f); // pale
    static readonly Color ColCinnabar = new Color(0.75f, 0.22f, 0.17f, 1f); // #c0392b
    static readonly Color ColJade     = new Color(0.16f, 0.49f, 0.44f, 1f); // #2a7c6f
    static readonly Color ColTrail    = new Color(0.10f, 0.05f, 0.12f, 0.9f);
    static readonly Color ColCowl     = new Color(0.06f, 0.04f, 0.07f, 1f); // slightly darker than robe

    // ── Rig references ────────────────────────────────────────────────────────
    Transform _figure;       // "InkFigure" root — pose everything via this
    Transform _armHint;      // slash arc origin (right arm)
    Transform _qiCore;       // unlit core sphere (pulsed)

    // ── Qi core glow references ───────────────────────────────────────────────
    Transform _qiHalo;       // additive billboard quad behind/over the core
    Material  _coreUnlitMat; // unlit mat on the core sphere — we pulse its color
    Material  _haloMat;      // additive mat on the halo quad — we pulse its alpha

    // ── Slash arc ─────────────────────────────────────────────────────────────
    LineRenderer _arcLine;   // reused arc object
    float        _arcTimer;  // countdown; >0 → arc visible
    const float  ARC_DURATION = 0.25f;

    // ── Pose smoothing ────────────────────────────────────────────────────────
    Vector3 _baseScale = Vector3.one; // SmoothDamp'd pose scale, before the pop multiply
    Vector3 _figureScaleVel;   // SmoothDamp vel for scale
    Vector3 _figurePosVel;     // SmoothDamp vel for position
    float   _figureRotVel;     // SmoothDamp vel for roll (lane-change)
    float   _figureYawVel;     // SmoothDamp vel for lean (forward pitch)
    float   _currentRoll;      // current Z-roll angle (lane banking)
    float   _currentPitch;     // current X-pitch angle (forward lean)

    // ── Lane tracking for bank ────────────────────────────────────────────────
    int _prevLane = 1;
    int _bankDir;             // +1 tilt left, -1 tilt right (captured at lane-change moment)
    float _bankTimer;         // how long we've been changing lane
    const float BANK_HOLD  = 0.18f; // seconds of peak bank during lane move
    const float BANK_ANGLE = 18f;   // peak roll degrees

    // ── Feel Pass v1: transient scale pop + landing impact ────────────────────
    float _pop;                       // decaying scale impulse (kills, orbs, burst)
    bool  _wasGroundedInk = true;     // for landing detection
    float _vyPrevInk;                 // last frame vertical velocity
    float _landTimer;                 // brief landing-squash window
    const float LAND_SQUASH_TIME = 0.14f;

    /// <summary>Punch the figure's scale (game-feel). Strength ~0.15 light, ~0.4 strong.</summary>
    public void Pop(float strength) { _pop = Mathf.Max(_pop, strength); }

    // ── Materials (created once) ──────────────────────────────────────────────
    Material _inkMat;
    Material _coreMat;   // kept for compatibility — unused at runtime (unlit replaces it)
    Material _jadeMat;
    Material _headMat;
    Material _cowlMat;
    Material _trailMat;
    Material _arcMat;

    // ── Cached runner ref ─────────────────────────────────────────────────────
    PlayerRunner _runner;

    // ─────────────────────────────────────────────────────────────────────────
    void Start()
    {
        try
        {
            // Hide placeholder capsule (Bootstrap names it "Visual").
            var vis = transform.Find("Visual");
            if (vis != null)
            {
                var mr = vis.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = false;
            }

            _runner = GetComponent<PlayerRunner>();

            BuildMaterials();
            BuildRig();
            BuildTrail();
            BuildArcLine();

            // Subscribe to slash event.
            if (_runner != null)
                _runner.Slashed += OnSlashed;
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[InkCultivator] Rig build failed — keeping placeholder. " + ex);
        }
    }

    void OnDestroy()
    {
        if (_runner != null)
            _runner.Slashed -= OnSlashed;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Material factory
    // ─────────────────────────────────────────────────────────────────────────
    void BuildMaterials()
    {
        // Shader chain: URP Lit → Standard (editor fallback).
        var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        _inkMat  = new Material(sh);
        _inkMat.color = ColInkRobe;
        SetLowSmooth(_inkMat);

        _headMat = new Material(sh);
        _headMat.color = ColHead;
        SetLowSmooth(_headMat);

        _jadeMat = new Material(sh);
        _jadeMat.color = ColJade;
        SetLowSmooth(_jadeMat);

        _cowlMat = new Material(sh);
        _cowlMat.color = ColCowl;
        SetLowSmooth(_cowlMat);

        // Emissive core material — kept but superseded by unlit version.
        // Assigned to _coreMat for any legacy usage; runtime uses _coreUnlitMat.
        _coreMat = new Material(sh);
        _coreMat.color = ColCinnabar;
        SetLowSmooth(_coreMat);
        EnableEmission(_coreMat, ColCinnabar * 2.0f);

        // ── Unlit / additive shader chain ─────────────────────────────────────
        var unlitSh = Shader.Find("Sprites/Default")
                   ?? Shader.Find("Universal Render Pipeline/Unlit")
                   ?? Shader.Find("Unlit/Color");

        // Trail material: alpha-blend, mirrors TelegraphSystem.cs pattern.
        _trailMat = new Material(unlitSh);
        _trailMat.SetOverrideTag("RenderType", "Transparent");
        _trailMat.SetFloat("_Surface", 1f);
        _trailMat.SetFloat("_Blend", 0f);
        _trailMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        _trailMat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        _trailMat.SetInt("_ZWrite", 0);
        _trailMat.DisableKeyword("_ALPHATEST_ON");
        _trailMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        _trailMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        _trailMat.renderQueue = (int)RenderQueue.Transparent;

        // Arc material: emissive cinnabar on same alpha-blend base.
        _arcMat = new Material(unlitSh);
        _arcMat.SetOverrideTag("RenderType", "Transparent");
        _arcMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        _arcMat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        _arcMat.SetInt("_ZWrite", 0);
        _arcMat.renderQueue = (int)RenderQueue.Transparent;

        // ── Qi-core UNLIT material: always bright regardless of scene lighting ─
        // Sprites/Default renders at full vertex/material color in all lighting.
        _coreUnlitMat = new Material(unlitSh);
        _coreUnlitMat.SetOverrideTag("RenderType", "Opaque");
        _coreUnlitMat.SetInt("_SrcBlend", (int)BlendMode.One);
        _coreUnlitMat.SetInt("_DstBlend", (int)BlendMode.Zero);
        _coreUnlitMat.SetInt("_ZWrite", 1);
        _coreUnlitMat.renderQueue = (int)RenderQueue.Geometry;
        // Set base color: Sprites/Default uses _Color / _MainTex tint.
        if (_coreUnlitMat.HasProperty("_Color"))
            _coreUnlitMat.SetColor("_Color", ColCinnabar);
        else
            _coreUnlitMat.color = ColCinnabar;

        // ── Halo ADDITIVE material: simulates glow bloom without post-process ─
        // SrcAlpha + One blending adds light on top of whatever is behind it.
        _haloMat = new Material(unlitSh);
        _haloMat.SetOverrideTag("RenderType", "Transparent");
        _haloMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        _haloMat.SetInt("_DstBlend", (int)BlendMode.One);   // ADDITIVE
        _haloMat.SetInt("_ZWrite", 0);
        _haloMat.renderQueue = (int)RenderQueue.Transparent + 1;
        // Start color with moderate alpha; pulsed in Update.
        Color haloStartColor = new Color(ColCinnabar.r, ColCinnabar.g * 0.6f, ColCinnabar.b * 0.3f, 0.55f);
        if (_haloMat.HasProperty("_Color"))
            _haloMat.SetColor("_Color", haloStartColor);
        else
            _haloMat.color = haloStartColor;

        // ── Radial gradient texture for the halo quad ─────────────────────────
        // Soft round glow: full-alpha cinnabar-tinted center fading to alpha=0
        // at the edge. Squared falloff (alpha = (1-d)^2) gives a softer look.
        // This prevents the hard-edged opaque rectangle artifact from the
        // default Sprites/Default + no-texture combination.
        const int TexSize = 64;
        var haloTex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false);
        haloTex.filterMode = FilterMode.Bilinear;
        haloTex.wrapMode   = TextureWrapMode.Clamp;
        float center = (TexSize - 1) * 0.5f;
        float radius = center;
        var pixels = new Color[TexSize * TexSize];
        for (int py = 0; py < TexSize; py++)
        {
            for (int px = 0; px < TexSize; px++)
            {
                float dx   = (px - center) / radius;
                float dy   = (py - center) / radius;
                float dist = Mathf.Sqrt(dx * dx + dy * dy); // 0 at center, 1 at edge
                float t    = Mathf.Clamp01(1f - dist);
                float a    = t * t; // squared = softer falloff
                // Near-white cinnabar tint: bright orange-red toward center.
                float r = Mathf.Lerp(ColCinnabar.r, 1.0f, t * 0.55f);
                float g = Mathf.Lerp(ColCinnabar.g, 0.7f, t * 0.40f);
                float b = Mathf.Lerp(ColCinnabar.b, 0.4f, t * 0.30f);
                pixels[py * TexSize + px] = new Color(r, g, b, a);
            }
        }
        haloTex.SetPixels(pixels);
        haloTex.Apply();
        // Assign to the halo material — Sprites/Default uses _MainTex.
        _haloMat.mainTexture = haloTex;
        if (_haloMat.HasProperty("_MainTex"))
            _haloMat.SetTexture("_MainTex", haloTex);
    }

    static void SetLowSmooth(Material m)
    {
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.08f);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.08f);
        if (m.HasProperty("_Metallic"))   m.SetFloat("_Metallic",   0.00f);
    }

    static void EnableEmission(Material m, Color emissiveColor)
    {
        m.EnableKeyword("_EMISSION");
        if (m.HasProperty("_EmissionColor"))
            m.SetColor("_EmissionColor", emissiveColor);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Rig builder — primitives under "InkFigure"
    // Character feet at y=0 (figure local), head around y=1.85.
    // CharacterController center is at y=1 in world space.
    // ─────────────────────────────────────────────────────────────────────────
    void BuildRig()
    {
        var figureGo = new GameObject("InkFigure");
        figureGo.transform.SetParent(transform, false);
        figureGo.transform.localPosition = Vector3.zero;
        _figure = figureGo.transform;

        // ── ROBE SKIRT: Cylinder gives a natural taper (round at top/bottom). ──
        // Scale X>Y ratio makes it wider than tall — reads as a wide flowing hem.
        // We scale Z to be slightly shallower than X for a slight oval silhouette.
        var robeSkirt = MakePart("Robe_Skirt", PrimitiveType.Cylinder, _inkMat, _figure);
        robeSkirt.localPosition = new Vector3(0f, 0.42f, 0f);
        robeSkirt.localScale    = new Vector3(0.80f, 0.44f, 0.52f); // wide, short cylinder = skirt

        // ── ROBE TORSO: narrower angled shoulder block on top of skirt. ─────────
        // A Cube kept narrower than skirt width reads as the upper body narrowing
        // toward the shoulders — the key silhouette difference from the old design.
        var robeTorso = MakePart("Robe_Torso", PrimitiveType.Cube, _inkMat, _figure);
        robeTorso.localPosition = new Vector3(0f, 1.08f, 0f);
        robeTorso.localScale    = new Vector3(0.56f, 0.70f, 0.36f);

        // ── JADE HEM TRIM: thin flat torus-substitute (flat Cylinder) at base ──
        // A very flat wide cylinder at the base of the skirt reads as hem trim.
        var hemTrim = MakePart("Jade_HemTrim", PrimitiveType.Cylinder, _jadeMat, _figure);
        hemTrim.localPosition = new Vector3(0f, 0.06f, 0f);
        hemTrim.localScale    = new Vector3(0.82f, 0.04f, 0.54f); // very flat wide ring

        // ── JADE COLLAR TRIM: flat thin strip across the shoulders ─────────────
        // A thin wide flat cube at the top of the torso reads as a jade collar band.
        var collar = MakePart("Jade_Collar", PrimitiveType.Cube, _jadeMat, _figure);
        collar.localPosition = new Vector3(0f, 1.44f, 0f);
        collar.localScale    = new Vector3(0.60f, 0.07f, 0.38f);

        // ── JADE SASH: waist accent (front-face visible) ─────────────────────
        var sash = MakePart("Jade_Sash", PrimitiveType.Cube, _jadeMat, _figure);
        sash.localPosition = new Vector3(0f, 0.70f, 0.20f);
        sash.localScale    = new Vector3(0.40f, 0.09f, 0.04f);

        // ── HEAD: small pale sphere — shows as face sliver under the cowl ─────
        // Shrunk ~17% vs previous (0.22 → 0.18) for a more human head-to-body ratio.
        var head = MakePart("Head", PrimitiveType.Sphere, _headMat, _figure);
        head.localPosition = new Vector3(0f, 1.68f, 0.02f);
        head.localScale    = new Vector3(0.18f, 0.18f, 0.18f);

        // ── COWL/HOOD: dark sphere slightly larger than head, wrapped over it ──
        // Also shrunk ~17% (0.30→0.25, 0.32→0.27, 0.36→0.30) so the combined
        // head+cowl reads as a small head on broad shoulders, not a big ball.
        var cowl = MakePart("Cowl_Hood", PrimitiveType.Sphere, _cowlMat, _figure);
        cowl.localPosition = new Vector3(0f, 1.72f, -0.04f);
        cowl.localScale    = new Vector3(0.25f, 0.27f, 0.30f); // ~17% smaller

        // Cowl peak: a small dark sphere at the top of the hood for a pointed-cowl silhouette.
        // Also scaled down proportionally (0.14→0.12, 0.18→0.15).
        var cowlPeak = MakePart("Cowl_Peak", PrimitiveType.Sphere, _cowlMat, _figure);
        cowlPeak.localPosition = new Vector3(0f, 1.92f, -0.06f);
        cowlPeak.localScale    = new Vector3(0.12f, 0.15f, 0.12f);

        // ── RIGHT ARM / SLEEVE: pushed further out for clear arm silhouette ─────
        // X offset 0.34→0.40, angle 16°→24° so sleeve clears the torso block visually.
        var armR = MakePart("Arm_R", PrimitiveType.Cube, _inkMat, _figure);
        armR.localPosition    = new Vector3(0.40f, 1.00f, 0f);
        armR.localScale       = new Vector3(0.13f, 0.44f, 0.13f);
        armR.localEulerAngles = new Vector3(0f, 0f, 24f); // more outward angle
        _armHint = armR; // keep reference for slash arc

        // Right sleeve cuff — jade trim.
        var cuffR = MakePart("Jade_CuffR", PrimitiveType.Cube, _jadeMat, _figure);
        cuffR.localPosition = new Vector3(0.47f, 0.79f, 0f);
        cuffR.localScale    = new Vector3(0.14f, 0.06f, 0.14f);

        // ── LEFT ARM / SLEEVE: mirror of right ─────────────────────────────────
        var armL = MakePart("Arm_L", PrimitiveType.Cube, _inkMat, _figure);
        armL.localPosition    = new Vector3(-0.40f, 1.00f, 0f);
        armL.localScale       = new Vector3(0.13f, 0.44f, 0.13f);
        armL.localEulerAngles = new Vector3(0f, 0f, -24f); // symmetric outward angle
        // (no _armHint assignment — _armHint stays as armR)

        // Left sleeve cuff — jade trim.
        var cuffL = MakePart("Jade_CuffL", PrimitiveType.Cube, _jadeMat, _figure);
        cuffL.localPosition = new Vector3(-0.47f, 0.79f, 0f);
        cuffL.localScale    = new Vector3(0.14f, 0.06f, 0.14f);

        // ── QI CORE: unlit bright-cinnabar sphere at chest ─────────────────────
        // Uses _coreUnlitMat (Sprites/Default unlit) so it reads at full brightness
        // regardless of scene lighting — no bloom required.
        var corePart = MakePart("Qi_Core", PrimitiveType.Sphere, _coreUnlitMat, _figure);
        corePart.localPosition = new Vector3(0f, 1.10f, 0.19f);
        corePart.localScale    = new Vector3(0.13f, 0.13f, 0.13f);
        _qiCore = corePart;

        // ── QI HALO: additive billboard Quad behind the core ───────────────────
        // Additively blended (SrcAlpha + One) so it brightens whatever is behind it,
        // simulating a point-light glow / bloom without any post-processing.
        // Quad primitive faces +Z by default; we rotate it to face the camera axis.
        var haloGo = new GameObject("Qi_Halo");
        haloGo.transform.SetParent(_figure, false);
        haloGo.transform.localPosition = new Vector3(0f, 1.10f, 0.17f); // slightly behind core
        haloGo.transform.localScale    = new Vector3(0.72f, 0.72f, 0.72f); // ~2.8x core diameter for glow spread
        // Quad faces +Z in local space; player faces -Z (forward), so we need no extra rotation
        // to face the camera in a typical over-the-shoulder view. We rely on billboard
        // via the TrailRenderer approach: halo just faces the scene camera each frame in LateUpdate.
        // For simplicity we make it a MeshRenderer Quad with additive mat — it will look halo-ish
        // from the fixed camera angle used in Tribulation (behind+above the player).
        var haloQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        haloQuad.name = "Qi_Halo_Quad";
        haloQuad.transform.SetParent(haloGo.transform, false);
        haloQuad.transform.localPosition = Vector3.zero;
        haloQuad.transform.localScale    = Vector3.one;
        // Destroy collider.
        var haloCol = haloQuad.GetComponent<Collider>();
        if (haloCol != null) Destroy(haloCol);
        // Assign additive halo material.
        var haloRend = haloQuad.GetComponent<Renderer>();
        if (haloRend != null) haloRend.sharedMaterial = _haloMat;
        _qiHalo = haloGo.transform;
    }

    // Create a primitive, destroy its collider, assign material, parent it, return transform.
    static Transform MakePart(string name, PrimitiveType type, Material mat, Transform parent)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);

        // Strip collider — all cosmetic; CharacterController owns collision.
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);

        var rend = go.GetComponent<Renderer>();
        if (rend != null) rend.sharedMaterial = mat;

        return go.transform;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Trail renderer — ink ribbon at figure back/shoulder
    // ─────────────────────────────────────────────────────────────────────────
    void BuildTrail()
    {
        if (_figure == null) return;

        var trailGo = new GameObject("Ink_Trail");
        trailGo.transform.SetParent(_figure, false);
        trailGo.transform.localPosition = new Vector3(0f, 1.30f, 0.22f); // back of shoulder

        var tr = trailGo.AddComponent<TrailRenderer>();
        tr.time              = 0.35f;
        tr.startWidth        = 0.22f;
        tr.endWidth          = 0.00f;
        tr.startColor        = ColTrail;
        tr.endColor          = new Color(ColTrail.r, ColTrail.g, ColTrail.b, 0f);
        tr.material          = _trailMat;
        tr.alignment         = LineAlignment.View; // always faces camera
        tr.autodestruct      = false;
        tr.minVertexDistance = 0.04f;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Slash arc — one pooled LineRenderer; reused each slash
    // ─────────────────────────────────────────────────────────────────────────
    void BuildArcLine()
    {
        var arcGo = new GameObject("Slash_Arc");
        arcGo.transform.SetParent(transform, false); // world-ish; parented to player root
        _arcLine = arcGo.AddComponent<LineRenderer>();
        _arcLine.positionCount = 8;
        _arcLine.useWorldSpace = true;
        _arcLine.startWidth = 0.22f;
        _arcLine.endWidth   = 0.04f;
        _arcLine.startColor = new Color(ColCinnabar.r, ColCinnabar.g, ColCinnabar.b, 1f);
        _arcLine.endColor   = new Color(1f, 0.8f, 0.6f, 0f);
        _arcLine.material   = _arcMat;
        _arcLine.alignment  = LineAlignment.View;
        arcGo.SetActive(false);
    }

    void OnSlashed()
    {
        _arcTimer = ARC_DURATION;
        if (_arcLine != null && _arcLine.gameObject != null)
            _arcLine.gameObject.SetActive(true);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Update — drive poses, arc fade, qi core pulse
    // ─────────────────────────────────────────────────────────────────────────
    void Update()
    {
        if (_figure == null) return;

        // ── Arc fade ──────────────────────────────────────────────────────────
        if (_arcLine != null && _arcTimer > 0f)
        {
            _arcTimer -= Time.deltaTime;
            float t = Mathf.Clamp01(_arcTimer / ARC_DURATION);
            UpdateArcPositions();
            var sc = _arcLine.startColor; sc.a = t;        _arcLine.startColor = sc;
            var ec = _arcLine.endColor;   ec.a = t * 0.3f; _arcLine.endColor   = ec;
            if (_arcTimer <= 0f && _arcLine.gameObject != null)
                _arcLine.gameObject.SetActive(false);
        }

        // ── Qi core pulse (unlit + halo approach, no bloom needed) ────────────
        // Pulse drives: (a) _coreUnlitMat._Color brightness, (b) _haloMat alpha.
        // Both are materials on Sprites/Default (unlit), so color = visible color.
        if (_qiCore != null && _coreUnlitMat != null)
        {
            float pulse = 1.0f + Mathf.Sin(Time.time * 3.5f) * 0.25f; // 0.75..1.25
            Color coreColor = new Color(
                ColCinnabar.r * pulse,
                ColCinnabar.g * pulse,
                ColCinnabar.b * pulse,
                1f);
            // Clamp to avoid exceeding 1 on Sprites/Default (it won't overbright without HDR)
            coreColor.r = Mathf.Clamp01(coreColor.r);
            coreColor.g = Mathf.Clamp01(coreColor.g);
            coreColor.b = Mathf.Clamp01(coreColor.b);
            if (_coreUnlitMat.HasProperty("_Color"))
                _coreUnlitMat.SetColor("_Color", coreColor);
            else
                _coreUnlitMat.color = coreColor;

            // Halo alpha pulsing — additive so higher alpha = more light added.
            if (_haloMat != null)
            {
                float haloAlpha = 0.45f + Mathf.Sin(Time.time * 3.5f) * 0.30f; // 0.15..0.75
                Color haloColor = new Color(
                    ColCinnabar.r,
                    ColCinnabar.g * 0.5f,
                    ColCinnabar.b * 0.2f,
                    haloAlpha);
                if (_haloMat.HasProperty("_Color"))
                    _haloMat.SetColor("_Color", haloColor);
                else
                    _haloMat.color = haloColor;
            }
        }

        // ── Billboard halo: face the main camera each frame ───────────────────
        if (_qiHalo != null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                // Make the halo quad face the camera (billboard).
                Vector3 toCamera = mainCam.transform.position - _qiHalo.position;
                if (toCamera.sqrMagnitude > 0.001f)
                    _qiHalo.rotation = Quaternion.LookRotation(-toCamera, Vector3.up);
            }
        }

        if (_runner == null) return;

        bool  grounded  = _runner.Grounded;
        bool  sliding   = _runner.IsSliding;
        bool  dead      = _runner.IsDead;
        int   lane      = _runner.Lane;
        float vy        = _runner.Vy;
        bool  rising    = !grounded && vy > 1f;

        // Landing impact squash (dust puff is handled universally by PlayerRunner).
        if (grounded && !_wasGroundedInk && _vyPrevInk < -5f && !dead)
            _landTimer = LAND_SQUASH_TIME;
        _wasGroundedInk = grounded; _vyPrevInk = vy;

        // ── Determine target pose ─────────────────────────────────────────────
        Vector3 targetScale;
        Vector3 targetLocalPos;
        float   targetPitch; // X-rotation: forward lean (negative = lean forward)
        float   targetRoll;  // Z-rotation: bank for lane-change

        if (dead)
        {
            // Topple forward.
            targetScale    = new Vector3(1f, 1f, 1f);
            targetLocalPos = new Vector3(0f, -0.40f, 0.20f);
            targetPitch    = -75f;
            targetRoll     = 0f;
        }
        else if (sliding)
        {
            // Squash down, forward lean.
            targetScale    = new Vector3(1.20f, 0.55f, 1.10f);
            targetLocalPos = new Vector3(0f, -0.50f, 0f);
            targetPitch    = -20f;
            targetRoll     = 0f;
        }
        else if (rising)
        {
            // Stretch upward, slight arms-up lean.
            float stretchT = Mathf.Clamp01(vy / 17f);
            float scaleY   = Mathf.Lerp(1f, 1.18f, stretchT);
            float scaleXZ  = Mathf.Lerp(1f, 0.88f, stretchT);
            targetScale    = new Vector3(scaleXZ, scaleY, scaleXZ);
            targetLocalPos = new Vector3(0f, 0.12f * stretchT, 0f);
            targetPitch    = 6f;
            targetRoll     = 0f;
        }
        else
        {
            // Run: gentle bob + forward lean.
            float bob      = Mathf.Sin(Time.time * 10f) * 0.045f;
            targetScale    = new Vector3(1f, 1f, 1f);
            targetLocalPos = new Vector3(0f, bob, 0f);
            targetPitch    = -8f;
            targetRoll     = 0f;
        }

        // ── Lane-change bank ──────────────────────────────────────────────────
        if (lane != _prevLane && !dead && !sliding)
        {
            _bankDir   = (lane < _prevLane) ? 1 : -1; // capture BEFORE updating _prevLane
            _bankTimer = BANK_HOLD;
            _prevLane  = lane;
        }
        if (_bankTimer > 0f) _bankTimer -= Time.deltaTime;

        if (_bankTimer > 0f && !dead && !sliding)
        {
            float bankFrac = Mathf.Sin(Mathf.Clamp01(_bankTimer / BANK_HOLD) * Mathf.PI);
            targetRoll = _bankDir * BANK_ANGLE * bankFrac;
        }

        // Landing squash overrides the pose scale for a brief window after touchdown.
        if (_landTimer > 0f)
        {
            _landTimer -= Time.deltaTime;
            if (grounded && !sliding && !dead) targetScale = new Vector3(1.22f, 0.74f, 1.14f);
        }

        // ── Smooth toward targets ─────────────────────────────────────────────
        float smooth = dead ? 0.25f : 0.10f; // slow topple, fast pose

        // SmoothDamp the BASE scale, then apply a crisp decaying pop on top so kills/orbs
        // punch through the smoothing instead of being damped away.
        _baseScale = Vector3.SmoothDamp(_baseScale, targetScale, ref _figureScaleVel, smooth);
        _pop = Mathf.Lerp(_pop, 0f, Mathf.Clamp01(11f * Time.deltaTime));
        _figure.localScale = _baseScale * (1f + _pop);

        _figure.localPosition = Vector3.SmoothDamp(
            _figure.localPosition, targetLocalPos, ref _figurePosVel, smooth);

        _currentPitch = Mathf.SmoothDamp(_currentPitch, targetPitch, ref _figureYawVel, smooth);
        _currentRoll  = Mathf.SmoothDamp(_currentRoll,  targetRoll,  ref _figureRotVel, smooth * 1.3f);

        _figure.localEulerAngles = new Vector3(_currentPitch, 0f, _currentRoll);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Arc sweep — 8-point arc from arm outward in a crescent (world space)
    // ─────────────────────────────────────────────────────────────────────────
    void UpdateArcPositions()
    {
        if (_arcLine == null || _armHint == null) return;

        Vector3 origin = _armHint.position;
        float t01    = Mathf.Clamp01(_arcTimer / ARC_DURATION);
        float radius = 0.70f;
        float sweepAngle = 110f * t01; // arc sweeps open over the duration

        int pts = _arcLine.positionCount; // 8
        for (int i = 0; i < pts; i++)
        {
            float frac  = (float)i / (pts - 1);
            float angle = Mathf.Lerp(-sweepAngle * 0.5f, sweepAngle * 0.5f, frac);
            float rad   = angle * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(
                Mathf.Cos(rad),
                Mathf.Sin(rad) * 0.5f,
                -Mathf.Abs(Mathf.Sin(rad)) * 0.3f);
            _arcLine.SetPosition(i, origin + dir * radius);
        }
    }
}
