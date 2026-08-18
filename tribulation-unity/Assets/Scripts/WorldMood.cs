// WorldMood.cs — per-realm world theming + reactive atmosphere. Port of game.gd's
// _stages table, _apply_theme(), the speed-reactive fog, the HDRI panorama sky, and
// the tribulation heart demon. The world, the threats, and the cultivator's aura all
// evolve as you ascend: green forest → dusk sect → spirit blue → demonic purple →
// golden heaven. Bootstrap adds this once; it polls Core.Realm/InTribulation each
// frame (cheap) so restarts, resets, and save-loads re-theme without event plumbing.
//
// Ground tints are the Godot hues re-calibrated for the pale crazy-paving road (the
// Godot values were dark absolute albedos on untextured boxes; here they multiply a
// textured runway, so path tints sit ~0.7-0.9 brightness with the same hue).
using UnityEngine;

public class WorldMood : MonoBehaviour
{
    // Fog thickens with run speed — pressure cue (game.gd FOG_BASE→FOG_MAX span).
    const float FOG_SPEED_SPAN = 0.008f;

    struct Stage
    {
        public bool  forest;       // forest sky early realms, night sky beyond
        public Color amb;          // flat ambient color
        public float ambE;         // ambient energy
        public Color fog;          // fog color
        public float dens;         // base fog density (speed lerps above this)
        public float sky;          // skybox exposure
        public Color bed, path, accent;      // ground: shoulders, runway tint, lane lines
        public Color aura;                   // qi-glow light color
        public Color low, high, foe;         // hazards: jump (block), slide (bar), enemy
    }

    static Color C(float r, float g, float b) => new Color(r, g, b);

    // Unity realm 0..5 (Qi Condensation … Ascension) mapped over game.gd stages 0..5.
    static readonly Stage[] STAGES =
    {
        new Stage { forest = true,  amb = C(0.55f,0.60f,0.50f), ambE = 0.85f, fog = C(0.34f,0.40f,0.32f), dens = 0.012f, sky = 1.0f,
                    bed = C(0.44f,0.40f,0.28f), path = C(0.80f,0.74f,0.60f), accent = C(0.50f,0.62f,0.32f),
                    aura = C(0.60f,0.66f,0.72f), low = C(0.55f,0.42f,0.25f), high = C(0.50f,0.70f,0.45f), foe = C(0.45f,0.32f,0.22f) },
        new Stage { forest = true,  amb = C(0.50f,0.56f,0.55f), ambE = 0.80f, fog = C(0.30f,0.36f,0.36f), dens = 0.013f, sky = 1.0f,
                    bed = C(0.42f,0.42f,0.38f), path = C(0.78f,0.76f,0.74f), accent = C(0.50f,0.80f,0.60f),
                    aura = C(0.55f,0.82f,0.62f), low = C(0.50f,0.45f,0.30f), high = C(0.45f,0.75f,0.60f), foe = C(0.50f,0.45f,0.35f) },
        new Stage { forest = false, amb = C(0.50f,0.50f,0.60f), ambE = 0.55f, fog = C(0.10f,0.09f,0.13f), dens = 0.012f, sky = 1.0f,
                    bed = C(0.34f,0.34f,0.44f), path = C(0.70f,0.70f,0.82f), accent = C(0.95f,0.80f,0.35f),
                    aura = C(0.95f,0.80f,0.35f), low = C(0.95f,0.55f,0.20f), high = C(0.30f,0.80f,1.00f), foe = C(0.80f,0.82f,0.92f) },
        new Stage { forest = false, amb = C(0.45f,0.55f,0.70f), ambE = 0.60f, fog = C(0.12f,0.16f,0.26f), dens = 0.011f, sky = 1.1f,
                    bed = C(0.36f,0.43f,0.58f), path = C(0.66f,0.74f,0.88f), accent = C(0.45f,0.70f,1.00f),
                    aura = C(0.45f,0.70f,1.00f), low = C(0.40f,0.60f,1.00f), high = C(0.50f,0.85f,1.00f), foe = C(0.55f,0.70f,0.95f) },
        new Stage { forest = false, amb = C(0.55f,0.45f,0.70f), ambE = 0.60f, fog = C(0.18f,0.12f,0.26f), dens = 0.012f, sky = 1.1f,
                    bed = C(0.44f,0.34f,0.54f), path = C(0.74f,0.62f,0.86f), accent = C(0.72f,0.48f,1.00f),
                    aura = C(0.72f,0.48f,1.00f), low = C(0.80f,0.40f,1.00f), high = C(0.70f,0.50f,1.00f), foe = C(0.50f,0.35f,0.55f) },
        new Stage { forest = false, amb = C(0.85f,0.80f,0.62f), ambE = 1.10f, fog = C(0.70f,0.62f,0.40f), dens = 0.012f, sky = 1.4f,
                    bed = C(0.56f,0.52f,0.42f), path = C(0.88f,0.82f,0.68f), accent = C(1.00f,0.92f,0.55f),
                    aura = C(1.00f,0.95f,0.70f), low = C(1.00f,0.85f,0.40f), high = C(0.90f,0.95f,1.00f), foe = C(0.90f,0.85f,0.70f) },
    };

