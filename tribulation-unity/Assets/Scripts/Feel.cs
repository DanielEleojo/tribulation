using UnityEngine;
using UnityEngine.Rendering;
using PrimeTween;

// Implemented by whichever character-visual driver is active (RiggedCharacter or the
// procedural InkCultivator fallback) so PlayerRunner can punch its scale without caring which.
public interface IFeelPose { void Pop(float strength); }

// Game-feel helpers (Feel Pass v1/v2 + pooling pass).
// Built on the already-installed PrimeTween. Shared materials + glow texture are built ONCE
// and reused; the burst ParticleSystem is drawn from a small ring-buffer pool. Every call-site
// keeps its original signature — this is a GC/perf refactor behind a stable API.
public static class Feel
{
    // ── Shared cached resources (lazy-built once, reused for every effect) ────
    static Texture2D _glowTex;
    static Material  _glowWhite;   // additive glow, white  — particle renderer (color comes from startColor)
    static Material  _ringMat;     // additive glow, jade   — CollectPop expanding ring
    static Material  _arcMat;      // additive glow, cyan   — SlashArc streak + slash trail
    static Material  _poofMat;     // opaque grey-brown     — dust sphere
    static Gradient  _fadeGrad;    // alpha 1→0 for particle colorOverLifetime
    static GameObject _host;       // hidden DontDestroyOnLoad parent for pooled objects

    static void EnsureResources()
    {
        if (_glowWhite != null) return;

        _glowTex = InkArt.SoftGlow(64).texture; // was regenerated per call — now once
        var addSh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                 ?? Shader.Find("Sprites/Default");
        _glowWhite = MakeAdditive(addSh, _glowTex, Color.white);
        _ringMat   = MakeAdditive(addSh, _glowTex, new Color(0.5f, 1f, 0.8f, 1f));
        _arcMat    = MakeAdditive(addSh, _glowTex, new Color(0.85f, 1f, 1f, 1f));

        // Device builds strip unreferenced shaders: URP/Unlit ships via the
        // ShaderKeep material (Resources/ShaderKeep/UnlitOpaque.mat); the final
        // ?? addSh fallback means a null shader can never reach new Material()
        // (the first on-device slash used to throw ArgumentNullException here).
        var opSh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard") ?? addSh;
        _poofMat = new Material(opSh) { color = new Color(0.72f, 0.70f, 0.64f) };

        _fadeGrad = new Gradient();
        _fadeGrad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
    }

