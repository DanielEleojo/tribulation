// NetOverlay.cs — Heavenly Net fullscreen screen-space overlay (UI-5).
// Code-built Screen-Space-Overlay Canvas, no prefabs, no external assets.
// Renders the "Tribulation/HeavenlyNet" ShaderLab shader over a fullscreen
// UI Image, driven by the core's NetChanged event (0 = clear, 1 = closes in).
//
// Canvas sortingOrder = 5  →  above the 3D world, BELOW HudOverlay (order 10).
//
// ponytail: fullscreen fragment shader — known mobile fill-rate cost; optimize
//   (e.g. half-res RenderTexture blit) when tackling iOS perf issue #1.

using UnityEngine;
using UnityEngine.UI;

public class NetOverlay : MonoBehaviour
{
    public static NetOverlay I;

    // ── Runtime refs ────────────────────────────────────────────────────────
    Material _mat;
    int      _cachedW;
    int      _cachedH;

    // ════════════════════════════════════════════════════════════════════════
    // Lifecycle
    // ════════════════════════════════════════════════════════════════════════
    void Awake() { I = this; }

    void Start()
    {
        BuildCanvas();
        SubscribeCoreEvents();
        OnNetChanged(0f);                  // initialise to fully clear
        _cachedW = Screen.width;
        _cachedH = Screen.height;
        if (_mat != null)
            _mat.SetFloat("_Aspect", (float)_cachedW / _cachedH);
    }

    // ── Canvas construction ──────────────────────────────────────────────────
    void BuildCanvas()
    {
        // Root Canvas — Screen Space Overlay, order BELOW HudOverlay (10)
        var canvasGO = new GameObject("NetCanvas");
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;           // HudOverlay = 10; we sit beneath it

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(810f, 1440f);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0f; // match width — portrait-locked game

        // No GraphicRaycaster needed — overlay is pure visual.

        // ── Shader material ──────────────────────────────────────────────────
        var shader = Shader.Find("Tribulation/HeavenlyNet");
        if (shader == null)
        {
            Debug.LogError("[NetOverlay] Shader 'Tribulation/HeavenlyNet' not found. " +
                           "Ensure HeavenlyNet.shader is in the project.");
            return;
        }
        _mat = new Material(shader) { name = "NetOverlay_Mat" };

        // ── Fullscreen Image ─────────────────────────────────────────────────
        var imgGO = new GameObject("NetImage", typeof(RectTransform));
        imgGO.transform.SetParent(canvasGO.transform, false);

        var rt = imgGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = imgGO.AddComponent<Image>();
        img.material      = _mat;
        img.color         = Color.white;   // shader drives the actual colour
        img.raycastTarget = false;
    }

    // ── Event wiring — mirrors HudOverlay.SubscribeCoreEvents() exactly ─────
    void SubscribeCoreEvents()
    {
        if (Game.I?.Core == null) return;
        Game.I.Core.NetChanged += OnNetChanged;
    }

    void UnsubscribeCoreEvents()
    {
        if (Game.I?.Core == null) return;
        Game.I.Core.NetChanged -= OnNetChanged;
    }

    // ── NetChanged handler ───────────────────────────────────────────────────
    void OnNetChanged(float net)
    {
        if (_mat != null)
            _mat.SetFloat("_Net", net);
    }

    // ── Per-frame: cheap aspect-ratio drift check ────────────────────────────
    void Update()
    {
        if (_mat == null) return;
        if (Screen.width != _cachedW || Screen.height != _cachedH)
        {
            _cachedW = Screen.width;
            _cachedH = Screen.height;
            _mat.SetFloat("_Aspect", (float)_cachedW / _cachedH);
        }
    }

    // ── Lifecycle cleanup ────────────────────────────────────────────────────
    void OnDestroy()
    {
        UnsubscribeCoreEvents();
        if (_mat != null) Destroy(_mat);
    }
}
