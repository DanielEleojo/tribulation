// UI-2: world-space attack-telegraph renderer.
// Owns a pool of LineRenderer objects (each on its own GameObject, NOT under any hazard)
// and drives them each frame with wind-up brightness + width as the hazard closes on the player.
// "Ink & Talisman" palette: qi-jade cyan, cinnabar red, amber gold, ink-white.
//
// ponytail: straight line for every plane — a true brush-texture crescent for Low-plane
//           and a Z-streak for Lane-plane are visual polish deferred to a later issue.
// ponytail: no external textures/assets — glow achieved via additive-ish bright unlit color.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Tribulation.Core;

public class TelegraphSystem : MonoBehaviour
{
    // ── Singleton ────────────────────────────────────────────────────────────
    public static TelegraphSystem I;

    // ── Ink & Talisman palette ───────────────────────────────────────────────
    // Source of truth: TelegraphColor enum → world-space UI color.
    static readonly Color PaletteAmber = new Color(0.910f, 0.635f, 0.239f); // #E8A23D qi-gold
    static readonly Color PaletteCyan  = new Color(0.361f, 0.847f, 0.757f); // #5CD8C2 qi-jade
    static readonly Color PaletteRed   = new Color(0.878f, 0.275f, 0.235f); // #E0463C cinnabar
    static readonly Color PaletteWhite = new Color(0.949f, 0.937f, 0.878f); // #F2EFE0 ink-white

    // ── Wind-up parameters ───────────────────────────────────────────────────
    const float WINDUP_FAR  = 45f; // ahead distance where telegraph starts appearing
    const float WINDUP_NEAR = 15f; // ahead distance where it reaches full intensity

    // Bold by default so it's unmistakable on a bright scene; wind-up still ramps these.
    const float ALPHA_MIN  = 0.55f;
    const float ALPHA_MAX  = 1.00f;
    const float WIDTH_MIN  = 0.25f;
    const float WIDTH_MAX  = 0.55f;

    const float PULSE_SPEED = 6f;   // radians/s of the brightness pulse
    const float PULSE_RANGE = 0.12f; // fraction of WIDTH_MAX added at full t

    // ── Plane heights ────────────────────────────────────────────────────────
    const float Y_LOW          = 0.06f; // just above the ground (amber sweep)
    const float Y_HIGH         = 1.20f; // bar bottom edge (cyan slash)
    const float Y_DESTRUCTIBLE = 1.30f; // chest height (white blocking disciple)
    const float Y_LANE         = 1.00f; // mid-body height for lane hazards (cinnabar)
    const float LANE_TEL_WIDTH = 1.00f; // fixed width for Destructible marker (~foe's lane)

    // ── Active-record struct ─────────────────────────────────────────────────
    struct TelEntry
    {
        public Transform     hazard;
        public LineRenderer  line;
        public float         planeY;
        public float         halfWidth;
        public Color         baseColor;
        public TextMesh      name;     // null when not a first-encounter announcement
    }

    // ── State ────────────────────────────────────────────────────────────────
    readonly List<TelEntry>       _active      = new List<TelEntry>();
    readonly Stack<LineRenderer>  _pool        = new Stack<LineRenderer>();
    readonly Telegraph            _fallbackTele = new Telegraph(); // used only if Game.I is null

    Transform _player;
    Material  _lineMat; // shared across all pooled line renderers

    // ── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;

        // Build a shared unlit material for all lines.
        // URP Unlit → fallback Sprites/Default → Unlit/Color.
        // Sprites/Default honors LineRenderer vertex colors AND alpha-blends. URP/Unlit
        // IGNORES vertex color → every line rendered white. Prefer Sprites/Default.
        var shader = Shader.Find("Sprites/Default")
                  ?? Shader.Find("Universal Render Pipeline/Unlit")
                  ?? Shader.Find("Unlit/Color");
        _lineMat = new Material(shader);
        // Standard alpha-blend (NOT additive — additive washes out against the bright
        // sky/parchment scene). Solid colored stroke that reads against the ground.
        _lineMat.SetOverrideTag("RenderType", "Transparent");
        _lineMat.SetFloat("_Surface", 1f);                 // 0 opaque, 1 transparent
        _lineMat.SetFloat("_Blend", 0f);                   // 0 = alpha blend
        _lineMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        _lineMat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        _lineMat.SetInt("_ZWrite", 0);
        _lineMat.DisableKeyword("_ALPHATEST_ON");
        _lineMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        _lineMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        _lineMat.renderQueue = (int)RenderQueue.Transparent;