    static Material MakeAdditive(Shader sh, Texture tex, Color c)
    {
        var m = new Material(sh);
        m.SetTexture("_BaseMap", tex);
        m.mainTexture = tex;
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_ZWrite",  0f);
        m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)BlendMode.One); // additive
        m.renderQueue = (int)RenderQueue.Transparent;
        return m;
    }

    static Transform Host()
    {
        if (_host == null)
        {
            _host = new GameObject("~FeelPool") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(_host);
        }
        return _host.transform;
    }

    // ── Impact freeze ────────────────────────────────────────────────────────
    // Restores on UNSCALED time so the restore fires even though the freeze slows scaled time.
    // ponytail: global timeScale dip — fine for a single-player runner.
    public static void Hitstop(float seconds = 0.06f, float scale = 0.06f)
    {
        if (Time.timeScale == 0f) return; // don't fight the pause menu
        Time.timeScale = scale;
        Tween.Delay(seconds, () => { if (Time.timeScale != 0f) Time.timeScale = 1f; }, useUnscaledTime: true);
    }

    // ── Dust puff ────────────────────────────────────────────────────────────
    // Flattened sphere that grows then collapses. Cached opaque material (no per-call alloc).
    public static void Poof(Vector3 pos, float size = 1.2f)
    {
        EnsureResources();
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.position = pos + Vector3.up * 0.1f;
        go.transform.localScale = Vector3.one * 0.15f;
        var mr = go.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.sharedMaterial = _poofMat;
        Tween.Scale(go.transform, new Vector3(size, size * 0.4f, size), 0.12f, Ease.OutQuad, 2, CycleMode.Yoyo)
             .OnComplete(() => { if (go != null) Object.Destroy(go); });
    }

    // ── Pooled one-shot particle burst ───────────────────────────────────────
    const int BURST_POOL = 12;
    static ParticleSystem[] _burstPool;
    static int _burstIdx;

    static ParticleSystem AcquireBurst()
    {
        EnsureResources();
        if (_burstPool == null) _burstPool = new ParticleSystem[BURST_POOL];
        _burstIdx = (_burstIdx + 1) % BURST_POOL;
        var ps = _burstPool[_burstIdx];
        if (ps == null)
        {
            var go = new GameObject("FeelBurst");
            go.transform.SetParent(Host(), false);
            ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop            = false;
            main.playOnAwake     = false;

            var em = ps.emission; em.rateOverTime = 0f;
            var shp = ps.shape; shp.shapeType = ParticleSystemShapeType.Sphere; shp.radius = 0.1f;
            var col = ps.colorOverLifetime; col.enabled = true; col.color = _fadeGrad;

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial    = _glowWhite;  // shared, not per-call
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows    = false;

            _burstPool[_burstIdx] = ps;
        }
        return ps;
    }

    // One-shot additive burst. Color per call via the particle start-color (no material churn).
    public static void Burst(Vector3 pos, Color color, int count, float speed, float size, float life,
                             float gravity = 0.2f)
    {
        var ps = AcquireBurst();
        ps.transform.position = pos;
        var main = ps.main;
        main.startLifetime   = life;
        main.startSpeed      = speed;
        main.startSize       = size;
        main.startColor      = color;
        main.gravityModifier = gravity;
        ps.Emit(count);
    }

    // Hot spark flash for enemy kills: white-gold, sharp and fast.
    public static void Spark(Vector3 pos)
        => Burst(pos, new Color(1f, 0.95f, 0.7f, 1f), count: 14, speed: 6f, size: 0.3f, life: 0.35f, gravity: 0.1f);

    // Jade qi pickup pop: soft teal scatter + a quick expanding ring.
    public static void CollectPop(Vector3 pos)
    {
        Burst(pos, new Color(0.5f, 1f, 0.8f, 1f), count: 10, speed: 3f, size: 0.35f, life: 0.4f, gravity: 0.05f);

        EnsureResources();
        var ring = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.Destroy(ring.GetComponent<Collider>());
        ring.transform.position   = pos;
        ring.transform.localScale = Vector3.one * 0.2f;
        ring.transform.rotation   = Quaternion.Euler(90f, 0f, 0f); // lie flat on the ground plane

        var rend = ring.GetComponent<MeshRenderer>();
        rend.shadowCastingMode = ShadowCastingMode.Off;
        rend.receiveShadows    = false;
        rend.sharedMaterial    = _ringMat;

        Tween.Scale(ring.transform, Vector3.one * 2f, 0.3f, Ease.OutQuad)
             .OnComplete(() => { if (ring != null) Object.Destroy(ring); });
    }

    // Soft dust kick-up for jump and slide: warm grey-brown, low-gravity drift.
    public static void DustBurst(Vector3 pos)
        => Burst(pos, new Color(0.72f, 0.70f, 0.64f, 0.8f), count: 8, speed: 2f, size: 0.5f, life: 0.5f, gravity: 0.35f);

    // Brief bright crescent flash in front of the player for the slash attack.
    public static void SlashArc(Vector3 pos)
    {
        EnsureResources();
        var arc = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.Destroy(arc.GetComponent<Collider>());
        arc.transform.position   = pos + new Vector3(0f, 1f, -0.5f); // ahead in -Z, chest height
        arc.transform.localScale = new Vector3(2.5f, 0.6f, 1f);
        arc.transform.rotation   = Quaternion.identity;

        var rend = arc.GetComponent<MeshRenderer>();
        rend.shadowCastingMode = ShadowCastingMode.Off;
        rend.receiveShadows    = false;
        rend.sharedMaterial    = _arcMat;

        Tween.Scale(arc.transform, new Vector3(3.5f, 0.3f, 1f), 0.18f, Ease.OutQuad)
             .OnComplete(() => { if (arc != null) Object.Destroy(arc); });
    }

    // ── Hero slash trail (polish lever) ──────────────────────────────────────
    // One reusable additive TrailRenderer swept along a short diagonal in front of the player,
    // reading as a single ink brush-stroke. Cleared on restart so it never streaks across the world.
    static TrailRenderer _slashTrail;

    static TrailRenderer AcquireTrail()
    {
        EnsureResources();
        if (_slashTrail == null)
        {
            var go = new GameObject("FeelSlashTrail");
            go.transform.SetParent(Host(), false);
            _slashTrail = go.AddComponent<TrailRenderer>();
            _slashTrail.time             = 0.22f;                                 // points linger → fade
            _slashTrail.widthCurve       = AnimationCurve.EaseInOut(0f, 0.30f, 1f, 0f); // taper to a point
            _slashTrail.numCapVertices   = 3;
            _slashTrail.sharedMaterial   = _arcMat;                               // white-cyan additive
            _slashTrail.shadowCastingMode = ShadowCastingMode.Off;
            _slashTrail.receiveShadows   = false;
            _slashTrail.autodestruct     = false;
            _slashTrail.emitting         = false;
        }
        return _slashTrail;
    }

    public static void SlashTrail(Transform follow)
    {
        if (follow == null) return;
        var tr = AcquireTrail();
        Vector3 basePos = follow.position + new Vector3(0f, 1f, -0.4f);
        Vector3 start   = basePos + new Vector3(0.8f, -0.5f, 0f);   // low-right
        Vector3 end     = basePos + new Vector3(-0.8f, 0.7f, 0f);   // up-left sweep
        tr.transform.position = start;
        tr.Clear();
        tr.emitting = true;
        Tween.Position(tr.transform, end, 0.16f, Ease.OutQuad)
             .OnComplete(() => { if (_slashTrail != null) _slashTrail.emitting = false; });
    }

    // ── Restart safety ───────────────────────────────────────────────────────
    // Stop/clear every live pooled effect so nothing streaks or lingers across a run restart.
    public static void ClearActive()
    {
        if (_slashTrail != null) { _slashTrail.emitting = false; _slashTrail.Clear(); }
        if (_burstPool != null)
            for (int i = 0; i < _burstPool.Length; i++)
                if (_burstPool[i] != null) _burstPool[i].Clear();
    }
}
