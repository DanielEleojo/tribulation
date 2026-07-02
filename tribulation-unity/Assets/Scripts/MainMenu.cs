// MainMenu.cs — Title screen / main menu (UI-5, tracer slice)
// Code-built uGUI, Screen-Space-Overlay Canvas (sortingOrder 20, above HUD's 10).
// Shows on launch; hidden when the player taps "Begin Cultivation."
//
// Faithful port of Godot hud.gd _build_menu / _on_begin.
//
// ponytail: parchment textures, brush fonts, wordmark art = art pass
// note: load-order — if Game.Start's LoadSave runs after this, realm/best may show
//           defaults for one display; refresh on Show() is enough.
// ponytail: shop/journal/settings slices TODO (ghost buttons disabled here)
// note: daily reward is surfaced in the Journal panel (MenuScreens.cs — issue #13)

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MainMenu : MonoBehaviour
{
    // ── Palette (Ink & Talisman) ─────────────────────────────────────────────
    static readonly Color C_PARCHMENT = HexCol("#f2e8d0");
    static readonly Color C_INK       = HexCol("#1a1008");
    static readonly Color C_JADE      = HexCol("#2a7c6f");
    static readonly Color C_CINNABAR  = HexCol("#c0392b");
    static readonly Color C_GOLD      = HexCol("#b8860b");
    static readonly Color C_TEXT_DIM  = HexCol("#6b4e2a");
    static readonly Color C_BACKDROP  = new Color(0.03f, 0.04f, 0.06f, 0.92f);

    // ── Realm data (mirrors HudOverlay.cs) ──────────────────────────────────
    static readonly string[] RealmNames =
    {
        "Qi Condensation", "Foundation Establishment", "Golden Core",
        "Nascent Soul", "Spirit Severing", "Ascension"
    };

    // ── Singleton ────────────────────────────────────────────────────────────
    public static MainMenu I;

    // ── Root ─────────────────────────────────────────────────────────────────
    GameObject _menuRoot;   // SetActive(false) to hide everything

    /// <summary>True while the main menu is visible (not yet in an active run).</summary>
    public bool IsVisible => _menuRoot != null && _menuRoot.activeSelf;

    // ── Text refs for live data ──────────────────────────────────────────────
    Text _realmLine;
    Text _bestLine;

    // ════════════════════════════════════════════════════════════════════════
    // Lifecycle
    // ════════════════════════════════════════════════════════════════════════
    void Awake()
    {
        I = this;
    }

    void Start()
    {
        EnsureEventSystem();
        BuildCanvas();
        RefreshLiveTexts();
        // _menuRoot is active by default — shown at launch.
    }

    // ── EventSystem guard ────────────────────────────────────────────────────
    // Required for Button clicks to register. Bootstrap doesn't add one, so we
    // create it here if absent. Works because Project Input Handling = Both.
    static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }

    // ════════════════════════════════════════════════════════════════════════
    // Canvas build
    // ════════════════════════════════════════════════════════════════════════
    void BuildCanvas()
    {
        // Root Canvas — Screen Space Overlay, sorts above HUD (10) at 20
        var canvasGO = new GameObject("MainMenuCanvas");
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(810f, 1440f);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0f; // match width — portrait-locked game

        // GraphicRaycaster needed so Button clicks register
        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Ink & Talisman art-pass fonts ───────────────────────────────────
        Font font     = InkArt.Serif(); // elegant Latin serif for all UI text
        Font sealFont = InkArt.Seal();  // traditional-Chinese subset (23 glyphs)

        // ── Menu root (dim backdrop + centred card) ──────────────────────────
        _menuRoot = new GameObject("MenuRoot", typeof(RectTransform));
        _menuRoot.transform.SetParent(canvasGO.transform, false);
        var rootRt = _menuRoot.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        // Full-screen dim backdrop (keep dark ink fill)
        var backdrop = MakeImage(_menuRoot, "Backdrop", C_BACKDROP,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var bdRt = backdrop.GetComponent<RectTransform>();
        bdRt.anchorMin = Vector2.zero;
        bdRt.anchorMax = Vector2.one;
        bdRt.offsetMin = Vector2.zero;
        bdRt.offsetMax = Vector2.zero;
        backdrop.raycastTarget = true; // intercept taps behind menu

        // ── Centred card — parchment scroll panel ────────────────────────────
        // RoundedPanel gives the card rounded corners + ink border.
        // Color set to white so the sprite's own parchment tones show through.
        // 700x1150 offset down 125 so the TOP edge sits where the old 900-tall card's
        // did, while the extra height extends the parchment toward the bottom of the
        // screen — the buttons live down there, in one-handed thumb reach.
        var card = MakeImage(_menuRoot, "MenuCard", Color.white,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -125f), new Vector2(700f, 1150f),
            new Vector2(0.5f, 0.5f));
        card.sprite = InkArt.RoundedPanel(700, 1150, 20, 3);
        card.type   = Image.Type.Simple;
        var cardGO = card.gameObject;

        // ── Seal accent 渡劫 above wordmark (the wuxia name for "crossing tribulation") ──
        // Title block fills the card's upper two-thirds (-60..-620) with generous
        // line spacing — the buttons live in the bottom quarter (thumb reach), and
        // this rhythm keeps the parchment between them from reading as dead space.
        // Cinnabar seal glyphs; large, centered, above the English wordmark.
        var sealAccent = MakeText(cardGO, "SealAccent", sealFont, 80, InkArt.Cinnabar,
            TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -60f), new Vector2(660f, 100f));
        sealAccent.text = "渡劫";

        // ── Wordmark "TRIBULATION" — serif bold, Gold, outlined ─────────────
        var wordmark = MakeText(cardGO, "Wordmark", font, 88, InkArt.Gold,
            TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -185f), new Vector2(680f, 110f));
        wordmark.text      = "TRIBULATION";
        wordmark.fontStyle = FontStyle.Bold;
        InkArt.AddOutline(wordmark, 1f);

        // ── Subtitle "The Cultivator's Road" — serif Jade + 道 seal accent ──
        var subtitle = MakeText(cardGO, "Subtitle", font, 44, InkArt.Jade,
            TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(-16f, -340f), new Vector2(640f, 60f));
        subtitle.text = "The Cultivator's Road";

        // Small 道 seal accent to the right of subtitle text.
        var subtitleSeal = MakeText(cardGO, "SubtitleSeal", sealFont, 40, InkArt.Jade,
            TextAnchor.UpperLeft,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(272f, -336f), new Vector2(52f, 56f));
        subtitleSeal.text = "道";

        // ── Live data: realm + best ──────────────────────────────────────────
        // Hierarchy kept: wordmark 88 > seal 80 > subtitle 44 > realm 36 > best 34.
        // Realm stays 36: "Foundation Establishment · 1st Layer" (the longest realm
        // string) must fit ONE line in 680 — at 42 it wraps into the Best line.
        _realmLine = MakeText(cardGO, "RealmLine", font, 36, InkArt.Gold,
            TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -470f), new Vector2(680f, 50f));

        _bestLine = MakeText(cardGO, "BestLine", font, 34, InkArt.TextDim,
            TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -560f), new Vector2(660f, 46f));

        // ── Begin Cultivation (primary button) ───────────────────────────────
        // RoundedPanel parchment background with ink border; Ink label reads well on parchment.
        // HIG sizing: 100 units tall (> 44pt min touch target), 36-unit label (~17pt body).
        // Spans -730..-830 from card top — below the Best line with breathing room,
        // in one-handed thumb reach (right about where the runner stands in-game).
        var beginBtn = MakeButton(cardGO, "BeginBtn",
            "Begin Cultivation",
            font, 36, InkArt.Ink,
            Color.white,        // color multiplied by sprite; white = show sprite as-is
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -730f), new Vector2(440f, 100f),
            new Vector2(0.5f, 1f),
            beginSprite: InkArt.RoundedPanel(440, 100, 14, 2));
        beginBtn.onClick.AddListener(() =>
        {
            Game.I?.BeginRun();
            _menuRoot.SetActive(false);
        });

        // ── Secondary (ghost) buttons row ───────────────────────────────────
        // ponytail: shop/journal/settings slices TODO
        // These are disabled/dimmed until those screens are built.
        // HIG sizing: 92 units tall (44pt min touch target), 31-unit labels (~15pt).
        // Row fit in the 700-wide card: 3×200 + 2×25 gaps = 650, leaving a 25-unit
        // margin each side. Row spans -860..-952; BeginBtn ends at -830 (30-unit gap),
        // parchment breathing room below before the card bottom at -1150.
        const float GHOST_Y    = -860f;
        const float GHOST_W    = 200f;
        const float GHOST_H    = 92f;
        const float GHOST_GAP  = 25f;
        float[] offsets = { -(GHOST_W + GHOST_GAP), 0f, GHOST_W + GHOST_GAP };
        string[] labels = { "Cultivation", "Journal", "Settings" };

        for (int i = 0; i < 3; i++)
        {
            var ghostSprite = InkArt.RoundedPanel((int)GHOST_W, (int)GHOST_H, 10, 2);
            var ghostBtn = MakeButton(cardGO, labels[i] + "Btn",
                labels[i],
                font, 31, InkArt.TextDim,
                new Color(InkArt.ParchmentDark.r, InkArt.ParchmentDark.g, InkArt.ParchmentDark.b, 0.75f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(offsets[i], GHOST_Y), new Vector2(GHOST_W, GHOST_H),
                new Vector2(0.5f, 1f),
                interactable: true,
                beginSprite: ghostSprite);
            // Full-opacity label so the buttons read clearly as active
            var txt = ghostBtn.gameObject.GetComponentInChildren<Text>();
            if (txt != null) txt.color = InkArt.TextDim;

            // Wire onClick — guarded with ?. so order vs. MenuScreens.Start() doesn't matter
            int idx = i;
            ghostBtn.onClick.AddListener(() =>
            {
                if (idx == 0) MenuScreens.I?.OpenShop();
                else if (idx == 1) MenuScreens.I?.OpenJournal();
                else               MenuScreens.I?.OpenSettings();
            });
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // Public API
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>Show the main menu (e.g. called by PauseMenu's Quit to Menu).</summary>
    public void Show()
    {
        if (_menuRoot != null) _menuRoot.SetActive(true);
        RefreshLiveTexts();
    }

    // ════════════════════════════════════════════════════════════════════════
    // Live data refresh
    // ════════════════════════════════════════════════════════════════════════
    void RefreshLiveTexts()
    {
        if (_realmLine == null || _bestLine == null) return;

        var core = Game.I?.Core;
        if (core == null)
        {
            _realmLine.text = RealmNames[0] + " · " + LayerStr(1);
            _bestLine.text  = "A long road awaits";
            return;
        }

        int realmIdx = Mathf.Clamp(core.Realm, 0, RealmNames.Length - 1);
        _realmLine.text = RealmNames[realmIdx] + " · " + LayerStr(core.MinorLevel());
        _bestLine.text  = core.BestLi > 0
            ? "Best:  " + core.BestLi + " li"
            : "A long road awaits";
    }

    // ════════════════════════════════════════════════════════════════════════
    // Realm helpers (mirrors HudOverlay.cs — ponytail: consolidate if desired)
    // ════════════════════════════════════════════════════════════════════════
    static string LayerStr(int n)
    {
        if (n >= 10) return "Great Perfection";
        string suffix = n == 1 ? "st" : n == 2 ? "nd" : n == 3 ? "rd" : "th";
        return n + suffix + " Layer";
    }

    // ════════════════════════════════════════════════════════════════════════
    // uGUI Helpers (mirrors HudOverlay.cs style)
    // ════════════════════════════════════════════════════════════════════════

    static Button MakeButton(GameObject parent, string name,
        string label, Font font, int fontSize, Color textColor,
        Color bgColor,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPos, Vector2 sizeDelta,
        Vector2 pivot,
        bool interactable = true,
        Sprite beginSprite = null)
    {
        // Button root
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.pivot            = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = sizeDelta;

        // Background image — use sprite if provided, otherwise flat color.
        var img = go.AddComponent<Image>();
        img.color = bgColor;
        if (beginSprite != null)
        {
            img.sprite = beginSprite;
            img.type   = Image.Type.Simple;
        }
        img.raycastTarget = true;

        // Button component
        var btn = go.AddComponent<Button>();
        btn.interactable = interactable;
        btn.targetGraphic = img;

        // Color block — dim all states when disabled.
        // Use white for normal/highlight/pressed when a sprite is supplied so its
        // own colors show through; tint slightly on interaction.
        Color baseCol = (beginSprite != null) ? Color.white : bgColor;
        var cb = btn.colors;
        cb.normalColor      = baseCol;
        cb.highlightedColor = Color.Lerp(baseCol, Color.white, 0.12f);
        cb.pressedColor     = Color.Lerp(baseCol, Color.black, 0.12f);
        cb.disabledColor    = new Color(baseCol.r, baseCol.g, baseCol.b, 0.45f);
        cb.colorMultiplier  = 1f;
        btn.colors = cb;

        // Label child
        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(go.transform, false);
        var lrt = labelGO.GetComponent<RectTransform>();
        lrt.anchorMin        = Vector2.zero;
        lrt.anchorMax        = Vector2.one;
        lrt.offsetMin        = Vector2.zero;
        lrt.offsetMax        = Vector2.zero;

        var txt = labelGO.AddComponent<Text>();
        txt.font      = font;
        txt.fontSize  = fontSize;
        txt.color     = textColor;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.supportRichText = false;
        txt.raycastTarget   = false;
        txt.text = label;

        return btn;
    }

    static Text MakeText(GameObject parent, string name, Font font,
        int fontSize, Color color, TextAnchor alignment,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.pivot            = new Vector2(anchorMin.x, anchorMax.y);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = sizeDelta;

        var t = go.AddComponent<Text>();
        t.font      = font;
        t.fontSize  = fontSize;
        t.color     = color;
        t.alignment = alignment;
        t.supportRichText = false;
        t.raycastTarget   = false;
        // All menu texts are single-line in fixed slots; the serif line height can
        // exceed a tight slot (e.g. wordmark 72pt in 90u) and default Truncate then
        // drops the whole line — Overflow keeps them rendering.
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    static Image MakeImage(GameObject parent, string name, Color color,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPos, Vector2 sizeDelta,
        Vector2? pivot = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.pivot            = pivot ?? anchorMin;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = sizeDelta;

        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static Color HexCol(string hex)
    {
        hex = hex.TrimStart('#');
        float r = System.Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
        float g = System.Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
        float b = System.Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;
        return new Color(r, g, b, 1f);
    }
}
