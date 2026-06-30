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
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0.5f;

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
        _shopPanel.SetActive(true);
    }

    public void OpenJournal()
    {
        CloseAll();
        RefreshJournal();
        _journalPanel.SetActive(true);
    }

    public void OpenSettings()
    {
        CloseAll();
        RefreshSettings();
        _settingsPanel.SetActive(true);
    }

    public void CloseAll()
    {
        if (_shopPanel     != null) _shopPanel    .SetActive(false);
        if (_journalPanel  != null) _journalPanel .SetActive(false);
        if (_settingsPanel != null) _settingsPanel.SetActive(false);
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
    //         UpgradeRow0..3     [LayoutElement preferredHeight 120]
    //           (HorizontalLayoutGroup: TextCol flexibleWidth=1 | BuyBtn 140px)
    //             TextCol (VerticalLayoutGroup: Name + Desc + Level)
    //             BuyBtn
    //         Spacer             [LayoutElement flexibleHeight 1]
    //         BackBtn            [LayoutElement preferredHeight 60]
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
            var rowLE = rowGO.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 120f;
            rowLE.minHeight       = 120f;

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
            nameLE.preferredHeight = 34f;
            nameLE.minHeight       = 34f;
            var nameText = nameGO.AddComponent<Text>();
            nameText.font            = font;
            nameText.fontSize        = 26;
            nameText.color           = C_INK;
            nameText.alignment       = TextAnchor.MiddleLeft;
            nameText.fontStyle       = FontStyle.Bold;
            nameText.supportRichText = false;
            nameText.raycastTarget   = false;
            nameText.horizontalOverflow = HorizontalWrapMode.Wrap;

            // Desc
            var descGO = new GameObject("UpgradeDesc", typeof(RectTransform));
            descGO.transform.SetParent(textColGO.transform, false);
            var descLE = descGO.AddComponent<LayoutElement>();
            descLE.preferredHeight = 44f;
            descLE.minHeight       = 44f;
            var descText = descGO.AddComponent<Text>();
            descText.font               = font;
            descText.fontSize           = 20;
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
            lvLE.preferredHeight = 28f;
            lvLE.minHeight       = 28f;
            var lvText = lvGO.AddComponent<Text>();
            lvText.font            = font;
            lvText.fontSize        = 22;
            lvText.color           = C_TEXT_DIM;
            lvText.alignment       = TextAnchor.MiddleLeft;
            lvText.fontStyle       = FontStyle.Normal;
            lvText.supportRichText = false;
            lvText.raycastTarget   = false;
            lvText.text = "Lv 0/3";

            // RIGHT: Buy button
            var buyBtnGO = new GameObject("BuyBtn" + i, typeof(RectTransform));
            buyBtnGO.transform.SetParent(rowGO.transform, false);
            var buyBtnLE = buyBtnGO.AddComponent<LayoutElement>();
            buyBtnLE.preferredWidth = 140f;
            buyBtnLE.minWidth       = 140f;

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
            buyLabel.fontSize        = 22;
            buyLabel.color           = C_INK;
            buyLabel.alignment       = TextAnchor.MiddleCenter;
            buyLabel.supportRichText = false;
            buyLabel.raycastTarget   = false;
            buyLabel.text = "---";

            // Closure capture
            int idx = i;
            buyBtn.onClick.AddListener(() =>
            {
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
    //       ContentContainer (VerticalLayoutGroup)
    //         JournalHeader      [preferredHeight 64]
    //         SealLine           [preferredHeight 42]
    //         StatsText          [preferredHeight 230, wrap]
    //         TechHeader         [preferredHeight 44]
    //         TechDivider        [preferredHeight 2]
    //         TechList           [flexibleHeight 1, wrap]
    //         Spacer             [flexibleHeight 1]
    //         BackBtn            [preferredHeight 60]
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

        // Stats block — multiline, wrapping
        _journalStatsText = AddTextRow(content, "StatsText", font, 24, C_INK,
            TextAnchor.UpperLeft, FontStyle.Normal, preferredHeight: 230);
        _journalStatsText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _journalStatsText.verticalOverflow   = VerticalWrapMode.Overflow;

        // Techniques sub-header
        var techHdr = AddTextRow(content, "TechHeader", font, 28, C_CINNABAR,
            TextAnchor.MiddleLeft, FontStyle.Bold, preferredHeight: 44);
        techHdr.text = "Techniques";

        // Thin divider
        AddDivider(content, "TechDivider");

        // Techniques list — wrapping, flexible height
        _journalTechText = AddTextRow(content, "TechList", font, 22, C_TEXT_DIM,
            TextAnchor.UpperLeft, FontStyle.Normal,
            preferredHeight: 200f, flexibleHeight: 1f);
        _journalTechText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _journalTechText.verticalOverflow   = VerticalWrapMode.Overflow;

        // Spacer + Back
        AddSpacer(content, "JournalSpacer");
        AddBackButtonToLayout(content, font);

        return panel;
    }

    // ── SETTINGS PANEL ────────────────────────────────────────────────────────
    // Layout hierarchy:
    //   SettingsPanel (full-screen backdrop)
    //     Card (720×820)
    //       ContentContainer (VerticalLayoutGroup)
    //         SettingsHeader     [preferredHeight 64]
    //         SealLine           [preferredHeight 42]
    //         MusicRow           [preferredHeight 56]  (HorizontalLayoutGroup)
    //           MusicLabel       [preferredWidth 180]
    //           MusicSlider      [flexibleWidth 1]
    //         SfxRow             [preferredHeight 56]
    //           SfxLabel
    //           SfxSlider
    //         MuteRow            [preferredHeight 56]
    //           MuteLabel
    //           MuteToggle       [preferredWidth 48]
    //         Spacer             [flexibleHeight 1]
    //         BackBtn            [preferredHeight 60]
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
            var rowGO = MakeHorizontalRow(content, "MusicRow", preferredHeight: 56f);
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
            var rowGO = MakeHorizontalRow(content, "SfxRow", preferredHeight: 56f);
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
            var rowGO = MakeHorizontalRow(content, "MuteRow", preferredHeight: 56f);
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
            nrLE.preferredHeight = 60f;
            nrLE.minHeight       = 60f;

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
            resetLabel.fontSize        = 24;
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
            crLE.preferredHeight = 60f;
            crLE.minHeight       = 60f;

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
            promptText.fontSize        = 22;
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
            cbLE.preferredWidth = 140f;
            cbLE.minWidth       = 140f;

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
            confirmYesLabel.fontSize        = 22;
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
            cancLE.preferredWidth = 120f;
            cancLE.minWidth       = 120f;

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
            cancelLabel.fontSize        = 22;
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
                _resetNormalRow .SetActive(false);
                _resetConfirmRow.SetActive(true);
            });

            // Confirm → execute reset, save, refresh UI, dismiss confirm.
            confirmBtnBtn.onClick.AddListener(() =>
            {
                Game.I?.Core?.ResetCultivation();
                Game.I?.SaveProgress();
                // Refresh any open panels and the main menu realm/best readout.
                RefreshShop();
                RefreshSettings();
                MainMenu.I?.Show();
                // Dismiss confirm: swap back to normal row.
                _resetConfirmRow.SetActive(false);
                _resetNormalRow .SetActive(true);
            });

            // Cancel → dismiss confirm, no change.
            cancelBtnBtn.onClick.AddListener(() =>
            {
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
                return;
            }

            var seen = new List<string>(tele.SeenTechniques);
            if (seen.Count == 0)
                _journalTechText.text = "None discovered yet — survive to learn your enemies' techniques.";
            else
                _journalTechText.text = string.Join("\n", seen);
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
    static Text AddTextRow(GameObject container, string name, Font font,
        int fontSize, Color color, TextAnchor alignment, FontStyle style,
        float preferredHeight, float flexibleHeight = -1f)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(container.transform, false);

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = preferredHeight;
        le.minHeight       = preferredHeight;
        if (flexibleHeight >= 0f) le.flexibleHeight = flexibleHeight;

        var t = go.AddComponent<Text>();
        t.font            = font;
        t.fontSize        = fontSize;
        t.color           = color;
        t.alignment       = alignment;
        t.fontStyle       = style;
        t.supportRichText = false;
        t.raycastTarget   = false;
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

    // Add a Back button as a layout child (preferredHeight 60).
    static void AddBackButtonToLayout(GameObject container, Font font)
    {
        var go = new GameObject("BackBtn", typeof(RectTransform));
        go.transform.SetParent(container.transform, false);

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 60f;
        le.minHeight       = 60f;

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
        btn.onClick.AddListener(() => MenuScreens.I?.CloseAll());

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(go.transform, false);
        var lrt = labelGO.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;

        var lbl = labelGO.AddComponent<Text>();
        lbl.font            = font;
        lbl.fontSize        = 28;
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
        t.fontSize        = 28;
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

        // Handle slide area
        var handleAreaGO = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleAreaGO.transform.SetParent(go.transform, false);
        var haRT = handleAreaGO.GetComponent<RectTransform>();
        haRT.anchorMin = Vector2.zero;
        haRT.anchorMax = Vector2.one;
        haRT.offsetMin = new Vector2(10f, 0f);
        haRT.offsetMax = new Vector2(-10f, 0f);

        var handleGO = new GameObject("Handle", typeof(RectTransform));
        handleGO.transform.SetParent(handleAreaGO.transform, false);
        var hRT = handleGO.GetComponent<RectTransform>();
        hRT.sizeDelta = new Vector2(24f, 24f);
        hRT.anchorMin = new Vector2(0f, 0.5f);
        hRT.anchorMax = new Vector2(0f, 0.5f);
        hRT.pivot     = new Vector2(0.5f, 0.5f);
        var handleImg = handleGO.AddComponent<Image>();
        handleImg.color = C_GOLD;

        var slider = go.AddComponent<Slider>();
        slider.fillRect      = fillRT;
        slider.handleRect    = hRT;
        slider.targetGraphic = handleImg;
        slider.direction     = Slider.Direction.LeftToRight;
        slider.minValue      = 0f;
        slider.maxValue      = 1f;
        slider.wholeNumbers  = false;
        slider.value         = 0.8f;

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
        le.preferredWidth = 48f;
        le.minWidth       = 48f;

        var bgImg = go.AddComponent<Image>();
        bgImg.color = new Color(C_INK.r, C_INK.g, C_INK.b, 0.18f);

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