        // ── Ink & Talisman art pass: brush-stroke texture on all telegraph lines ──
        // Apply the BrushStroke sprite texture to the shared line material so every
        // LineRenderer reads as a soft calligraphic qi-stroke instead of a hard bar.
        // textureMode = Stretch is set on each new LineRenderer in AcquireLine().
        var brushTex = InkArt.BrushStroke(128, 16).texture;
        _lineMat.mainTexture = brushTex;
        // ponytail: a wider low-alpha second-pass glow LineRenderer would add bloom;
        //           skipped — would require doubling the pool count for marginal gain.
    }

    void Update()
    {
        // Lazily find player (mirrors pattern in Spawner.cs).
        if (_player == null)
        {
            var pr = FindObjectOfType<PlayerRunner>();
            if (pr == null) return;
            _player = pr.transform;
        }

        float playerZ = _player.position.z;

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var e = _active[i];

            // Recycle if the hazard is gone or has passed the player.
            bool gone = e.hazard == null
                     || !e.hazard.gameObject.activeSelf
                     || e.hazard.position.z > playerZ; // behind player (+Z axis)
            if (gone)
            {
                Recycle(e.line);
                if (e.name != null) Destroy(e.name.gameObject);
                _active.RemoveAt(i);
                continue;
            }

            // ── Reposition endpoints ─────────────────────────────────────────
            float cx = e.hazard.position.x;
            float cz = e.hazard.position.z;
            e.line.SetPosition(0, new Vector3(cx - e.halfWidth, e.planeY, cz));
            e.line.SetPosition(1, new Vector3(cx + e.halfWidth, e.planeY, cz));

            // ── Wind-up ──────────────────────────────────────────────────────
            // ahead: positive when the hazard is in front of the player.
            float ahead = playerZ - cz;
            float t = Mathf.Clamp01(Mathf.InverseLerp(WINDUP_FAR, WINDUP_NEAR, ahead));

            float alpha = Mathf.Lerp(ALPHA_MIN, ALPHA_MAX, t);
            float pulse = Mathf.Sin(Time.time * PULSE_SPEED) * PULSE_RANGE * t;
            float width = Mathf.Lerp(WIDTH_MIN, WIDTH_MAX, t) + pulse;

            // Drive color + alpha.
            Color c = e.baseColor;
            c.a = alpha;
            // Slight brightness lift at high t (kept low so the plane COLOR stays saturated
            // — a big multiply blows every line toward white and you lose the amber/jade read).
            float bright = 1f + 0.25f * t;
            var lit = new Color(c.r * bright, c.g * bright, c.b * bright, alpha);

            // Taper alpha at the ends for brush feel (start opaque, end fades).
            var endColor = new Color(lit.r, lit.g, lit.b, alpha * 0.25f);
            e.line.startColor = lit;
            e.line.endColor   = endColor;

            // Width ramp.
            e.line.startWidth = width;
            e.line.endWidth   = width * 0.55f; // taper to a point — calligraphic tail

            // ── Name label: position + billboard ────────────────────────────
            if (e.name != null)
            {
                var nameTf = e.name.transform;
                // Float above the telegraph line so it doesn't occlude it.
                nameTf.position = new Vector3(cx, e.planeY + 0.9f, cz);
                // Billboard toward camera so it stays readable.
                // note: cache Camera.main if desired; guarded lookup is fine for rare labels.
                if (Camera.main != null)
                    nameTf.forward = Camera.main.transform.forward;

                // Fade alpha in with wind-up t so the name resolves as the attack commits.
                var nameColor = e.name.color;
                nameColor.a = t;
                e.name.color = nameColor;
            }
        }
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Attach a telegraph line to a spawned hazard. Call once per spawn.
    /// <paramref name="width"/> is the hazard's X extent (used as line full-width).
    /// For Destructible hazards the line is clamped to LANE_TEL_WIDTH regardless.
    /// </summary>
    public void Attach(Transform hazard, HazardKind kind, float width)
    {
        var info = Telegraph.Resolve(kind);

        float planeY     = PlaneHeight(info.Plane);
        float halfWidth  = (info.Plane == AttackPlane.Destructible
                            ? LANE_TEL_WIDTH
                            : width) * 0.5f;
        Color baseColor  = PaletteFor(info.Color);

        var lr = AcquireLine();
        SetupLine(lr, baseColor);

        // Initial position — Update() will correct it next frame; set something sane now.
        float cx = hazard.position.x;
        float cz = hazard.position.z;
        lr.SetPosition(0, new Vector3(cx - halfWidth, planeY, cz));
        lr.SetPosition(1, new Vector3(cx + halfWidth, planeY, cz));

        // ── First-encounter name label ───────────────────────────────────────
        TextMesh nameMesh = null;
        var tele = Game.I != null ? Game.I.Tele : _fallbackTele;
        if (tele != null && tele.ShouldAnnounce(info.TechniqueId))
            nameMesh = BuildNameLabel(info.DisplayName, baseColor);

        _active.Add(new TelEntry
        {
            hazard    = hazard,
            line      = lr,
            planeY    = planeY,
            halfWidth = halfWidth,
            baseColor = baseColor,
            name      = nameMesh,
        });
    }

    /// <summary>Recycle all active telegraphs. Call on game restart.</summary>
    public void ClearAll()
    {
        foreach (var e in _active)
        {
            Recycle(e.line);
            if (e.name != null) Destroy(e.name.gameObject);
        }
        _active.Clear();
    }

    // ── Pool helpers ─────────────────────────────────────────────────────────

    LineRenderer AcquireLine()
    {
        while (_pool.Count > 0)
        {
            var lr = _pool.Pop();
            if (lr != null) { lr.gameObject.SetActive(true); return lr; }
        }
        // Build a new one.
        var go = new GameObject("Tel_Line");
        go.transform.SetParent(transform, false);
        var r = go.AddComponent<LineRenderer>();
        r.positionCount   = 2;
        r.useWorldSpace   = true;
        r.numCapVertices  = 4;      // soft round caps
        r.alignment       = LineAlignment.View;
        r.textureMode     = LineTextureMode.Stretch; // stretch brush-stroke texture along the line
        r.sharedMaterial  = _lineMat;
        return r;
    }

    void Recycle(LineRenderer lr)
    {
        if (lr == null) return;
        lr.gameObject.SetActive(false);
        _pool.Push(lr);
    }

    void SetupLine(LineRenderer lr, Color baseColor)
    {
        // Start fully dim — Update() will ramp it up.
        var dim = new Color(baseColor.r, baseColor.g, baseColor.b, ALPHA_MIN);
        lr.startColor = dim;
        lr.endColor   = dim;
        lr.startWidth = WIDTH_MIN;
        lr.endWidth   = WIDTH_MIN * 0.55f;
        lr.sharedMaterial = _lineMat;
    }

    // ── Name label builder ───────────────────────────────────────────────────

    /// <summary>
    /// Creates a world-space TextMesh showing the technique name with guillemets.
    /// Called at most once per techniqueId (first-encounter gate enforced by Game.I.Tele).
    /// Names are rare — no pooling needed; plain Destroy on recycle is fine.
    /// </summary>
    TextMesh BuildNameLabel(string displayName, Color planeColor)
    {
        var go = new GameObject("Tel_Name");
        go.transform.SetParent(transform, false);

        var tm = go.AddComponent<TextMesh>();
        // ponytail: TextMesh sizing is finicky + no SDF crispness — brush/seal font is the art pass.
        tm.text          = "« " + displayName + " »"; // « name »
        tm.fontSize      = 64;
        tm.characterSize = 0.08f;
        tm.anchor        = TextAnchor.LowerCenter;
        tm.alignment     = TextAlignment.Center;

        // Start fully transparent; Update() fades it in with wind-up t.
        var c  = planeColor;
        c.a    = 0f;
        tm.color = c;

        // Use InkArt serif font for name labels (art pass).
        var font = InkArt.Serif();
        if (font != null)
        {
            tm.font = font;
            // REQUIRED: assign the font's material or the mesh renders invisible.
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }
        // If font lookup fails the MeshRenderer's default material is used — readable but unstyled.

        return tm;
    }

    // ── Palette / plane lookups ──────────────────────────────────────────────

    static Color PaletteFor(TelegraphColor tc) => tc switch
    {
        TelegraphColor.Amber => PaletteAmber,
        TelegraphColor.Cyan  => PaletteCyan,
        TelegraphColor.Red   => PaletteRed,
        TelegraphColor.White => PaletteWhite,
        _                    => Color.white,
    };

    static float PlaneHeight(AttackPlane plane) => plane switch
    {
        AttackPlane.Low          => Y_LOW,
        AttackPlane.High         => Y_HIGH,
        AttackPlane.Destructible => Y_DESTRUCTIBLE,
        AttackPlane.Lane         => Y_LANE,
        _                        => Y_LOW,
    };
}