    Material  _skybox;    // instance of the ShaderKeep panoramic mat (shared asset untouched)
    Texture2D _skyForest, _skyNight;
    PlayerRunner _runner;
    Light  _qiGlow;
    Ground _ground;
    Spawner _spawner;
    int    _lastRealm = -1;
    float  _fogBase = 0.012f;
    GameObject _heartDemon;

    void Start()
    {
        _skyForest = Resources.Load<Texture2D>("Sky/sky_forest");
        _skyNight  = Resources.Load<Texture2D>("Sky/sky_night");

        // The panoramic skybox shader must come from a Resources material — a bare
        // Shader.Find would be stripped from device builds (same ShaderKeep trick as
        // the particle material).
        var baseMat = Resources.Load<Material>("ShaderKeep/SkyboxPanoramic");
        if (baseMat != null && (_skyForest != null || _skyNight != null))
        {
            _skybox = new Material(baseMat);
            RenderSettings.skybox = _skybox;
            var cam = Camera.main;
            if (cam != null) cam.clearFlags = CameraClearFlags.Skybox;
        }
        else
        {
            Debug.LogWarning("[WorldMood] Panoramic skybox unavailable — keeping solid-color background.");
        }

        _ground  = FindObjectOfType<Ground>();
        _spawner = FindObjectOfType<Spawner>();
    }

    void Update()
    {
        var core = Game.I != null ? Game.I.Core : null;
        if (core == null) return;

        if (_runner == null) _runner = FindObjectOfType<PlayerRunner>();
        if (_qiGlow == null)
        {
            var qi = GameObject.Find("QiGlow");
            if (qi != null) _qiGlow = qi.GetComponent<Light>();
        }

        // Realm change (run start, breakthrough, reset, save-load) → re-theme.
        int realm = Mathf.Clamp(core.Realm, 0, STAGES.Length - 1);
        if (realm != _lastRealm)
        {
            _lastRealm = realm;
            Apply(realm);
        }

        // Fog thickens with run speed above the stage's base density.
        float speed = (_runner != null && !core.IsDead) ? _runner.GetSpeedFraction() : 0f;
        RenderSettings.fogDensity = _fogBase + FOG_SPEED_SPAN * speed;

        // Heart demon looms through a tribulation, gone the instant it ends
        // (surmounted OR died — both clear InTribulation).
        if (core.InTribulation && _heartDemon == null) SpawnHeartDemon();
        else if (!core.InTribulation && _heartDemon != null) { Destroy(_heartDemon); _heartDemon = null; }
    }

    void Apply(int realm)
    {
        var s = STAGES[realm];

        if (_skybox != null)
        {
            var tex = s.forest ? (_skyForest != null ? _skyForest : _skyNight)
                               : (_skyNight  != null ? _skyNight  : _skyForest);
            _skybox.SetTexture("_MainTex", tex);
            _skybox.SetFloat("_Exposure", s.sky);
        }

        // 0.7 factor: Godot's ambient energies read hotter than URP's flat ambient —
        // this keeps the dusk depth while the hue still shifts per realm.
        RenderSettings.ambientLight = s.amb * (s.ambE * 0.7f);
        RenderSettings.fogColor = s.fog;
        _fogBase = s.dens;

        if (_ground  == null) _ground  = FindObjectOfType<Ground>();
        if (_spawner == null) _spawner = FindObjectOfType<Spawner>();
        if (_ground  != null) _ground.SetTheme(s.bed, s.path, s.accent);
        if (_spawner != null) _spawner.SetHazardTheme(s.low, s.high, s.foe);

        // Cultivator's aura brightens and widens as you ascend (mortal spark → halo).
        if (_qiGlow != null)
        {
            _qiGlow.color     = s.aura;
            _qiGlow.intensity = 1.2f + realm * 0.25f;
            _qiGlow.range     = 3.8f + realm * 0.4f;
        }

        Debug.Log($"[WorldMood] Applied realm {realm} theme ({(s.forest ? "forest" : "night")} sky).");
    }

    // Dark orb with a smoldering blood-red glow hanging ahead in the sky for the
    // duration of a Heavenly Tribulation (port of game.gd _spawn_heart_demon).
    void SpawnHeartDemon()
    {
        var cam = Camera.main;
        if (cam == null) return;

        _heartDemon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _heartDemon.name = "HeartDemon";
        Destroy(_heartDemon.GetComponent<Collider>());
        _heartDemon.transform.SetParent(cam.transform, false);
        _heartDemon.transform.localPosition = new Vector3(0f, 13f, -72f);
        _heartDemon.transform.localScale = Vector3.one * 12f;

        var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(sh);
        m.SetColor("_BaseColor", Color.black); // near-black orb — the red comes from emission only
        m.color = Color.black;
        m.SetFloat("_Smoothness", 0f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", new Color(0.45f, 0.03f, 0.08f)); // smolder, not a pink moon
        _heartDemon.GetComponent<Renderer>().sharedMaterial = m;
    }
}
