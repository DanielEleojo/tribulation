// MenuScreens.cs — Cultivation Shop, Journal, and Settings overlay panels.
// Singleton MonoBehaviour; Bootstrap adds it right after MainMenu so
// MenuScreens.I is live when MainMenu wires its ghost-button onClick callbacks.
//
// Canvas: Screen-Space-Overlay, sortingOrder 22 (above MainMenu's 20).
// Three hidden panels — Shop / Journal / Settings — each with:
//   • full-screen dim backdrop (raycast-blocking)
//   • centred parchment card (InkArt.RoundedPanel, white tint)
//   • VerticalLayoutGroup stacking all content top-to-bottom (no hand-anchored positions)
// All panels start inactive; opening one closes the others.
//
// Code-built uGUI only — legacy UI.Text, no TMP / prefabs / external assets.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MenuScreens : MonoBehaviour
{
    // ── Singleton ────────────────────────────────────────────────────────────
    public static MenuScreens I { get; private set; }

    // ── Palette (exact copy from MainMenu so screens match visually) ─────────
    static readonly Color C_PARCHMENT = HexCol("#f2e8d0");
    static readonly Color C_INK       = HexCol("#1a1008");
    static readonly Color C_JADE      = HexCol("#2a7c6f");
    static readonly Color C_CINNABAR  = HexCol("#c0392b");
    static readonly Color C_GOLD      = HexCol("#b8860b");
    static readonly Color C_TEXT_DIM  = HexCol("#6b4e2a");
    static readonly Color C_BACKDROP  = new Color(0.03f, 0.04f, 0.06f, 0.92f);

    // ── Realm names (mirrors MainMenu / HudOverlay) ──────────────────────────
    static readonly string[] RealmNames =
    {
        "Qi Condensation", "Foundation Establishment", "Golden Core",
        "Nascent Soul", "Spirit Severing", "Ascension"
    };

    // ── Panel root GameObjects ────────────────────────────────────────────────
    GameObject _shopPanel;
    GameObject _journalPanel;
    GameObject _settingsPanel;

    // ── Shop live refs ────────────────────────────────────────────────────────
    Text   _shopStonesText;
    // Per-upgrade row: [i] = (levelText, buyButton, buyLabel)
    struct UpgradeRow { public Text levelText; public Button buyBtn; public Text buyLabel; }
    UpgradeRow[] _upgradeRows;

    // ── Journal live refs ─────────────────────────────────────────────────────
    Text _journalStatsText;
    Text _journalTechText;
    Text _journalAchText;
    // Daily claim button (lives in the journal panel)
    Button _dailyBtn;
    Text   _dailyBtnLabel;

    // ── Settings live refs ────────────────────────────────────────────────────
    Slider _musicSlider;
    Slider _sfxSlider;
    Toggle _muteToggle;
    // Reset-cultivation confirm gate: the two rows swap visibility on tap.
    GameObject _resetNormalRow;
    GameObject _resetConfirmRow;

    // ════════════════════════════════════════════════════════════════════════
    // Lifecycle
    // ════════════════════════════════════════════════════════════════════════
    void Awake()
    {
        I = this;
    }

    void Start()
    {
        Font font     = InkArt.Serif();
        Font sealFont = InkArt.Seal();

        // ── Canvas (sortingOrder 22 — above MainMenu's 20) ───────────────────
        var canvasGO = new GameObject("MenuScreensCanvas");
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 22;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(810f, 1440f);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0f; // match width — portrait-locked game

        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Build each panel ─────────────────────────────────────────────────
        _shopPanel     = BuildShopPanel    (canvasGO, font, sealFont);
        _journalPanel  = BuildJournalPanel (canvasGO, font, sealFont);
        _settingsPanel = BuildSettingsPanel(canvasGO, font, sealFont);

        // All panels start hidden
        _shopPanel    .SetActive(false);
        _journalPanel .SetActive(false);
        _settingsPanel.SetActive(false);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Public API
    // ════════════════════════════════════════════════════════════════════════
    public void OpenShop()
    {
        CloseAll();
        RefreshShop();
        UiAnim.Show(_shopPanel);
    }

    public void OpenJournal()
    {
        CloseAll();
        RefreshJournal();
        UiAnim.Show(_journalPanel);
    }

    public void OpenSettings()
    {
        CloseAll();
        RefreshSettings();
        UiAnim.Show(_settingsPanel);
    }

    public void CloseAll()
    {
        UiAnim.Hide(_shopPanel);
        UiAnim.Hide(_journalPanel);
        UiAnim.Hide(_settingsPanel);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Panel builders
    // ════════════════════════════════════════════════════════════════════════

    // ── SHOP PANEL ────────────────────────────────────────────────────────────
    // Layout hierarchy:
    //   ShopPanel (full-screen backdrop)
    //     Card (720×1100 RoundedPanel Image)
    //       ContentContainer (RectTransform child of Card, VerticalLayoutGroup)
    //         ShopHeader         [LayoutElement preferredHeight 64]
    //         SealLine           [LayoutElement preferredHeight 44]
    //         StonesText         [LayoutElement preferredHeight 44]
    //         Divider            [LayoutElement preferredHeight 2]
    //         UpgradeRow0..3     [LayoutElement preferredHeight 144]
    //           (HorizontalLayoutGroup: TextCol flexibleWidth=1 | BuyBtn 140×≥92)
    //             TextCol (VerticalLayoutGroup: Name 38 + Desc 56 + Level 30)
    //             BuyBtn
    //         Spacer             [LayoutElement flexibleHeight 1]
    //         BackBtn            [LayoutElement preferredHeight 92]
    //
    // Budget: pad 80 + 64 + 42 + 42 + 2 + 4×144 + 92 + 9×14 spacing = 1024
    // ≤ 1100 card — the spacer soaks the remaining 76.
    GameObject BuildShopPanel(GameObject canvasGO, Font font, Font sealFont)
    {
        var panel  = MakePanelRoot(canvasGO, "ShopPanel");
        var cardGO = MakeCentredCard(panel, 720, 1100);

        // Content container fills the card with a VerticalLayoutGroup
        var content = MakeContentContainer(cardGO, "ShopContent",
            padTop: 40, padBottom: 40, padLeft: 30, padRight: 30, spacing: 14);

        // ── Header ──────────────────────────────────────────────────────────
        var hdrText = AddTextRow(content, "ShopHeader", font, 52, C_CINNABAR,
            TextAnchor.MiddleCenter, FontStyle.Bold, preferredHeight: 64);
        hdrText.text = "Cultivation";
        InkArt.AddOutline(hdrText, 1f);

        // ── Seal accent (valid glyph: 道) ───────────────────────────────────
        var sealText = AddTextRow(content, "SealLine", sealFont, 34, C_CINNABAR,
            TextAnchor.MiddleCenter, FontStyle.Normal, preferredHeight: 42);
        sealText.text = "道";

        // ── Spirit Stones balance ────────────────────────────────────────────
        _shopStonesText = AddTextRow(content, "StonesText", font, 28, C_JADE,
            TextAnchor.MiddleCenter, FontStyle.Normal, preferredHeight: 42);
        _shopStonesText.text = "Spirit Stones: 0";

        // ── Thin gold divider ────────────────────────────────────────────────
        AddDivider(content, "Divider");

        // ── Upgrade rows ─────────────────────────────────────────────────────
        var core   = Game.I?.Core;
        int upCount = (core != null) ? core.Upgrades.Count : 4;
        _upgradeRows = new UpgradeRow[upCount];

        for (int i = 0; i < upCount; i++)
        {
            // Row container — HorizontalLayoutGroup
            var rowGO = new GameObject("UpgradeRow" + i, typeof(RectTransform));
            rowGO.transform.SetParent(content.transform, false);
            {
                // Faint tinted background for the row
                var rowBg = rowGO.AddComponent<Image>();
                rowBg.color         = new Color(C_INK.r, C_INK.g, C_INK.b, 0.06f);
                rowBg.raycastTarget = false;
            }
            // 144 = pad 16 + Name 38 + Desc 56 + Level 30 + 2×2 spacing
            var rowLE = rowGO.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 144f;
            rowLE.minHeight       = 144f;

            var rowHLG = rowGO.AddComponent<HorizontalLayoutGroup>();
            rowHLG.padding              = new RectOffset(10, 10, 8, 8);
            rowHLG.spacing              = 12f;
            rowHLG.childAlignment       = TextAnchor.MiddleLeft;
            rowHLG.childControlWidth    = true;
            rowHLG.childControlHeight   = true;
            rowHLG.childForceExpandWidth  = false;
            rowHLG.childForceExpandHeight = true;

            // LEFT: vertical sub-container (Name + Desc + Level)
            var textColGO = new GameObject("TextCol", typeof(RectTransform));
            textColGO.transform.SetParent(rowGO.transform, false);
            var textColLE = textColGO.AddComponent<LayoutElement>();
            textColLE.flexibleWidth = 1f;

            var textColVLG = textColGO.AddComponent<VerticalLayoutGroup>();
            textColVLG.padding              = new RectOffset(0, 0, 0, 0);
            textColVLG.spacing              = 2f;
            textColVLG.childAlignment       = TextAnchor.UpperLeft;
            textColVLG.childControlWidth    = true;
            textColVLG.childControlHeight   = true;
            textColVLG.childForceExpandWidth  = true;
            textColVLG.childForceExpandHeight = false;

            // Name
            var nameGO = new GameObject("UpgradeName", typeof(RectTransform));
            nameGO.transform.SetParent(textColGO.transform, false);
            var nameLE = nameGO.AddComponent<LayoutElement>();
            nameLE.preferredHeight = 38f;
            nameLE.minHeight       = 38f;
            var nameText = nameGO.AddComponent<Text>();
            nameText.font            = font;
            nameText.fontSize        = 30;
            nameText.color           = C_INK;
            nameText.alignment       = TextAnchor.MiddleLeft;
            nameText.fontStyle       = FontStyle.Bold;
            nameText.supportRichText = false;
            nameText.raycastTarget   = false;
            nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
            // Single line pinned at 38 — the serif line height slightly exceeds the
            // slot and default Truncate would drop the whole line (invisible name).
            nameText.verticalOverflow   = VerticalWrapMode.Overflow;

            // Desc
            var descGO = new GameObject("UpgradeDesc", typeof(RectTransform));
            descGO.transform.SetParent(textColGO.transform, false);
            var descLE = descGO.AddComponent<LayoutElement>();
            descLE.preferredHeight = 56f; // two wrapped lines at fontSize 24
            descLE.minHeight       = 56f;
            var descText = descGO.AddComponent<Text>();
            descText.font               = font;
            descText.fontSize           = 24;
            descText.color              = C_TEXT_DIM;
            descText.alignment          = TextAnchor.UpperLeft;
            descText.fontStyle          = FontStyle.Normal;
            descText.supportRichText    = false;
            descText.raycastTarget      = false;
            descText.horizontalOverflow = HorizontalWrapMode.Wrap;
            descText.verticalOverflow   = VerticalWrapMode.Overflow;
            descText.text = "";

            // Level
            var lvGO = new GameObject("LevelText", typeof(RectTransform));
            lvGO.transform.SetParent(textColGO.transform, false);
            var lvLE = lvGO.AddComponent<LayoutElement>();
            lvLE.preferredHeight = 30f;
            lvLE.minHeight       = 30f;
            var lvText = lvGO.AddComponent<Text>();
            lvText.font            = font;
            lvText.fontSize        = 24;
            lvText.color           = C_TEXT_DIM;
            lvText.alignment       = TextAnchor.MiddleLeft;
            lvText.fontStyle       = FontStyle.Normal;
            lvText.supportRichText = false;
            lvText.raycastTarget   = false;
            lvText.verticalOverflow = VerticalWrapMode.Overflow; // same truncation guard as the name
            lvText.text = "Lv 0/3";

            // RIGHT: Buy button
            var buyBtnGO = new GameObject("BuyBtn" + i, typeof(RectTransform));
            buyBtnGO.transform.SetParent(rowGO.transform, false);
            var buyBtnLE = buyBtnGO.AddComponent<LayoutElement>();
            buyBtnLE.preferredWidth  = 140f;
            buyBtnLE.minWidth        = 140f;
            buyBtnLE.preferredHeight = 92f; // 44pt touch target (row inner height is 128)
            buyBtnLE.minHeight       = 92f;

            var buyBtnImg = buyBtnGO.AddComponent<Image>();
            buyBtnImg.sprite = InkArt.RoundedPanel(140, 56, 12, 2);
            buyBtnImg.type   = Image.Type.Simple;
            buyBtnImg.color  = Color.white;
            buyBtnImg.raycastTarget = true;

            var buyBtn = buyBtnGO.AddComponent<Button>();
            buyBtn.interactable  = false;
            buyBtn.targetGraphic = buyBtnImg;
            {
                var cb = buyBtn.colors;
                cb.normalColor      = Color.white;
                cb.highlightedColor = Color.Lerp(Color.white, Color.white, 0.12f);
                cb.pressedColor     = new Color(0.85f, 0.85f, 0.85f, 1f);
                cb.disabledColor    = new Color(1f, 1f, 1f, 0.45f);
                cb.colorMultiplier  = 1f;
                buyBtn.colors = cb;
            }

            // Buy button label
            var buyLabelGO = new GameObject("Label", typeof(RectTransform));
            buyLabelGO.transform.SetParent(buyBtnGO.transform, false);
            var buyLabelRT = buyLabelGO.GetComponent<RectTransform>();
            buyLabelRT.anchorMin = Vector2.zero;
            buyLabelRT.anchorMax = Vector2.one;
            buyLabelRT.offsetMin = Vector2.zero;
            buyLabelRT.offsetMax = Vector2.zero;
            var buyLabel = buyLabelGO.AddComponent<Text>();
            buyLabel.font            = font;
            buyLabel.fontSize        = 26;
            buyLabel.color           = C_INK;
            buyLabel.alignment       = TextAnchor.MiddleCenter;
            buyLabel.supportRichText = false;
            buyLabel.raycastTarget   = false;
            buyLabel.text = "---";

            // Closure capture
            int idx = i;
            buyBtn.onClick.AddListener(() =>
            {
                Haptics.Light();
                SoundManager.I?.Play("ui_tap");
                var c = Game.I?.Core;
                if (c != null && c.TryBuyUpgrade(idx))
                {
                    Game.I.SaveProgress();
                    RefreshShop();
                }
            });

            _upgradeRows[i] = new UpgradeRow
            {
                levelText = lvText,
                buyBtn    = buyBtn,
                buyLabel  = buyLabel,
            };

            // Populate static text if core is available
            if (core != null && i < core.Upgrades.Count)
            {
                nameText.text = core.Upgrades[i].Name;
                descText.text = core.Upgrades[i].Desc;
            }
        }

        // ── Flexible spacer ──────────────────────────────────────────────────
        AddSpacer(content, "ShopSpacer");

        // ── Back button ──────────────────────────────────────────────────────
        AddBackButtonToLayout(content, font);

        return panel;
    }

    // ── JOURNAL PANEL ─────────────────────────────────────────────────────────
    // Layout hierarchy:
    //   JournalPanel (full-screen backdrop)
    //     Card (720×1100)
    //       ContentContainer (VerticalLayoutGroup, pad 40/30, spacing 14)
    //         JournalHeader      [preferredHeight 64]
    //         SealLine           [preferredHeight 42]
    //         JournalScroll      [flexibleHeight 1, minHeight 200]  (ScrollRect)
    //           Viewport         (stretch; Image + Mask, mask graphic hidden)
    //             ScrollContent  (VerticalLayoutGroup + ContentSizeFitter)
    //               StatsText    [auto height, wrap]
    //               TechHeader   [preferredHeight 44]
    //               TechDivider  [preferredHeight 2]
    //               TechList     [auto height, wrap]
    //               AchHeader    [preferredHeight 44]
    //               AchDivider   [preferredHeight 2]
    //               AchList      [auto height, wrap]
    //         DailyBtn           [preferredHeight 92]
    //         BackBtn            [preferredHeight 92]
    //
    // Fixed rows budget: pad 80 + header 64 + seal 42 + daily 92 + back 92
    // + 4×14 spacing = 426 → the scroll flexes to 1100 − 426 = 674 (≥ 200 min).
    GameObject BuildJournalPanel(GameObject canvasGO, Font font, Font sealFont)
    {
        var panel  = MakePanelRoot(canvasGO, "JournalPanel");
        var cardGO = MakeCentredCard(panel, 720, 1100);

        var content = MakeContentContainer(cardGO, "JournalContent",
            padTop: 40, padBottom: 40, padLeft: 30, padRight: 30, spacing: 14);

        // Header
        var hdrText = AddTextRow(content, "JournalHeader", font, 52, C_CINNABAR,
            TextAnchor.MiddleCenter, FontStyle.Bold, preferredHeight: 64);
        hdrText.text = "Journal";
        InkArt.AddOutline(hdrText, 1f);

        // Seal accent
        var sealText = AddTextRow(content, "SealLine", sealFont, 34, C_CINNABAR,
            TextAnchor.MiddleCenter, FontStyle.Normal, preferredHeight: 42);
        sealText.text = "道";

        // ── Scrollable middle region ─────────────────────────────────────────
        // Stats + techniques + achievements can outgrow the card, so they live
        // in a masked, touch-draggable ScrollRect between the fixed header rows
        // above and the fixed Daily/Back buttons below.
        GameObject scrollContentGO;
        {
            var scrollGO = new GameObject("JournalScroll", typeof(RectTransform));
            scrollGO.transform.SetParent(content.transform, false);
            var scrollLE = scrollGO.AddComponent<LayoutElement>();
            scrollLE.flexibleHeight = 1f;   // absorb whatever the fixed rows leave over
            scrollLE.minHeight      = 200f; // never collapse below a usable window

            var scroll = scrollGO.AddComponent<ScrollRect>();

            // Viewport — masked window over the scrolled content.
            var viewportGO = new GameObject("Viewport", typeof(RectTransform));
            viewportGO.transform.SetParent(scrollGO.transform, false);
            var viewportRT = viewportGO.GetComponent<RectTransform>();
            viewportRT.anchorMin = Vector2.zero;
            viewportRT.anchorMax = Vector2.one;
            viewportRT.offsetMin = Vector2.zero;
            viewportRT.offsetMax = Vector2.zero;
            viewportRT.pivot     = new Vector2(0f, 1f);

            var viewportImg = viewportGO.AddComponent<Image>();
            viewportImg.color         = Color.white; // never drawn — mask graphic hidden
            viewportImg.raycastTarget = true;        // catches the touch drags
            var viewportMask = viewportGO.AddComponent<Mask>();
            viewportMask.showMaskGraphic = false;

            // Scroll content — own VLG; ContentSizeFitter grows it to true height.
            scrollContentGO = new GameObject("ScrollContent", typeof(RectTransform));
            scrollContentGO.transform.SetParent(viewportGO.transform, false);
            var scRT = scrollContentGO.GetComponent<RectTransform>();
            scRT.anchorMin = new Vector2(0f, 1f);
            scRT.anchorMax = new Vector2(1f, 1f);
            scRT.pivot     = new Vector2(0.5f, 1f);
            scRT.offsetMin = Vector2.zero;
            scRT.offsetMax = Vector2.zero;

            var scVLG = scrollContentGO.AddComponent<VerticalLayoutGroup>();
            scVLG.padding                = new RectOffset(0, 0, 0, 10); // outer VLG already pads L/R 30
            scVLG.spacing                = 14f;
            scVLG.childAlignment         = TextAnchor.UpperCenter;
            scVLG.childControlWidth      = true;
            scVLG.childControlHeight     = true;
            scVLG.childForceExpandWidth  = true;
            scVLG.childForceExpandHeight = false;

            var scFitter = scrollContentGO.AddComponent<ContentSizeFitter>();
            scFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            scFitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content           = scRT;
            scroll.viewport          = viewportRT;
            scroll.horizontal        = false;
            scroll.vertical          = true;
            scroll.movementType      = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
        }

        // Stats block — multiline, wrapping; auto height, the scroll absorbs it
        _journalStatsText = AddTextRow(scrollContentGO, "StatsText", font, 28, C_INK,
            TextAnchor.UpperLeft, FontStyle.Normal, preferredHeight: -1f);
        _journalStatsText.horizontalOverflow = HorizontalWrapMode.Wrap;

        // Techniques sub-header
        var techHdr = AddTextRow(scrollContentGO, "TechHeader", font, 28, C_CINNABAR,
            TextAnchor.MiddleLeft, FontStyle.Bold, preferredHeight: 44);
        techHdr.text = "Techniques";

        // Thin divider
        AddDivider(scrollContentGO, "TechDivider");

        // Techniques list — wrapping; unpinned so the Text's true preferred
        // height drives the layout (verticalOverflow stays Truncate — the
        // layout now always grants the full height, so nothing is cut).
        _journalTechText = AddTextRow(scrollContentGO, "TechList", font, 28, C_TEXT_DIM,
            TextAnchor.UpperLeft, FontStyle.Normal, preferredHeight: -1f);
        _journalTechText.horizontalOverflow = HorizontalWrapMode.Wrap;

        // ── Achievements sub-header ──────────────────────────────────────────
        var achHdr = AddTextRow(scrollContentGO, "AchHeader", font, 28, C_CINNABAR,
            TextAnchor.MiddleLeft, FontStyle.Bold, preferredHeight: 44);
        achHdr.text = "Achievements";
        AddDivider(scrollContentGO, "AchDivider");

        // Achievements list — wrapping; unpinned, same as TechList above.
        _journalAchText = AddTextRow(scrollContentGO, "AchList", font, 26, C_TEXT_DIM,
            TextAnchor.UpperLeft, FontStyle.Normal, preferredHeight: -1f);
        _journalAchText.horizontalOverflow = HorizontalWrapMode.Wrap;

        // ── Daily reward button (fixed footer — NOT scrolled) ────────────────
        {
            var dailyGO = new GameObject("DailyBtn", typeof(RectTransform));
            dailyGO.transform.SetParent(content.transform, false);
            var dlLE = dailyGO.AddComponent<LayoutElement>();
            dlLE.preferredHeight = 92f; // 44pt iOS minimum touch target
            dlLE.minHeight       = 92f;

            var dlImg = dailyGO.AddComponent<Image>();
            dlImg.sprite        = InkArt.RoundedPanel(400, 60, 12, 2);
            dlImg.type          = Image.Type.Simple;
            dlImg.color         = Color.white;
            dlImg.raycastTarget = true;

            _dailyBtn = dailyGO.AddComponent<Button>();
            _dailyBtn.interactable  = false;
            _dailyBtn.targetGraphic = dlImg;
            {
                var cb = _dailyBtn.colors;
                cb.normalColor      = Color.white;
                cb.highlightedColor = new Color(0.94f, 0.94f, 0.94f, 1f);
                cb.pressedColor     = new Color(0.85f, 0.85f, 0.85f, 1f);
                cb.disabledColor    = new Color(1f, 1f, 1f, 0.45f);
                cb.colorMultiplier  = 1f;
                _dailyBtn.colors = cb;
            }

            var dlLabelGO = new GameObject("Label", typeof(RectTransform));
            dlLabelGO.transform.SetParent(dailyGO.transform, false);
            var dlLRT = dlLabelGO.GetComponent<RectTransform>();
            dlLRT.anchorMin = Vector2.zero;
            dlLRT.anchorMax = Vector2.one;
            dlLRT.offsetMin = Vector2.zero;
            dlLRT.offsetMax = Vector2.zero;
            _dailyBtnLabel = dlLabelGO.AddComponent<Text>();
            _dailyBtnLabel.font            = font;
            _dailyBtnLabel.fontSize        = 31;
            _dailyBtnLabel.color           = C_INK;
            _dailyBtnLabel.alignment       = TextAnchor.MiddleCenter;
            _dailyBtnLabel.fontStyle       = FontStyle.Bold;
            _dailyBtnLabel.supportRichText = false;
            _dailyBtnLabel.raycastTarget   = false;
            _dailyBtnLabel.text            = "Daily Qi";

            _dailyBtn.onClick.AddListener(() =>
            {
                Haptics.Light();
                SoundManager.I?.Play("ui_tap");
                var c = Game.I?.Core;
                if (c == null) return;
                int today = (int)(System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 86400L);
                int reward = c.ClaimDaily(today);
                if (reward > 0)
                {
                    Game.I.SaveProgress();
                    RefreshJournal();
                }
            });
        }

        // Back (fixed footer). No spacer — the ScrollRect flexes instead;
        // a flexible spacer here would steal half the scroll's height.
        AddBackButtonToLayout(content, font);

        return panel;
    }

    // ── SETTINGS PANEL ────────────────────────────────────────────────────────
    // Layout hierarchy:
    //   SettingsPanel (full-screen backdrop)
    //     Card (720×960)
    //       ContentContainer (VerticalLayoutGroup, spacing 16)
    //         SettingsHeader     [preferredHeight 64]
    //         SealLine           [preferredHeight 42]
    //         MusicRow           [preferredHeight 92]  (HorizontalLayoutGroup)
    //           MusicLabel       [preferredWidth 180]
    //           MusicSlider      [flexibleWidth 1]
    //         SfxRow             [preferredHeight 92]
    //           SfxLabel
    //           SfxSlider
    //         MuteRow            [preferredHeight 92]
    //           MuteLabel
    //           MuteToggle       [preferredWidth 72]
    //         ResetNormalRow     [preferredHeight 92]  (swaps with confirm row)
    //         ResetConfirmRow    [preferredHeight 92]  (hidden until reset tapped)
    //         Spacer             [flexibleHeight 1]
    //         BackBtn            [preferredHeight 92]
    //
    // Budget: pad 80 + 64 + 42 + 3×92 + 92 (one reset row visible) + 92
    // + 7×16 spacing = 758 ≤ 960 card — the spacer soaks the remaining 202.
    GameObject BuildSettingsPanel(GameObject canvasGO, Font font, Font sealFont)
    {
        var panel  = MakePanelRoot(canvasGO, "SettingsPanel");
        var cardGO = MakeCentredCard(panel, 720, 960);

        var content = MakeContentContainer(cardGO, "SettingsContent",
            padTop: 40, padBottom: 40, padLeft: 30, padRight: 30, spacing: 16);

        // Header
        var hdrText = AddTextRow(content, "SettingsHeader", font, 52, C_CINNABAR,
            TextAnchor.MiddleCenter, FontStyle.Bold, preferredHeight: 64);
        hdrText.text = "Settings";
        InkArt.AddOutline(hdrText, 1f);

        // Seal accent
        var sealText = AddTextRow(content, "SealLine", sealFont, 34, C_CINNABAR,
            TextAnchor.MiddleCenter, FontStyle.Normal, preferredHeight: 42);
        sealText.text = "道";

        // ── Music row ───────────────────────────────────────────────────────
        {
            var rowGO = MakeHorizontalRow(content, "MusicRow", preferredHeight: 92f);
            var lbl = AddLabelToRow(rowGO, "MusicLabel", font, "Music");
            _musicSlider = AddSliderToRow(rowGO, "MusicSlider");
            _musicSlider.onValueChanged.AddListener(v =>
            {
                Game.I?.Core?.SetMusicVol(v);
                Game.I?.SaveProgress();
            });
        }

        // ── SFX row ─────────────────────────────────────────────────────────
        {
            var rowGO = MakeHorizontalRow(content, "SfxRow", preferredHeight: 92f);
            var lbl = AddLabelToRow(rowGO, "SfxLabel", font, "SFX");
            _sfxSlider = AddSliderToRow(rowGO, "SfxSlider");
            _sfxSlider.onValueChanged.AddListener(v =>
            {
                Game.I?.Core?.SetSfxVol(v);
                Game.I?.SaveProgress();
            });
        }

        // ── Mute row ────────────────────────────────────────────────────────
        {
            var rowGO = MakeHorizontalRow(content, "MuteRow", preferredHeight: 92f);
            var lbl = AddLabelToRow(rowGO, "MuteLabel", font, "Mute All");
            _muteToggle = AddToggleToRow(rowGO, "MuteToggle");
            _muteToggle.onValueChanged.AddListener(v =>
            {
                Game.I?.Core?.SetMuted(v);
                Game.I?.SaveProgress();
            });
        }

        // ── Reset Cultivation (danger, confirm-gated) ────────────────────────
        // Normal row: single "Reset Cultivation" button (cinnabar danger style).
        // Clicking it hides the normal row and shows the confirm row.
        // A stray single tap on the normal button CANNOT wipe data — it only opens the gate.
        {
            var normalRowGO = new GameObject("ResetNormalRow", typeof(RectTransform));
            normalRowGO.transform.SetParent(content.transform, false);
            var nrLE = normalRowGO.AddComponent<LayoutElement>();
            nrLE.preferredHeight = 92f; // 44pt iOS minimum touch target
            nrLE.minHeight       = 92f;

            var nrHLG = normalRowGO.AddComponent<HorizontalLayoutGroup>();
            nrHLG.padding               = new RectOffset(0, 0, 0, 0);
            nrHLG.spacing               = 0f;
            nrHLG.childAlignment        = TextAnchor.MiddleCenter;
            nrHLG.childControlWidth     = true;
            nrHLG.childControlHeight    = true;
            nrHLG.childForceExpandWidth  = true;
            nrHLG.childForceExpandHeight = true;

            // The danger button
            var resetBtnGO = new GameObject("ResetBtn", typeof(RectTransform));
            resetBtnGO.transform.SetParent(normalRowGO.transform, false);

            var resetImg = resetBtnGO.AddComponent<Image>();
            resetImg.sprite        = InkArt.RoundedPanel(400, 60, 12, 2);
            resetImg.type          = Image.Type.Simple;
            resetImg.color         = new Color(C_CINNABAR.r, C_CINNABAR.g, C_CINNABAR.b, 0.18f);
            resetImg.raycastTarget = true;

            var resetBtn = resetBtnGO.AddComponent<Button>();
            resetBtn.interactable  = true;
            resetBtn.targetGraphic = resetImg;
            {
                var cb = resetBtn.colors;
                cb.normalColor      = new Color(C_CINNABAR.r, C_CINNABAR.g, C_CINNABAR.b, 0.18f);
                cb.highlightedColor = new Color(C_CINNABAR.r, C_CINNABAR.g, C_CINNABAR.b, 0.30f);
                cb.pressedColor     = new Color(C_CINNABAR.r, C_CINNABAR.g, C_CINNABAR.b, 0.45f);
                cb.disabledColor    = new Color(1f, 1f, 1f, 0.25f);
                cb.colorMultiplier  = 1f;
                resetBtn.colors = cb;
            }

            var resetLabelGO = new GameObject("Label", typeof(RectTransform));
            resetLabelGO.transform.SetParent(resetBtnGO.transform, false);
            var rlrt = resetLabelGO.GetComponent<RectTransform>();
            rlrt.anchorMin = Vector2.zero;
            rlrt.anchorMax = Vector2.one;
            rlrt.offsetMin = Vector2.zero;
            rlrt.offsetMax = Vector2.zero;
            var resetLabel = resetLabelGO.AddComponent<Text>();
            resetLabel.font            = font;
            resetLabel.fontSize        = 28;
            resetLabel.color           = C_CINNABAR;
            resetLabel.alignment       = TextAnchor.MiddleCenter;
            resetLabel.fontStyle       = FontStyle.Bold;
            resetLabel.supportRichText = false;
            resetLabel.raycastTarget   = false;
            resetLabel.text            = "Reset Cultivation";

            _resetNormalRow  = normalRowGO;

            // Confirm row: "Erase all progress?  [Confirm]  [Cancel]"
            // Starts hidden; shown when the normal button is tapped.
            var confirmRowGO = new GameObject("ResetConfirmRow", typeof(RectTransform));
            confirmRowGO.transform.SetParent(content.transform, false);
            var crLE = confirmRowGO.AddComponent<LayoutElement>();
            crLE.preferredHeight = 92f; // 44pt iOS minimum touch target
            crLE.minHeight       = 92f;

            var crHLG = confirmRowGO.AddComponent<HorizontalLayoutGroup>();
            crHLG.padding               = new RectOffset(0, 0, 0, 0);
            crHLG.spacing               = 12f;
            crHLG.childAlignment        = TextAnchor.MiddleCenter;
            crHLG.childControlWidth     = true;
            crHLG.childControlHeight    = true;
            crHLG.childForceExpandWidth  = false;
            crHLG.childForceExpandHeight = true;

            // Prompt label
            var promptGO = new GameObject("ConfirmPrompt", typeof(RectTransform));
            promptGO.transform.SetParent(confirmRowGO.transform, false);
            var promptLE = promptGO.AddComponent<LayoutElement>();
            promptLE.flexibleWidth = 1f;
            var promptText = promptGO.AddComponent<Text>();
            promptText.font            = font;
            promptText.fontSize        = 24;
            promptText.color           = C_CINNABAR;
            promptText.alignment       = TextAnchor.MiddleLeft;
            promptText.fontStyle       = FontStyle.Bold;
            promptText.supportRichText = false;
            promptText.raycastTarget   = false;
            promptText.text            = "Erase all progress?";

            // [Confirm] button
            var confirmBtnGO = new GameObject("ConfirmYesBtn", typeof(RectTransform));
            confirmBtnGO.transform.SetParent(confirmRowGO.transform, false);
            var cbLE = confirmBtnGO.AddComponent<LayoutElement>();
            cbLE.preferredWidth = 160f;
            cbLE.minWidth       = 160f;

            var confirmBtnImg = confirmBtnGO.AddComponent<Image>();
            confirmBtnImg.sprite        = InkArt.RoundedPanel(140, 56, 12, 2);
            confirmBtnImg.type          = Image.Type.Simple;
            confirmBtnImg.color         = C_CINNABAR;
            confirmBtnImg.raycastTarget = true;

            var confirmBtnBtn = confirmBtnGO.AddComponent<Button>();
            confirmBtnBtn.interactable  = true;
            confirmBtnBtn.targetGraphic = confirmBtnImg;
            {
                var cb = confirmBtnBtn.colors;
                cb.normalColor      = C_CINNABAR;
                cb.highlightedColor = Color.Lerp(C_CINNABAR, Color.white, 0.15f);
                cb.pressedColor     = Color.Lerp(C_CINNABAR, Color.black, 0.15f);
                cb.disabledColor    = new Color(1f, 1f, 1f, 0.25f);
                cb.colorMultiplier  = 1f;
                confirmBtnBtn.colors = cb;
            }

            var confirmYesLabelGO = new GameObject("Label", typeof(RectTransform));
            confirmYesLabelGO.transform.SetParent(confirmBtnGO.transform, false);
            var cylrt = confirmYesLabelGO.GetComponent<RectTransform>();
            cylrt.anchorMin = Vector2.zero;
            cylrt.anchorMax = Vector2.one;
            cylrt.offsetMin = Vector2.zero;
            cylrt.offsetMax = Vector2.zero;
            var confirmYesLabel = confirmYesLabelGO.AddComponent<Text>();
            confirmYesLabel.font            = font;
            confirmYesLabel.fontSize        = 26;
            confirmYesLabel.color           = C_PARCHMENT;
            confirmYesLabel.alignment       = TextAnchor.MiddleCenter;
            confirmYesLabel.fontStyle       = FontStyle.Bold;
            confirmYesLabel.supportRichText = false;
            confirmYesLabel.raycastTarget   = false;
            confirmYesLabel.text            = "Confirm";

            // [Cancel] button
            var cancelBtnGO = new GameObject("ConfirmNoBtn", typeof(RectTransform));
            cancelBtnGO.transform.SetParent(confirmRowGO.transform, false);
            var cancLE = cancelBtnGO.AddComponent<LayoutElement>();
            cancLE.preferredWidth = 150f;
            cancLE.minWidth       = 150f;

            var cancelBtnImg = cancelBtnGO.AddComponent<Image>();
            cancelBtnImg.sprite        = InkArt.RoundedPanel(120, 56, 12, 2);
            cancelBtnImg.type          = Image.Type.Simple;
            cancelBtnImg.color         = Color.white;
            cancelBtnImg.raycastTarget = true;

            var cancelBtnBtn = cancelBtnGO.AddComponent<Button>();
            cancelBtnBtn.interactable  = true;
            cancelBtnBtn.targetGraphic = cancelBtnImg;
            {
                var cb = cancelBtnBtn.colors;
                cb.normalColor      = Color.white;
                cb.highlightedColor = new Color(0.94f, 0.94f, 0.94f, 1f);
                cb.pressedColor     = new Color(0.85f, 0.85f, 0.85f, 1f);
                cb.disabledColor    = new Color(1f, 1f, 1f, 0.45f);
                cb.colorMultiplier  = 1f;
                cancelBtnBtn.colors = cb;
            }

            var cancelLabelGO = new GameObject("Label", typeof(RectTransform));
            cancelLabelGO.transform.SetParent(cancelBtnGO.transform, false);
            var clrt = cancelLabelGO.GetComponent<RectTransform>();
            clrt.anchorMin = Vector2.zero;
            clrt.anchorMax = Vector2.one;
            clrt.offsetMin = Vector2.zero;
            clrt.offsetMax = Vector2.zero;
            var cancelLabel = cancelLabelGO.AddComponent<Text>();
            cancelLabel.font            = font;
            cancelLabel.fontSize        = 26;
            cancelLabel.color           = C_INK;
            cancelLabel.alignment       = TextAnchor.MiddleCenter;
            cancelLabel.fontStyle       = FontStyle.Bold;
            cancelLabel.supportRichText = false;
            cancelLabel.raycastTarget   = false;
            cancelLabel.text            = "Cancel";

            _resetConfirmRow = confirmRowGO;
            confirmRowGO.SetActive(false); // hidden until user taps "Reset Cultivation"

            // Wire the three callbacks now that both rows are fully built.
            // Normal → show confirm row, hide normal row (single tap can't wipe).
            resetBtn.onClick.AddListener(() =>
            {
                Haptics.Light();
                SoundManager.I?.Play("ui_tap");
                _resetNormalRow .SetActive(false);
                _resetConfirmRow.SetActive(true);
            });

            // Confirm → execute reset, save, refresh UI, dismiss confirm.
            confirmBtnBtn.onClick.AddListener(() =>
            {
                Haptics.Light();
                SoundManager.I?.Play("ui_tap");
                Game.I?.Core?.ResetCultivation();
                Game.I?.SaveProgress();
                // Refresh any open panels and the main menu realm/best readout.
                RefreshShop();
                RefreshSettings();
                // End any live run so nothing keeps simulating behind the menu.
                Game.I?.EndRunToMenu();
                MainMenu.I?.Show();
                // Dismiss confirm: swap back to normal row.
                _resetConfirmRow.SetActive(false);
                _resetNormalRow .SetActive(true);
            });

            // Cancel → dismiss confirm, no change.
            cancelBtnBtn.onClick.AddListener(() =>
            {
                Haptics.Light();
                SoundManager.I?.Play("ui_tap");
                _resetConfirmRow.SetActive(false);
                _resetNormalRow .SetActive(true);
            });
        }

        // Spacer + Back
        AddSpacer(content, "SettingsSpacer");
        AddBackButtonToLayout(content, font);

        return panel;
    }

    // ════════════════════════════════════════════════════════════════════════
    // Refresh helpers
    // ════════════════════════════════════════════════════════════════════════

    void RefreshShop()
    {
        var core = Game.I?.Core;
        if (_shopStonesText != null)
            _shopStonesText.text = "Spirit Stones: " + (core != null ? core.SpendableStones : 0);

        if (_upgradeRows == null) return;
        for (int i = 0; i < _upgradeRows.Length; i++)
        {
            var row = _upgradeRows[i];
            if (row.levelText == null || row.buyBtn == null || row.buyLabel == null) continue;

            if (core == null || i >= core.Upgrades.Count)
            {
                row.buyBtn.interactable = false;
                continue;
            }

            var def   = core.Upgrades[i];
            int lv    = core.UpgradeLevel(i);
            int cost  = core.NextUpgradeCost(i);
            bool maxed = cost < 0;

            row.levelText.text  = "Lv " + lv + "/" + def.MaxLevel;
            row.buyLabel.text   = maxed ? "MAX" : cost + " stones";
            row.buyBtn.interactable = !maxed && cost <= core.SpendableStones;
        }
    }

    // ── Technique display names ──────────────────────────────────────────────
    // Telegraph.SeenTechniques stores raw ids ("heaven_cleaving_slash"); the
    // journal shows the DisplayName from Telegraph.Resolve ("Heaven-Cleaving
    // Slash"). The id→name map is built once, lazily, by resolving every
    // HazardKind through the static telegraph catalog.
    static Dictionary<string, string> _techniqueNames;

    static string TechniqueDisplayName(string id)
    {
        if (_techniqueNames == null)
        {
            _techniqueNames = new Dictionary<string, string>();
            foreach (Tribulation.Core.HazardKind kind in
                     System.Enum.GetValues(typeof(Tribulation.Core.HazardKind)))
            {
                try
                {
                    var info = Tribulation.Core.Telegraph.Resolve(kind);
                    if (!string.IsNullOrEmpty(info.TechniqueId))
                        _techniqueNames[info.TechniqueId] = info.DisplayName;
                }
                catch (System.ArgumentOutOfRangeException)
                {
                    // HazardKind without a telegraph entry — skip it.
                }
            }
        }

        if (_techniqueNames.TryGetValue(id, out var name)) return name;

        // Unknown id (older save / future content) — prettify it:
        // "some_new_move" → "Some New Move".
        var parts = id.Split('_');
        for (int i = 0; i < parts.Length; i++)
            if (parts[i].Length > 0)
                parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
        return string.Join(" ", parts);
    }

    void RefreshJournal()
    {
        var core = Game.I?.Core;

        if (_journalStatsText != null)
        {
            if (core == null)
            {
                _journalStatsText.text = "No save data yet.";
            }
            else
            {
                int realmIdx = Mathf.Clamp(core.Realm, 0, RealmNames.Length - 1);
                _journalStatsText.text =
                    "Runs           " + core.StatRuns   + "\n" +
                    "Foes Slain     " + core.StatFoes   + "\n" +
                    "Tribulations   " + core.StatTribs  + "\n" +
                    "Deaths         " + core.StatDeaths + "\n" +
                    "Best           " + core.BestLi + " li\n" +
                    "Realm          " + RealmNames[realmIdx];
            }
        }

        if (_journalTechText != null)
        {
            var tele = Game.I?.Tele;
            if (tele == null)
            {
                _journalTechText.text = "None discovered yet — survive to learn your enemies' techniques.";
            }
            else
            {
                var seen = new List<string>(tele.SeenTechniques);
                if (seen.Count == 0)
                {
                    _journalTechText.text = "None discovered yet — survive to learn your enemies' techniques.";
                }
                else
                {
                    for (int i = 0; i < seen.Count; i++)
                        seen[i] = TechniqueDisplayName(seen[i]);
                    _journalTechText.text = string.Join("\n", seen);
                }
            }
        }

        // ── Achievements ─────────────────────────────────────────────────────
        if (_journalAchText != null)
        {
            if (core == null)
            {
                _journalAchText.text = "No data.";
            }
            else
            {
                var sb = new System.Text.StringBuilder();
                foreach (var a in core.Achievements)
                {
                    bool unlocked = core.IsAchUnlocked(a.Id);
                    sb.AppendLine(unlocked
                        ? "✔ " + a.Name
                        : "✧ " + a.Name + " — " + a.Desc);
                }
                _journalAchText.text = sb.ToString().TrimEnd();
            }
        }

        // ── Daily button ──────────────────────────────────────────────────────
        if (_dailyBtn != null && _dailyBtnLabel != null)
        {
            int today = (int)(System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 86400L);
            bool available = core != null && core.DailyAvailable(today);
            _dailyBtn.interactable = available;
            if (core == null)
            {
                _dailyBtnLabel.text = "Daily Qi";
            }
            else if (available)
            {
                int streak   = core.DailyStreak;
                int nextStreak = streak + 1;
                int preview  = 80 * System.Math.Min(nextStreak, 7);
                _dailyBtnLabel.text = $"Claim Daily Qi ({preview} stones)";
            }
            else
            {
                _dailyBtnLabel.text = $"Daily claimed — Streak {core.DailyStreak}";
            }
        }
    }

    void RefreshSettings()
    {
        var core = Game.I?.Core;
        if (core == null) return;

        // Suppress the onValueChanged listeners while we seed the controls
        if (_musicSlider != null) _musicSlider.SetValueWithoutNotify(core.MusicVol);
        if (_sfxSlider   != null) _sfxSlider  .SetValueWithoutNotify(core.SfxVol);
        if (_muteToggle  != null) _muteToggle .SetIsOnWithoutNotify(core.Muted);

        // Always dismiss any half-open confirm gate when re-entering Settings.
        if (_resetNormalRow  != null) _resetNormalRow .SetActive(true);
        if (_resetConfirmRow != null) _resetConfirmRow.SetActive(false);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Layout construction helpers
    // ════════════════════════════════════════════════════════════════════════

    // Full-screen panel root: dim backdrop (raycast-blocking) as the panel itself.
    static GameObject MakePanelRoot(GameObject canvasGO, string name)
    {
        var go  = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(canvasGO.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = go.AddComponent<Image>();
        img.color         = C_BACKDROP;
        img.raycastTarget = true;
        return go;
    }

    // Centred parchment card. Returns the card GameObject (NOT an Image).
    // The card has no layout group itself — a ContentContainer child holds the VLG.
    static GameObject MakeCentredCard(GameObject panel, int w, int h)
    {
        var go = new GameObject("Card", typeof(RectTransform));
        go.transform.SetParent(panel.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(w, h);

        var img = go.AddComponent<Image>();
        img.sprite        = InkArt.RoundedPanel(w, h, 20, 3);
        img.type          = Image.Type.Simple;
        img.color         = Color.white;
        img.raycastTarget = true;
        return go;
    }

    // Content container: a child of the card that fills it, hosting the VLG.
    static GameObject MakeContentContainer(GameObject cardGO, string name,
        int padTop, int padBottom, int padLeft, int padRight, float spacing)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(cardGO.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.padding                 = new RectOffset(padLeft, padRight, padTop, padBottom);
        vlg.spacing                 = spacing;
        vlg.childAlignment          = TextAnchor.UpperCenter;
        vlg.childControlWidth       = true;
        vlg.childControlHeight      = true;
        vlg.childForceExpandWidth   = true;
        vlg.childForceExpandHeight  = false;

        return go;
    }

    // Add a Text child to a layout container, with a LayoutElement for sizing.
    // preferredHeight >= 0 pins the row (min = preferred). Pass a negative
    // preferredHeight to leave it unpinned so the Text's own preferred height
    // drives the layout — used by the journal's scrolled auto-height rows.
    static Text AddTextRow(GameObject container, string name, Font font,
        int fontSize, Color color, TextAnchor alignment, FontStyle style,
        float preferredHeight, float flexibleHeight = -1f)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(container.transform, false);

        var le = go.AddComponent<LayoutElement>();
        if (preferredHeight >= 0f)
        {
            le.preferredHeight = preferredHeight;
            le.minHeight       = preferredHeight;
        }
        if (flexibleHeight >= 0f) le.flexibleHeight = flexibleHeight;

        var t = go.AddComponent<Text>();
        t.font            = font;
        t.fontSize        = fontSize;
        t.color           = color;
        t.alignment       = alignment;
        t.fontStyle       = style;
        t.supportRichText = false;
        t.raycastTarget   = false;
        // Pinned rows are single-line headers/labels: the serif font's line height
        // can exceed the pinned slot by a few px, and the default Truncate then
        // drops the whole line (invisible text). Overflow keeps them rendering;
        // unpinned rows keep Truncate since layout grants their true height.
        if (preferredHeight >= 0f)
            t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    // Add a 2px gold divider line.
    static void AddDivider(GameObject container, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(container.transform, false);

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 2f;
        le.minHeight       = 2f;

        var img = go.AddComponent<Image>();
        img.color         = new Color(C_GOLD.r, C_GOLD.g, C_GOLD.b, 0.35f);
        img.raycastTarget = false;
    }

    // Add a flexible spacer.
    static void AddSpacer(GameObject container, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(container.transform, false);

        var le = go.AddComponent<LayoutElement>();
        le.flexibleHeight = 1f;
        le.minHeight      = 0f;
    }

    // Add a Back button as a layout child (preferredHeight 92 — 44pt target).
    static void AddBackButtonToLayout(GameObject container, Font font)
    {
        var go = new GameObject("BackBtn", typeof(RectTransform));
        go.transform.SetParent(container.transform, false);

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 92f;
        le.minHeight       = 92f;

        var img = go.AddComponent<Image>();
        img.sprite        = InkArt.RoundedPanel(260, 60, 12, 2);
        img.type          = Image.Type.Simple;
        img.color         = Color.white;
        img.raycastTarget = true;

        var btn = go.AddComponent<Button>();
        btn.interactable  = true;
        btn.targetGraphic = img;
        {
            var cb = btn.colors;
            cb.normalColor      = Color.white;
            cb.highlightedColor = new Color(0.94f, 0.94f, 0.94f, 1f);
            cb.pressedColor     = new Color(0.85f, 0.85f, 0.85f, 1f);
            cb.disabledColor    = new Color(1f, 1f, 1f, 0.45f);
            cb.colorMultiplier  = 1f;
            btn.colors = cb;
        }
        btn.onClick.AddListener(() => { Haptics.Light(); SoundManager.I?.Play("ui_tap"); MenuScreens.I?.CloseAll(); });

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(go.transform, false);
        var lrt = labelGO.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;

        var lbl = labelGO.AddComponent<Text>();
        lbl.font            = font;
        lbl.fontSize        = 31;
        lbl.color           = C_INK;
        lbl.alignment       = TextAnchor.MiddleCenter;
        lbl.fontStyle       = FontStyle.Bold;
        lbl.supportRichText = false;
        lbl.raycastTarget   = false;
        lbl.text = "Back";
    }

    // Create a row with HorizontalLayoutGroup for settings controls.
    static GameObject MakeHorizontalRow(GameObject container, string name, float preferredHeight)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(container.transform, false);

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = preferredHeight;
        le.minHeight       = preferredHeight;

        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.padding               = new RectOffset(0, 0, 0, 0);
        hlg.spacing               = 16f;
        hlg.childAlignment        = TextAnchor.MiddleLeft;
        hlg.childControlWidth     = true;
        hlg.childControlHeight    = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = true;

        return go;
    }

    // Add a label to a settings row.
    static Text AddLabelToRow(GameObject rowGO, string name, Font font, string labelText)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(rowGO.transform, false);

        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = 180f;
        le.minWidth       = 180f;

        var t = go.AddComponent<Text>();
        t.font            = font;
        t.fontSize        = 31;
        t.color           = C_INK;
        t.alignment       = TextAnchor.MiddleLeft;
        t.fontStyle       = FontStyle.Bold;
        t.supportRichText = false;
        t.raycastTarget   = false;
        t.text = labelText;
        return t;
    }

    // Add a horizontal Slider to a settings row (flexibleWidth=1).
    static Slider AddSliderToRow(GameObject rowGO, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(rowGO.transform, false);

        var le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f;

        // Background track
        var bgGO = new GameObject("Background", typeof(RectTransform));
        bgGO.transform.SetParent(go.transform, false);
        var bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0f, 0.25f);
        bgRT.anchorMax = new Vector2(1f, 0.75f);
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(C_INK.r, C_INK.g, C_INK.b, 0.18f);

        // Fill area
        var fillAreaGO = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaGO.transform.SetParent(go.transform, false);
        var faRT = fillAreaGO.GetComponent<RectTransform>();
        faRT.anchorMin = new Vector2(0f, 0.25f);
        faRT.anchorMax = new Vector2(1f, 0.75f);
        faRT.offsetMin = new Vector2(5f, 0f);
        faRT.offsetMax = new Vector2(-15f, 0f);

        var fillGO = new GameObject("Fill", typeof(RectTransform));
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        var fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = new Vector2(1f, 1f);
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;
        var fillImg = fillGO.AddComponent<Image>();
        fillImg.color = C_JADE;

        // Handle slide area — a 64-unit band centred on the track. The Slider
        // component force-stretches the handle's cross-axis anchors to fill this
        // area (authored handle anchors get stomped), so the band's height IS the
        // handle height.
        var handleAreaGO = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleAreaGO.transform.SetParent(go.transform, false);
        var haRT = handleAreaGO.GetComponent<RectTransform>();
        haRT.anchorMin = new Vector2(0f, 0.5f);
        haRT.anchorMax = new Vector2(1f, 0.5f);
        haRT.offsetMin = new Vector2(10f, -32f);
        haRT.offsetMax = new Vector2(-10f, 32f);

        var handleGO = new GameObject("Handle", typeof(RectTransform));
        handleGO.transform.SetParent(handleAreaGO.transform, false);
        var hRT = handleGO.GetComponent<RectTransform>();
        var handleImg = handleGO.AddComponent<Image>();
        handleImg.sprite = InkArt.RoundedPanel(36, 64, 14, 2);
        handleImg.type   = Image.Type.Simple;
        handleImg.color  = C_GOLD;

        var slider = go.AddComponent<Slider>();
        slider.fillRect      = fillRT;
        slider.handleRect    = hRT;
        slider.targetGraphic = handleImg;
        slider.direction     = Slider.Direction.LeftToRight;
        slider.minValue      = 0f;
        slider.maxValue      = 1f;
        slider.wholeNumbers  = false;
        slider.value         = 0.8f;

        // Handle: 36 wide, fills the 64-tall slide-area band (see above — the
        // Slider stretches the cross axis, so height comes from the band).
        hRT.pivot            = new Vector2(0.5f, 0.5f);
        hRT.sizeDelta        = new Vector2(36f, 0f);
        hRT.anchoredPosition = Vector2.zero;

        var cb = slider.colors;
        cb.normalColor      = C_GOLD;
        cb.highlightedColor = Color.Lerp(C_GOLD, Color.white, 0.15f);
        cb.pressedColor     = Color.Lerp(C_GOLD, Color.black, 0.1f);
        cb.disabledColor    = new Color(C_GOLD.r, C_GOLD.g, C_GOLD.b, 0.5f);
        cb.colorMultiplier  = 1f;
        slider.colors = cb;

        return slider;
    }

    // Add a Toggle to a settings row.
    static Toggle AddToggleToRow(GameObject rowGO, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(rowGO.transform, false);

        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth  = 72f; // 72x72 square box (35pt), centred in the 92-tall row
        le.minWidth        = 72f;
        le.preferredHeight = 72f;
        le.minHeight       = 72f;
        le.flexibleHeight  = 0f;

        var bgImg = go.AddComponent<Image>();
        bgImg.sprite = InkArt.RoundedPanel(72, 72, 12, 2);
        bgImg.type   = Image.Type.Simple;
        bgImg.color  = new Color(C_INK.r, C_INK.g, C_INK.b, 0.18f);

        var checkGO = new GameObject("Checkmark", typeof(RectTransform));
        checkGO.transform.SetParent(go.transform, false);
        var cRT = checkGO.GetComponent<RectTransform>();
        cRT.anchorMin = new Vector2(0.1f, 0.1f);
        cRT.anchorMax = new Vector2(0.9f, 0.9f);
        cRT.offsetMin = Vector2.zero;
        cRT.offsetMax = Vector2.zero;
        var checkImg = checkGO.AddComponent<Image>();
        checkImg.color = C_GOLD;

        var toggle = go.AddComponent<Toggle>();
        toggle.targetGraphic = bgImg;
        toggle.graphic       = checkImg;
        toggle.isOn          = false;

        return toggle;
    }

    // ════════════════════════════════════════════════════════════════════════
    // Colour helper
    // ════════════════════════════════════════════════════════════════════════
    static Color HexCol(string hex)
    {
        hex = hex.TrimStart('#');
        float r = System.Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
        float g = System.Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
        float b = System.Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;
        return new Color(r, g, b, 1f);
    }
}
