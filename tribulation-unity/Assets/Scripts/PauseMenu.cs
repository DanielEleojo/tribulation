// PauseMenu.cs — In-run pause screen (Ink & Talisman style).
// Code-built uGUI, Screen-Space-Overlay Canvas (sortingOrder 23, above MenuScreens' 22).
// Singleton MonoBehaviour; Bootstrap adds it after MenuScreens.
//
// Features:
//   • Pause trigger button — top-right corner, visible only during an active run.
//   • Pause panel — dim backdrop + centred parchment card with VerticalLayoutGroup,
//       leading-spacer trick so the "Paused" title actually renders (first VLG child
//       is an invisible spacer, title is the second child).
//   • Resume / Restart / Quit to Menu buttons (full-width, cinnabar tint on parchment).
//   • Time.timeScale = 0 while paused; uGUI click handlers fire at timeScale 0 — safe.
//   • Keyboard: Escape / P toggles pause/resume.
//   • All singleton refs (Game.I, Core, MainMenu.I, HudOverlay.I) null-checked.

using UnityEngine;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    // ── Singleton ────────────────────────────────────────────────────────────
    public static PauseMenu I { get; private set; }

    // ── Palette (exact match with MenuScreens / MainMenu) ───────────────────
    static readonly Color C_PARCHMENT = HexCol("#f2e8d0");
    static readonly Color C_INK       = HexCol("#1a1008");
    static readonly Color C_JADE      = HexCol("#2a7c6f");
    static readonly Color C_CINNABAR  = HexCol("#c0392b");
    static readonly Color C_GOLD      = HexCol("#b8860b");
    static readonly Color C_TEXT_DIM  = HexCol("#6b4e2a");
    static readonly Color C_BACKDROP  = new Color(0.03f, 0.04f, 0.06f, 0.86f);

    // ── UI refs ──────────────────────────────────────────────────────────────
    GameObject _triggerBtn;   // small "II" button in top-right, shown during a run
    GameObject _pausePanel;   // full-screen dim backdrop + card (hidden by default)

    bool _paused;

    /// <summary>True while the game is paused (Time.timeScale=0 and panel visible).</summary>
    public bool IsPaused => _paused;

    // ════════════════════════════════════════════════════════════════════════
    // Lifecycle
    // ════════════════════════════════════════════════════════════════════════
    void Awake()
    {
        I = this;
    }

    void Start()
    {
        Font font = InkArt.Serif();

        // ── Canvas (sortingOrder 23 — above MenuScreens' 22) ────────────────
        var canvasGO = new GameObject("PauseMenuCanvas");
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 23;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();
        // NOTE: EventSystem already exists (created by MainMenu) — no second one added.

        // ── Build the two pieces ─────────────────────────────────────────────
        _triggerBtn = BuildTriggerButton(canvasGO, font);
        _pausePanel = BuildPausePanel(canvasGO, font);

        // Initial state — trigger hidden, panel hidden.
        _triggerBtn.SetActive(false);
        _pausePanel .SetActive(false);
    }

    // Per-frame: manage trigger-button visibility; handle keyboard shortcuts.
    // Because Time.timeScale may be 0 while paused, we use unscaled input only.
    // Button clicks wire through uGUI which fires at any timeScale — safe.
    void Update()
    {
        // Keyboard shortcuts (work at timeScale 0 because Input polls unscaled).
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
        {
            if (_paused)
                Resume();
            else
                Pause();
        }

        // Trigger button visibility: show only during an active, non-dead, non-paused run.
        if (_triggerBtn != null)
        {
            bool runActive = IsRunActive();
            bool show = runActive && !_paused;
            if (_triggerBtn.activeSelf != show)
                _triggerBtn.SetActive(show);
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // Public API
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>Pause the run. Only acts if a run is active and not already dead/paused.</summary>
    public void Pause()
    {
        if (_paused) return;
        if (!IsRunActive()) return;

        _paused = true;
        Time.timeScale = 0f;

        if (_triggerBtn != null) _triggerBtn.SetActive(false);
        if (_pausePanel  != null) _pausePanel .SetActive(true);
    }

    /// <summary>Resume the run after pause.</summary>
    public void Resume()
    {
        if (!_paused) return;

        _paused = false;
        Time.timeScale = 1f;

        if (_pausePanel != null) _pausePanel.SetActive(false);
        // Trigger button visibility is handled each frame in Update().
    }

    /// <summary>Restart the current run (mirrors GameLoop restart path).</summary>
    public void Restart()
    {
        _paused = false;
        Time.timeScale = 1f;

        if (_pausePanel != null) _pausePanel.SetActive(false);

        DoRestartSequence();
    }

    /// <summary>Quit to the main menu. Resets the run state behind the menu.</summary>
    public void QuitToMenu()
    {
        _paused = false;
        Time.timeScale = 1f;

        if (_pausePanel != null) _pausePanel.SetActive(false);

        // Reset run state so nothing odd keeps running behind the menu.
        DoRestartSequence();

        // Show the main menu on top.
        if (MainMenu.I != null)
            MainMenu.I.Show();
    }

    // ════════════════════════════════════════════════════════════════════════
    // Helpers
    // ════════════════════════════════════════════════════════════════════════

    // True only when a real run is ongoing and the player has not died.
    bool IsRunActive()
    {
        var core = Game.I?.Core;
        if (core == null) return false;
        return core.IsStarted && !core.IsDead;
    }

    // Shared restart sequence (used by Restart() and QuitToMenu()).
    void DoRestartSequence()
    {
        HudOverlay.I?.HideDeathCard();
        Game.I?.RestartRun();
        var pr = FindObjectOfType<PlayerRunner>();
        if (pr != null) pr.ResetRun();
    }

    // ════════════════════════════════════════════════════════════════════════
    // UI builders
    // ════════════════════════════════════════════════════════════════════════

    // ── Trigger button ───────────────────────────────────────────────────────
    // Small "II" (two pipes, ascii) anchored to the top-right corner.
    // Visible only while a run is active and not paused.
    GameObject BuildTriggerButton(GameObject canvasGO, Font font)
    {
        var go = new GameObject("PauseTrigger", typeof(RectTransform));
        go.transform.SetParent(canvasGO.transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(1f, 1f);
        rt.anchorMax        = new Vector2(1f, 1f);
        rt.pivot            = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-28f, -60f); // ~28px from right, 60px from top
        rt.sizeDelta        = new Vector2(88f, 72f);

        var img = go.AddComponent<Image>();
        img.sprite        = InkArt.RoundedPanel(88, 72, 10, 2);
        img.type          = Image.Type.Simple;
        img.color         = new Color(C_PARCHMENT.r, C_PARCHMENT.g, C_PARCHMENT.b, 0.88f);
        img.raycastTarget = true;

        var btn = go.AddComponent<Button>();
        btn.interactable  = true;
        btn.targetGraphic = img;
        {
            var cb = btn.colors;
            cb.normalColor      = new Color(C_PARCHMENT.r, C_PARCHMENT.g, C_PARCHMENT.b, 0.88f);
            cb.highlightedColor = Color.white;
            cb.pressedColor     = new Color(0.85f, 0.85f, 0.85f, 1f);
            cb.disabledColor    = new Color(1f, 1f, 1f, 0.4f);
            cb.colorMultiplier  = 1f;
            btn.colors = cb;
        }
        btn.onClick.AddListener(() => Pause());

        // Label — "II" (two ascii pipe characters) reads as a pause icon.
        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(go.transform, false);
        var lrt = labelGO.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;

        var lbl = labelGO.AddComponent<Text>();
        lbl.font            = font;
        lbl.fontSize        = 32;
        lbl.color           = C_INK;
        lbl.alignment       = TextAnchor.MiddleCenter;
        lbl.fontStyle       = FontStyle.Bold;
        lbl.supportRichText = false;
        lbl.raycastTarget   = false;
        lbl.text = "II";

        return go;
    }

    // ── Pause panel ──────────────────────────────────────────────────────────
    // Full-screen dim backdrop + centred 560x620 parchment card.
    // Content: [spacer(first-child, invisible)], "Paused" title, flexible spacer,
    //          Resume, Restart, Quit to Menu buttons, bottom padding spacer.
    GameObject BuildPausePanel(GameObject canvasGO, Font font)
    {
        // Full-screen backdrop (raycast-blocking so taps don't pass through).
        var panel = new GameObject("PausePanel", typeof(RectTransform));
        panel.transform.SetParent(canvasGO.transform, false);
        var panelRT = panel.GetComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        var backdropImg = panel.AddComponent<Image>();
        backdropImg.color         = C_BACKDROP;
        backdropImg.raycastTarget = true;

        // ── Centred parchment card ───────────────────────────────────────────
        const int CARD_W = 560;
        const int CARD_H = 620;

        var cardGO = new GameObject("Card", typeof(RectTransform));
        cardGO.transform.SetParent(panel.transform, false);
        var cardRT = cardGO.GetComponent<RectTransform>();
        cardRT.anchorMin        = new Vector2(0.5f, 0.5f);
        cardRT.anchorMax        = new Vector2(0.5f, 0.5f);
        cardRT.pivot            = new Vector2(0.5f, 0.5f);
        cardRT.anchoredPosition = Vector2.zero;
        cardRT.sizeDelta        = new Vector2(CARD_W, CARD_H);

        var cardImg = cardGO.AddComponent<Image>();
        cardImg.sprite        = InkArt.RoundedPanel(CARD_W, CARD_H, 20, 3);
        cardImg.type          = Image.Type.Simple;
        cardImg.color         = Color.white;
        cardImg.raycastTarget = true;

        // ── Content container (VerticalLayoutGroup) ──────────────────────────
        var content = new GameObject("PauseContent", typeof(RectTransform));
        content.transform.SetParent(cardGO.transform, false);
        var contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin = Vector2.zero;
        contentRT.anchorMax = Vector2.one;
        contentRT.offsetMin = Vector2.zero;
        contentRT.offsetMax = Vector2.zero;

        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.padding               = new RectOffset(36, 36, 36, 36);
        vlg.spacing               = 16f;
        vlg.childAlignment        = TextAnchor.UpperCenter;
        vlg.childControlWidth     = true;
        vlg.childControlHeight    = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        // ── CHILD 0: Leading spacer (invisible) ──────────────────────────────
        // KNOWN QUIRK: the first child of a VLG fails to render in this project.
        // We make the first child an invisible 8px spacer so the title (child 1) renders.
        {
            var spacerGO = new GameObject("LeadingSpacer", typeof(RectTransform));
            spacerGO.transform.SetParent(content.transform, false);
            var le = spacerGO.AddComponent<LayoutElement>();
            le.preferredHeight = 8f;
            le.minHeight       = 8f;
            // Image with alpha 0 — invisible, still occupies layout space.
            var spacerImg = spacerGO.AddComponent<Image>();
            spacerImg.color         = new Color(0f, 0f, 0f, 0f);
            spacerImg.raycastTarget = false;
        }

        // ── CHILD 1: "Paused" title ──────────────────────────────────────────
        {
            var go = new GameObject("PausedTitle", typeof(RectTransform));
            go.transform.SetParent(content.transform, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 72f;
            le.minHeight       = 72f;

            var t = go.AddComponent<Text>();
            t.font            = font;
            t.fontSize        = 56;
            t.color           = C_CINNABAR;
            t.alignment       = TextAnchor.MiddleCenter;
            t.fontStyle       = FontStyle.Bold;
            t.supportRichText = false;
            t.raycastTarget   = false;
            t.text = "Paused";
            InkArt.AddOutline(t, 1f);
        }

        // ── Flexible spacer between title and buttons ────────────────────────
        AddFlexSpacer(content, "MidSpacer");

        // ── Buttons ──────────────────────────────────────────────────────────
        AddPauseButton(content, font, "Resume",       C_JADE,     () => Resume());
        AddPauseButton(content, font, "Restart",      C_CINNABAR, () => Restart());
        AddPauseButton(content, font, "Quit to Menu", C_INK,      () => QuitToMenu());

        // ── Bottom padding spacer ────────────────────────────────────────────
        AddFlexSpacer(content, "BottomSpacer");

        return panel;
    }

    // ── Layout helpers ───────────────────────────────────────────────────────

    static void AddFlexSpacer(GameObject container, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(container.transform, false);
        var le = go.AddComponent<LayoutElement>();
        le.flexibleHeight = 1f;
        le.minHeight      = 0f;
    }

    // Full-width button in the VLG, 68px tall, parchment card, coloured label.
    static void AddPauseButton(GameObject container, Font font,
        string label, Color labelColor, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(label + "Btn", typeof(RectTransform));
        go.transform.SetParent(container.transform, false);

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 68f;
        le.minHeight       = 68f;

        var img = go.AddComponent<Image>();
        img.sprite        = InkArt.RoundedPanel(488, 68, 12, 2);
        img.type          = Image.Type.Simple;
        img.color         = Color.white;
        img.raycastTarget = true;

        var btn = go.AddComponent<Button>();
        btn.interactable  = true;
        btn.targetGraphic = img;
        {
            var cb = btn.colors;
            cb.normalColor      = Color.white;
            cb.highlightedColor = new Color(0.95f, 0.95f, 0.95f, 1f);
            cb.pressedColor     = new Color(0.85f, 0.85f, 0.85f, 1f);
            cb.disabledColor    = new Color(1f, 1f, 1f, 0.45f);
            cb.colorMultiplier  = 1f;
            btn.colors = cb;
        }
        btn.onClick.AddListener(onClick);

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(go.transform, false);
        var lrt = labelGO.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;

        var lbl = labelGO.AddComponent<Text>();
        lbl.font            = font;
        lbl.fontSize        = 30;
        lbl.color           = labelColor;
        lbl.alignment       = TextAnchor.MiddleCenter;
        lbl.fontStyle       = FontStyle.Bold;
        lbl.supportRichText = false;
        lbl.raycastTarget   = false;
        lbl.text = label;
    }

    // ── Colour helper ────────────────────────────────────────────────────────
    static Color HexCol(string hex)
    {
        hex = hex.TrimStart('#');
        float r = System.Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
        float g = System.Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
        float b = System.Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;
        return new Color(r, g, b, 1f);
    }
}
