// HudOverlay.cs — Ink & Talisman in-run HUD (UI-4)
// Code-built uGUI, Screen-Space-Overlay Canvas. No TMP, no prefabs, no external assets.
// Matches Variant A layout from prototypes/in-run-hud.html.
//
// Layout:
//   Top-LEFT  — Realm block (parchment panel, realm name gold/bold serif, kanji accent seal,
//                            layer text-dim)
//   Top-RIGHT — Sky-Net seal-ring (InkArt.SealRing talisman, SoftGlow halo, 天網 kanji label,
//                                  tightens jade→cinnabar as net 0→1)
//   Top-CENTER— li distance readout (parchment tab, serif, 里 accent)
//              — Qi/spirit-stone count tab below dist (parchment tab, gold serif, 靈 accent)
//   Mid       — Combo float (serif + outline)
//   Mid       — Qi-ready flare (serif + SoftGlow halo)
//   Center    — Breakthrough banner (serif + outline, 2s then fade)
//
// shield pips (PlayerRunner.I.Shields), tribulation countdown (GameCore.TribTimeLeft),
// and powerup timers (GameCore.PowerupTimeLeft) are now wired — Issue #9 complete.
// ponytail: full Screen.safeArea rect (top/bottom notch) — currently just 80px top pad

using System.Collections;
using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

public class HudOverlay : MonoBehaviour
{
    public static HudOverlay I;
    // ── Palette (Variant A "Ink & Talisman") ────────────────────────────────
    static readonly Color C_PARCHMENT  = HexCol("#f2e8d0");
    static readonly Color C_INK        = HexCol("#1a1008");
    static readonly Color C_JADE       = HexCol("#2a7c6f");
    static readonly Color C_JADE_LIGHT = HexCol("#4db89e");
    static readonly Color C_CINNABAR   = HexCol("#c0392b");
    static readonly Color C_GOLD       = HexCol("#b8860b");
    static readonly Color C_TEXT_DIM   = HexCol("#6b4e2a");

    // ── Realm data ───────────────────────────────────────────────────────────
    static readonly string[] RealmNames =
    {
        "Qi Condensation", "Foundation Establishment", "Golden Core",
        "Nascent Soul", "Spirit Severing", "Ascension"
    };

    // ── Realm kanji per realm index (mapped from RealmNames order) ──────────
    static readonly string[] RealmKanji =
    {
        "凝氣", "築基", "金丹", "元嬰", "化神", "飛昇"
    };

    // ── Cached UI refs ───────────────────────────────────────────────────────
    Text   _realmName;
    Text   _realmKanji;  // seal-font kanji accent beside the realm name
    Text   _layerText;
    Text   _liText;
    Text   _liKanji;     // 里 accent in seal font
    Text   _stonesText;  // spirit-stone / Qi count readout
    Text   _stonesKanji; // 靈 accent in seal font
    RectTransform _stonesTab; // root rect — punch-scaled on collect
    Text   _comboText;
    Text   _qiFlare;
    GameObject _qiFlareRoot; // flare container (glow + text) — toggled as one
    Text   _breakthroughText;

    // Seal-ring (procedural)
    Image  _sealRingOuter;   // ring image (rotates)
    Image  _sealFill;        // radial-fill inner disk that shrinks from 1→0 as net rises

    // ── New HUD elements (Issue #9 gap-fill) ────────────────────────────────────
    Text _shieldText;   // "Iron Body  ◆◆" — hidden when n==0
    Text _tribText;     // "⚡ HEAVENLY TRIBULATION ⚡\nEndure  Ns" — centred top, gold
    Text _powerupText;  // "Soul-Attraction Talisman 8s   Sword-Qi Dash 3s" — hidden when none

    // Death card root + stats text
    GameObject _deathRoot;
    Text       _deathStats;
    GameObject _reviveBtn; // "RISE AGAIN" rewarded-ad revive button; hidden when no fill
    bool _reviveOfferClosed; // set when this death's offer is spent (declined / failed show) so a late ad load can't resurrect the button; reset each OnDied

    // Run-summary ceremony: count-up + NEW BEST stamp (all unscaled-time tweens)
    GameObject _newBestStamp;      // glow halo + "NEW BEST" text — active only on record runs
    string     _deathStatsFinal = ""; // exact final stats text — restored by HideDeathCard()
    Sequence   _deathSeq;          // ceremony handle — stopped on hide/destroy

    // Trials panel
    GameObject _trialRoot;
    Text[]     _trialRows = new Text[3];

    // Per-row snapshot of what's currently rendered so vow rows only rebuild their
    // strings on an actual change — building three formatted strings every frame was
    // steady GC churn (periodic hitches on device). TrialState instances are replaced
    // wholesale on re-roll, so reference identity + progress + done is a complete key.
    readonly Tribulation.Core.TrialState[] _trialRowSrc = new Tribulation.Core.TrialState[3];
    readonly int[]  _trialRowProg = { -1, -1, -1 };
    readonly bool[] _trialRowDone = new bool[3];

    int _lastLi = -1;       // last rendered li distance — skip the text alloc on equal frames
    int _lastTribSecs = -1; // last rendered tribulation countdown second — ditto

    // Breakthrough fade coroutine handle
    Coroutine _btCo;

    // Near-miss popup ("Near Miss!") + its fade coroutine
    Text      _nearMissText;
    Coroutine _nmCo;

    // Runtime state
    PlayerRunner _player;
    float  _net;
    float  _comboScaleTimer;
    float  _stonesPunchTimer;  // drives punch-scale on the stones tab
    bool   _qiReady;

    // ── Cached string builder to avoid per-frame alloc ───────────────────────
    // (li string is one per-frame alloc — acceptable per spec)

    // ════════════════════════════════════════════════════════════════════════
    // Build
    // ════════════════════════════════════════════════════════════════════════
    void Awake() { I = this; }

    void Start()
    {
        _player = FindObjectOfType<PlayerRunner>();
        BuildCanvas();
        SubscribeCoreEvents();
        RefreshRealmBlock();
    }

    void BuildCanvas()
    {
        // Root Canvas — Screen Space Overlay
        var canvasGO = new GameObject("HudCanvas");
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        // 720×1280 width-matched: phone-readable HUD scale on tall (19.5:9) screens.
        // Menus keep a gentler 810×1440 (see MainMenu/PauseMenu/MenuScreens).
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(720f, 1280f);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0f; // match width — portrait-locked game

        canvasGO.AddComponent<GraphicRaycaster>();

        // All live-HUD elements go under the safe-area root so they clear the
        // notch / Dynamic Island / home indicator. Full-screen veils (death dim)
        // stay on the canvas itself.
        var uiRoot = SafeAreaUI.CreateRoot(canvasGO);

        // ── Art-pass fonts (InkArt cached) ──────────────────────────────────
        Font font     = InkArt.Serif(); // elegant Latin serif for all UI text
        Font sealFont = InkArt.Seal();  // traditional-Chinese subset (23 glyphs)

        // Aesthetic pad inside the safe area (the notch itself is handled by uiRoot).
        const float TOP_PAD  = 24f;
        const float SIDE_PAD = 20f;

        // ── Realm block (top-left) ──────────────────────────────────────────
        // Parchment backing panel behind the realm texts.
        var realmBlock = MakeAnchoredRect(uiRoot, "RealmBlock",
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(SIDE_PAD, -(TOP_PAD)),
            new Vector2(280f, 96f));

        var realmPanelImg = MakeImage(realmBlock, "RealmPanel", Color.white,
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            Vector2.zero, new Vector2(280f, 96f));
        realmPanelImg.sprite = InkArt.RoundedPanel(280, 96, 12, 2);
        realmPanelImg.type   = Image.Type.Simple;

        // Kanji accent (seal font, gold) — two glyphs stacked vertically in a column
        // left of the name. 28pt keeps both INSIDE the 96-tall panel: MakeText uses
        // verticalOverflow=Overflow, so an oversized font (the old 36pt) doesn't clip,
        // it bleeds out the panel bottom. MiddleCenter centers the column vertically.
        _realmKanji = MakeText(realmBlock, "RealmKanji", sealFont, 28, C_GOLD,
            TextAnchor.MiddleCenter,
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(8f, -8f), new Vector2(52f, 80f));

        // Romanized realm name (serif bold, gold) — offset right of the kanji.
        // 18pt in a 52px-tall box: "Foundation Establishment" wraps to two lines
        // (~48px) with real margin before LayerText below — at 20pt the two lines
        // measured 53.9px, a 0.1px squeak past the layer line that device scale
        // rounding could flip back into overlap. Best-fit stays on as a backstop
        // for any longer future name.
        _realmName = MakeText(realmBlock, "RealmName", font, 18, C_GOLD,
            TextAnchor.UpperLeft,
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(66f, -8f), new Vector2(206f, 52f));
        _realmName.fontStyle = FontStyle.Bold;
        _realmName.resizeTextForBestFit = true;
        _realmName.resizeTextMinSize    = 12;
        _realmName.resizeTextMaxSize    = 18;
        InkArt.AddOutline(_realmName, 0.7f);

        _layerText = MakeText(realmBlock, "LayerText", font, 18, C_TEXT_DIM,
            TextAnchor.UpperLeft,
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(66f, -62f), new Vector2(206f, 28f));

        // ── Li distance (top-right, below the seal ring) ────────────────────
        // At the 720-wide reference a centered tab would collide with the realm
        // block, so the right edge stacks: seal ring → li tab → stones tab.
        var distBlock = MakeAnchoredRect(uiRoot, "DistBlock",
            new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-SIDE_PAD, -(TOP_PAD + 96f)),
            new Vector2(220f, 60f));

        // Parchment tab behind the li readout.
        var distPanelImg = MakeImage(distBlock, "DistPanel", Color.white,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            Vector2.zero, new Vector2(220f, 60f),
            new Vector2(0.5f, 1f));
        distPanelImg.sprite = InkArt.RoundedPanel(220, 60, 10, 2);
        distPanelImg.type   = Image.Type.Simple;

        // Serif li readout.
        _liText = MakeText(distBlock, "LiText", font, 26, C_INK,
            TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(-14f, -8f), new Vector2(180f, 48f));

        // 里 kanji accent to the right of the number (seal font).
        _liKanji = MakeText(distBlock, "LiKanji", sealFont, 28, C_TEXT_DIM,
            TextAnchor.UpperLeft,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(76f, -10f), new Vector2(36f, 44f));
        _liKanji.text = "里";

        // ── Spirit-stone / Qi count (below dist tab, top-right) ─────────────
        // Parchment tab, smaller than the li tab; punches on every orb collect.
        var stonesBlock = MakeAnchoredRect(uiRoot, "StonesBlock",
            new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-SIDE_PAD, -(TOP_PAD + 96f + 68f)),   // 8px gap below the 60px dist tab
            new Vector2(180f, 48f));
        _stonesTab = stonesBlock.GetComponent<RectTransform>();

        var stonesPanelImg = MakeImage(stonesBlock, "StonesPanel", Color.white,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            Vector2.zero, new Vector2(180f, 48f),
            new Vector2(0.5f, 1f));
        stonesPanelImg.sprite = InkArt.RoundedPanel(180, 48, 8, 2);
        stonesPanelImg.type   = Image.Type.Simple;

        // Gold serif number — matches the li readout style.
        _stonesText = MakeText(stonesBlock, "StonesText", font, 22, C_GOLD,
            TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(-12f, -6f), new Vector2(140f, 38f));
        _stonesText.fontStyle = FontStyle.Bold;
        InkArt.AddOutline(_stonesText, 0.7f);
        _stonesText.text = "0";

        // 靈 (spirit/soul) kanji accent in seal font.
        _stonesKanji = MakeText(stonesBlock, "StonesKanji", sealFont, 24, C_TEXT_DIM,
            TextAnchor.UpperLeft,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(60f, -8f), new Vector2(30f, 36f));
        _stonesKanji.text = "靈";

        // ── Sky-Net seal-ring (top-right) ───────────────────────────────────
        var sealParent = MakeAnchoredRect(uiRoot, "SealParent",
            new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-SIDE_PAD, -(TOP_PAD)),
            new Vector2(88f, 88f));

        // SoftGlow halo behind the ring (art pass).
        var glowImg = MakeImage(sealParent, "SealGlow", new Color(C_JADE.r, C_JADE.g, C_JADE.b, 0.5f),
            new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(8f, -8f), new Vector2(104f, 104f));
        glowImg.sprite = InkArt.SoftGlow(104);
        glowImg.type   = Image.Type.Simple;

        // Outer ring image — richer InkArt talisman SealRing.
        _sealRingOuter = MakeImage(sealParent, "SealRing", C_JADE,
            new Vector2(1f, 1f), new Vector2(1f, 1f),
            Vector2.zero, new Vector2(88f, 88f));
        _sealRingOuter.sprite = InkArt.SealRing(88);
        _sealRingOuter.color  = C_JADE;

        // 天網 kanji (seal font) centered inside the ring.
        // anchoredPos (-44,-44) with anchor (1,1) and size (44,44) places the label
        // in the center of the 88×88 sealParent. No custom pivot needed.
        var skyNetLabel = MakeText(sealParent, "SkyNetKanji", sealFont, 20, C_JADE_LIGHT,
            TextAnchor.MiddleCenter,
            new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-44f, -44f), new Vector2(44f, 44f));
        skyNetLabel.text = "天網";

        // Radial-fill inner disk — shrinks from 1→0 as net rises (seal "tightens")
        _sealFill = MakeImage(sealParent, "SealFill", C_JADE_LIGHT,
            new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-4f, -4f), new Vector2(80f, 80f));
        _sealFill.type        = Image.Type.Filled;
        _sealFill.fillMethod  = Image.FillMethod.Radial360;
        _sealFill.fillClockwise = true;
        _sealFill.fillAmount  = 1f;
        _sealFill.color       = new Color(C_JADE_LIGHT.r, C_JADE_LIGHT.g, C_JADE_LIGHT.b, 0.35f);
        _sealFill.sprite      = InkArt.SolidCircle(80);

        // ── Combo float (contextual, upper-mid) ────────────────────────────
        var comboGO = MakeAnchoredRect(uiRoot, "Combo",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -330f), new Vector2(200f, 60f)); // below the taller vows panel

        _comboText = MakeText(comboGO, "ComboText", font, 42, C_CINNABAR,
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(200f, 60f));
        _comboText.fontStyle = FontStyle.Bold;
        InkArt.AddOutline(_comboText, 0.9f);
        _comboText.gameObject.SetActive(false);

        // ── Qi-ready flare (contextual) ─────────────────────────────────────
        var qiGO = MakeAnchoredRect(uiRoot, "QiFlare",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -400f), new Vector2(280f, 48f));

        // SoftGlow halo behind the qi-ready text.
        var qiGlowImg = MakeImage(qiGO, "QiGlow", new Color(C_GOLD.r, C_GOLD.g, C_GOLD.b, 0.4f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(300f, 80f),
            new Vector2(0.5f, 0.5f));
        qiGlowImg.sprite = InkArt.SoftGlow(128);
        qiGlowImg.type   = Image.Type.Simple;

        _qiFlare = MakeText(qiGO, "QiFlareText", font, 26, C_GOLD,
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(280f, 48f));
        _qiFlare.text      = "QI READY";
        _qiFlare.fontStyle = FontStyle.Bold;
        InkArt.AddOutline(_qiFlare, 0.8f);
        // Hide the whole flare block — hiding only the text leaves the glow
        // halo floating over the world permanently.
        _qiFlareRoot = qiGO;
        _qiFlareRoot.SetActive(false);

        // ── Breakthrough banner (contextual, centered) ──────────────────────
        var btGO = MakeAnchoredRect(uiRoot, "Breakthrough",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(640f, 80f));

        _breakthroughText = MakeText(btGO, "BtText", font, 38, C_GOLD,
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(640f, 80f));
        _breakthroughText.fontStyle = FontStyle.Bold;
        InkArt.AddOutline(_breakthroughText, 1.0f);
        _breakthroughText.gameObject.SetActive(false);

        // ── Near-miss popup (small, above the breakthrough banner) ──────────
        // note: "Near Miss!" in Latin serif — the InkSeal font is a 23-glyph subset
        // that does not include 險/险, so a kanji flourish would render as tofu.
        var nmGO = MakeAnchoredRect(canvasGO, "NearMiss",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 140f), new Vector2(300f, 44f));

        _nearMissText = MakeText(nmGO, "NearMissText", font, 28, C_JADE_LIGHT,
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(300f, 44f));
        _nearMissText.text      = "Near Miss!";
        _nearMissText.fontStyle = FontStyle.Bold;
        InkArt.AddOutline(_nearMissText, 0.7f);
        _nearMissText.gameObject.SetActive(false);

        // ── Cultivation Vows / Trials panel (below realm block, top-left) ──────
        BuildTrialPanel(uiRoot, font);

        // ── Issue #9: shield pips, tribulation countdown, powerup timers ────────
        BuildShieldPips(uiRoot, font);
        BuildTribText(uiRoot, font);
        BuildPowerupText(uiRoot, font);

        // ── Death card (drawn last so it sits on top of all live HUD) ─────────
        BuildDeathCard(canvasGO, font); // passes InkArt.Serif() — full reskin is PART 2
    }

    // ── Cultivation Vows trials panel ───────────────────────────────────────
    // Anchored top-left, below the Realm block and shield pips.
    // Realm block: anchoredPos (20, -24), size 280x96 → bottom edge at y = -120;
    // shield pips row sits at -128..-160. Trial panel top at y = -170, size 380x140.
    void BuildTrialPanel(GameObject uiRoot, Font font)
    {
        const float SIDE_PAD    = 20f;
        const float PANEL_Y     = -170f;   // top edge y from top of the safe area
        const float PANEL_W     = 380f;
        const float PANEL_H     = 140f;    // header 28 + 3×32 rows + padding
        const float HEADER_H    = 28f;
        const float ROW_H       = 32f;
        const float ROW_INDENT  = 8f;

        // Root container — anchor top-left
        _trialRoot = MakeAnchoredRect(uiRoot, "TrialRoot",
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(SIDE_PAD, PANEL_Y),
            new Vector2(PANEL_W, PANEL_H));

        // Parchment backing panel (white so texture shows through)
        var panelImg = MakeImage(_trialRoot, "TrialPanel", Color.white,
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            Vector2.zero, new Vector2(PANEL_W, PANEL_H));
        panelImg.sprite = InkArt.RoundedPanel((int)PANEL_W, (int)PANEL_H, 10, 2);
        panelImg.type   = Image.Type.Simple;

        // Header: "Cultivation Vows" — serif bold, gold, outlined (Latin only — no seal font)
        var header = MakeText(_trialRoot, "TrialHeader", font, 22, C_GOLD,
            TextAnchor.UpperLeft,
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(ROW_INDENT, -6f), new Vector2(PANEL_W - ROW_INDENT * 2f, HEADER_H));
        header.fontStyle = FontStyle.Bold;
        InkArt.AddOutline(header, 0.6f);
        header.text = "Cultivation Vows";

        // Three trial rows — cached for per-frame update
        for (int i = 0; i < 3; i++)
        {
            float rowY = -(HEADER_H + 10f + i * ROW_H);
            _trialRows[i] = MakeText(_trialRoot, "TrialRow" + i, font, 20, C_INK,
                TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(ROW_INDENT, rowY), new Vector2(PANEL_W - ROW_INDENT * 2f, ROW_H));
            _trialRows[i].supportRichText = false;
            _trialRows[i].gameObject.SetActive(false);
        }

        // Start hidden; Update() shows it when a run has active trials
        _trialRoot.SetActive(false);
    }

    // ── Shield pips (Issue #9) ───────────────────────────────────────────────
    // Positioned top-left below the realm block (realm block bottom: y=-(24+96)=-120;
    // trial panel top: -170; pips sit between them at -128).
    // Faithful to hud.gd set_shields: "Iron Body  " + "◆".repeat(n), hidden when n==0.
    void BuildShieldPips(GameObject uiRoot, Font font)
    {
        // Anchored top-left, just below the realm block (y=-128 leaves 8px gap after -120).
        var go = MakeAnchoredRect(uiRoot, "ShieldPips",
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(20f, -128f),
            new Vector2(320f, 32f));

        _shieldText = MakeText(go, "ShieldText", font, 18,
            new Color(0.85f, 0.88f, 1.0f),  // hud.gd Color(0.85, 0.88, 1.0)
            TextAnchor.MiddleLeft,
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            Vector2.zero, new Vector2(320f, 32f));
        _shieldText.gameObject.SetActive(false); // hidden until shields > 0
    }

    // ── Tribulation countdown (Issue #9) ─────────────────────────────────────
    // Centre-top, gold, two lines, visible only while InTribulation.
    // Faithful to hud.gd set_tribulation / "⚡ HEAVENLY TRIBULATION ⚡\nEndure  Ns".
    void BuildTribText(GameObject uiRoot, Font font)
    {
        var go = MakeAnchoredRect(uiRoot, "TribBlock",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -340f),
            new Vector2(640f, 90f));

        _tribText = MakeText(go, "TribText", font, 32, C_GOLD,
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(640f, 90f));
        _tribText.fontStyle = FontStyle.Bold;
        InkArt.AddOutline(_tribText, 0.9f);
        _tribText.lineSpacing = 1.2f;
        _tribText.gameObject.SetActive(false);
    }

    // ── Powerup timers (Issue #9) ─────────────────────────────────────────────
    // Centre-top below the combo flare. Faithful to hud.gd _refresh_powerups:
    // chips "<Name> <ceil(t)>s" joined by "   "; hidden when none active.
    void BuildPowerupText(GameObject uiRoot, Font font)
    {
        var go = MakeAnchoredRect(uiRoot, "PowerupBlock",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -455f),
            new Vector2(640f, 36f));

        _powerupText = MakeText(go, "PowerupText", font, 20,
            new Color(0.60f, 0.95f, 1.00f), // hud.gd _pu_label color
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(640f, 36f));
        _powerupText.gameObject.SetActive(false);
    }

    void BuildDeathCard(GameObject canvasGO, Font font)
    {
        // Full-screen dim overlay — keep dark veil, unchanged.
        var dimOverlay = MakeImage(canvasGO, "DeathDim",
            new Color(0.02f, 0.03f, 0.05f, 0.80f),
            Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero);
        // Stretch to fill canvas
        var dimRt = dimOverlay.GetComponent<RectTransform>();
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = Vector2.zero;
        dimRt.offsetMax = Vector2.zero;

        // Optional soft-glow bloom behind the card for depth on the dark overlay.
        var glowImg = MakeImage(canvasGO, "DeathGlow",
            new Color(InkArt.Cinnabar.r, InkArt.Cinnabar.g, InkArt.Cinnabar.b, 0.18f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(640f, 640f),
            new Vector2(0.5f, 0.5f));
        glowImg.sprite = InkArt.SoftGlow(256);
        glowImg.type   = Image.Type.Simple;

        // ── Parchment card — RoundedPanel with ink border ──────────────────
        // White color so the sprite's parchment texture shows through untinted.
        // Height 750 fits the three stacked 80-tall action buttons (ad-revive,
        // restart, main menu) below DeathTip with a ~50px bottom margin.
        const float CARD_H = 750f;
        var card = MakeImage(canvasGO, "DeathCard", Color.white,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(560f, CARD_H),
            new Vector2(0.5f, 0.5f));
        card.sprite = InkArt.RoundedPanel(560, (int)CARD_H, 20, 3);
        card.type   = Image.Type.Simple;

        var cardGO = card.gameObject;

        // ── Title "QI DEVIATION" — serif bold, Cinnabar, outlined ─────────
        var deathTitle = MakeText(cardGO, "DeathTitle", font, 46, C_CINNABAR,
            TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -68f), new Vector2(520f, 60f));
        deathTitle.text      = "QI DEVIATION"; // was never assigned — title rendered empty
        deathTitle.fontStyle = FontStyle.Bold;
        InkArt.AddOutline(deathTitle, 0.8f);

        // ── 走火入魔 seal accent — wuxia term for fatal qi-deviation ────────
        // Placed beneath the title; four glyphs in seal font, cinnabar, centered.
        var sealFont = InkArt.Seal();
        var deathSeal = MakeText(cardGO, "DeathSeal", sealFont, 38, C_CINNABAR,
            TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -128f), new Vector2(520f, 52f));
        deathSeal.text = "走火入魔";

        // ── Stats block — updated each death; keep _deathStats ref untouched ─
        _deathStats = MakeText(cardGO, "DeathStats", font, 26, C_INK,
            TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -192f), new Vector2(520f, 160f));
        _deathStats.lineSpacing = 1.3f;

        // ── Dim tip — serif TextDim ─────────────────────────────────────────
        MakeText(cardGO, "DeathTip", font, 20, C_TEXT_DIM,
            TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -366f), new Vector2(500f, 56f))
            .text = "Your realm endures — only this layer's progress is lost.";

        // ── Action buttons — ad-revive, restart, main menu (top to bottom) ──
        // Three matching parchment buttons; every death-card action is an explicit
        // press (no tap-anywhere; Space/Return still work in editor). Revive stays a
        // permanent part of the card — ad readiness is resolved at TAP time
        // (AdsManager preloads, retries, and load-on-demand as a last resort) — and
        // only a spent offer hides it for the rest of that death.
        var reviveBtn = MakeCardButton(cardGO, "ReviveBtn", -436f,
            "WATCH AD TO KEEP RUNNING", C_JADE, font);
        reviveBtn.onClick.AddListener(() =>
        {
            Haptics.Light();
            SoundManager.I?.Play("ui_tap");
            reviveBtn.interactable = false; // prevent double-tap while the ad loads/plays; OnDied() re-enables it next death
            AdsManager.I?.ShowRewardedRevive(success =>
            {
                if (success) Game.I?.PerformRevive();
                else if (_reviveBtn != null)
                {
                    // Declined / no fill / failed — the restart button still works.
                    _reviveOfferClosed = true;
                    _reviveBtn.SetActive(false);
                }
            });
        });
        _reviveBtn = reviveBtn.gameObject;
        _reviveBtn.SetActive(false); // OnDied() re-decides per death

        var restartBtn = MakeCardButton(cardGO, "RestartBtn", -528f,
            "WALK THE ROAD AGAIN", C_GOLD, font);
        restartBtn.onClick.AddListener(() =>
        {
            Haptics.Light();
            SoundManager.I?.Play("ui_tap");
            GameLoop.I?.RestartFromDeathCard();
        });

        // Same end-of-run exit as PauseMenu.QuitToMenu, minus the pause plumbing.
        // Deliberately ad-free — leaving for the menu should never cost an ad.
        var menuBtn = MakeCardButton(cardGO, "MenuBtn", -620f,
            "RETURN TO MAIN MENU", C_TEXT_DIM, font);
        menuBtn.onClick.AddListener(() =>
        {
            Haptics.Light();
            SoundManager.I?.Play("ui_tap");
            GameLoop.I?.OnRunExited();
            HideDeathCard();
            Game.I?.EndRunToMenu();
            MainMenu.I?.Show();
        });

        // ── NEW BEST stamp — chop-mark over the stats block, record runs only ─
        // Latin text on purpose: the seal font is a 23-glyph subset, kanji here
        // would render as tofu. Hidden by default; OnDied pops it in after the
        // count-up when Core.WasNewBestThisRun is set.
        _newBestStamp = MakeAnchoredRect(cardGO, "NewBestStamp",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(140f, -330f), new Vector2(240f, 54f));
        var stampRt = _newBestStamp.GetComponent<RectTransform>();
        stampRt.pivot = new Vector2(0.5f, 0.5f);           // pop scales from center
        stampRt.anchoredPosition = new Vector2(140f, -330f); // re-apply after pivot change
        _newBestStamp.transform.localRotation = Quaternion.Euler(0f, 0f, 7f); // stamped tilt

        // SoftGlow halo behind the stamp (gold, mirrors the qi-flare halo).
        var stampGlow = MakeImage(_newBestStamp, "NewBestGlow",
            new Color(C_GOLD.r, C_GOLD.g, C_GOLD.b, 0.45f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(280f, 96f),
            new Vector2(0.5f, 0.5f));
        stampGlow.sprite = InkArt.SoftGlow(128);
        stampGlow.type   = Image.Type.Simple;

        var stampText = MakeText(_newBestStamp, "NewBestText", font, 30, C_CINNABAR,
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(240f, 54f));
        stampText.text      = "NEW BEST";
        stampText.fontStyle = FontStyle.Bold;
        InkArt.AddOutline(stampText, 0.8f);

        _newBestStamp.SetActive(false);

        // ── Wrap dim overlay, glow, and card under one root ─────────────────
        // A single SetActive on _deathRoot hides/shows the entire card.
        _deathRoot = new GameObject("DeathRoot");
        _deathRoot.transform.SetParent(canvasGO.transform, false);
        var rootRt = _deathRoot.AddComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        // Re-parent all death-card elements under _deathRoot
        dimOverlay.transform.SetParent(_deathRoot.transform, false);
        glowImg.transform.SetParent(_deathRoot.transform, false);
        card.transform.SetParent(_deathRoot.transform, false);

        _deathRoot.SetActive(false);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Procedural sprite builders
    // ════════════════════════════════════════════════════════════════════════

    // Builds a Texture2D ring with concentric outlines + radial spokes, returns Sprite.
    static Sprite BuildRingSprite(int size, Color tint)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color draw  = Color.white; // tinted by Image.color

        // Blank
        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
        tex.SetPixels(pixels);

        int cx = size / 2, cy = size / 2;
        int[] radii = { size / 2 - 1, size / 2 - 4, size / 2 - 8 }; // concentric rings

        // Draw concentric circle outlines
        foreach (int r in radii)
        {
            int rSq = r * r;
            int rInSq = (r - 1) * (r - 1);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int dx = x - cx, dy = y - cy;
                    int dSq = dx * dx + dy * dy;
                    if (dSq <= rSq && dSq >= rInSq)
                        tex.SetPixel(x, y, draw);
                }
            }
        }

        // Draw 8 radial spokes from innermost ring to outer ring
        int spokeInner = radii[2] - 1;
        int spokeOuter = radii[0];
        for (int spoke = 0; spoke < 8; spoke++)
        {
            float angle = spoke * Mathf.PI * 2f / 8f;
            float cosA = Mathf.Cos(angle), sinA = Mathf.Sin(angle);
            for (int t = spokeInner; t <= spokeOuter; t++)
            {
                int px = Mathf.RoundToInt(cx + cosA * t);
                int py = Mathf.RoundToInt(cy + sinA * t);
                if (px >= 0 && px < size && py >= 0 && py < size)
                    tex.SetPixel(px, py, draw);
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    // Builds a solid white circle sprite used for the radial-fill inner disk.
    static Sprite BuildSolidCircleSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color fill  = Color.white;

        int cx = size / 2, cy = size / 2;
        int rSq = (size / 2 - 1) * (size / 2 - 1);

        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
        {
            int x = i % size, y = i / size;
            int dx = x - cx, dy = y - cy;
            pixels[i] = (dx * dx + dy * dy <= rSq) ? fill : clear;
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    // ════════════════════════════════════════════════════════════════════════
    // Event wiring
    // ════════════════════════════════════════════════════════════════════════
    void SubscribeCoreEvents()
    {
        if (Game.I?.Core == null) return;
        var core = Game.I.Core;
        core.QiChanged     += OnQiChanged;
        core.NetChanged    += OnNetChanged;
        core.SoulsChanged  += OnSoulsChanged;
        core.ComboChanged  += OnComboChanged;
        core.Breakthrough  += OnBreakthrough;
        core.Died          += OnDied;
    }

    void UnsubscribeCoreEvents()
    {
        if (Game.I?.Core == null) return;
        var core = Game.I.Core;
        core.QiChanged     -= OnQiChanged;
        core.NetChanged    -= OnNetChanged;
        core.SoulsChanged  -= OnSoulsChanged;
        core.ComboChanged  -= OnComboChanged;
        core.Breakthrough  -= OnBreakthrough;
        core.Died          -= OnDied;
    }

    // ── Event handlers ───────────────────────────────────────────────────────

    void OnQiChanged(float qi, float qiMax)
    {
        bool ready = qi >= qiMax;
        if (ready != _qiReady)
        {
            _qiReady = ready;
            if (_qiFlareRoot != null)
                _qiFlareRoot.SetActive(ready);
        }
    }

    void OnSoulsChanged(int souls)
    {
        if (_stonesText != null)
            _stonesText.text = souls.ToString();
        // Punch-scale the stones tab: snap to 1.4×, let Update() decay it back.
        if (_stonesTab != null)
        {
            _stonesTab.localScale = new Vector3(1.4f, 1.4f, 1f);
            _stonesPunchTimer = 0.20f;
        }
    }

    /// <summary>
    /// Called by Game.OnOrbCollected() to punch the stones counter without waiting
    /// for the SoulsChanged event path (which fires from GameCore, same frame).
    /// Also refreshes the count immediately.
    /// </summary>
    public void PunchStones()
    {
        int souls = Game.I?.Core?.Souls ?? 0;
        OnSoulsChanged(souls);
    }

    void OnNetChanged(float net)
    {
        _net = net;
        UpdateSealRing(net);
    }

    void OnComboChanged(int combo, float mult)
    {
        if (_comboText == null) return;
        if (combo == 0)
        {
            _comboText.gameObject.SetActive(false);
            return;
        }
        _comboText.gameObject.SetActive(true);
        _comboText.text = "×" + combo;  // ×N
        // Pop scale animation
        _comboText.transform.localScale = new Vector3(1.6f, 1.6f, 1f);
        _comboScaleTimer = 0.25f;
    }

    void OnBreakthrough()
    {
        if (_breakthroughText == null) return;
        // Realm has already been incremented by the time this fires
        int realm = Mathf.Clamp(Game.I?.Core?.Realm ?? 0, 0, RealmNames.Length - 1);
        string name = RealmNames[realm];
        _breakthroughText.text = "Breakthrough — " + name;  // em-dash
        _breakthroughText.color = C_GOLD;

        if (_btCo != null) StopCoroutine(_btCo);
        _btCo = StartCoroutine(ShowBannerThenFade(2f, 0.5f));

        // Refresh realm block with new realm
        RefreshRealmBlock();
    }

    // Re-decides "RISE AGAIN" visibility on each death. The button is a permanent part
    // of the death card — AdsManager keeps a rewarded ad preloaded (with load retries),
    // and readiness is only checked at tap time. It hides for the rest of a death only
    // after the offer is spent (declined / no fill / failed show).
    void RefreshReviveButton()
    {
        if (_reviveBtn == null) return;
        bool show = !_reviveOfferClosed && AdsManager.I != null;
        if (show)
        {
            // Re-enable in case a PREVIOUS death's onClick disabled it mid-ad and the ad
            // never resolved to a fresh OnDied (e.g. quit-to-menu mid-show).
            var btn = _reviveBtn.GetComponent<Button>();
            if (btn != null) btn.interactable = true;
        }
        _reviveBtn.SetActive(show);
    }

    void OnDied()
    {
        if (_deathStats == null || _deathRoot == null) return;

        var core = Game.I?.Core;
        if (core == null) return;

        int realm = Mathf.Clamp(core.Realm, 0, RealmNames.Length - 1);
        string realmLine = RealmNames[realm] + " · " + LayerStr(core.MinorLevel());

        int dist  = Mathf.RoundToInt(_player != null ? _player.GetDistance() : 0f);
        int best  = core.BestLi;
        int souls = core.Souls;

        // Reset from any prior ceremony, then reveal the card at zeroed counters.
        if (_deathSeq.isAlive) _deathSeq.Stop();
        if (_newBestStamp != null) _newBestStamp.SetActive(false);
        _reviveOfferClosed = false; // fresh death, fresh offer
        _deathStats.text = ComposeDeathStats(realmLine, 0, best, 0);
        UiAnim.Show(_deathRoot);
        RefreshReviveButton();

        // Ceremony: count li + Qi up (unscaled — the card can outlive a timescale dip),
        // then stamp NEW BEST on a record run.
        var seq = Sequence.Create(useUnscaledTime: true)
            .Chain(Tween.Custom(0f, 1f, 0.8f, t =>
            {
                if (_deathStats == null) return;              // destroyed mid-tween
                _deathStats.text = ComposeDeathStats(
                    realmLine, Mathf.RoundToInt(dist * t), best, Mathf.RoundToInt(souls * t));
            }, Ease.OutQuad, useUnscaledTime: true));

        if (core.WasNewBestThisRun && _newBestStamp != null)
        {
            seq.ChainCallback(() =>
            {
                _newBestStamp.SetActive(true);
                _newBestStamp.transform.localScale = Vector3.one * 0.5f;
                Haptics.Success();
            })
            .Chain(Tween.Scale(_newBestStamp.transform, Vector3.one, 0.35f, Ease.OutBack, useUnscaledTime: true));
        }
        _deathSeq = seq;
    }

    // Death-card stats string; li + Qi are animated 0→final by the OnDied ceremony.
    string ComposeDeathStats(string realmLine, int li, int best, int qi)
        => realmLine
           + "\n\n" + li + " li traveled     Best: " + best + " li"
           + "\n+" + qi + " Qi gathered this run";

    public void HideDeathCard()
    {
        if (_deathSeq.isAlive) _deathSeq.Stop();
        UiAnim.Hide(_deathRoot); // fade the whole card out (stamp included); OnDied re-resets stamp
    }

    // ── Seal-ring visual encoding ────────────────────────────────────────────
    // net 0→1: color lerps jade→cinnabar; fill shrinks 1→0 (seal "tightens/closes")
    void UpdateSealRing(float net)
    {
        if (_sealRingOuter == null || _sealFill == null) return;
        _sealRingOuter.color = Color.Lerp(C_JADE, C_CINNABAR, net);
        _sealFill.fillAmount = 1f - net;
        // Tint the fill to match ring color but keep lower alpha
        Color fc = Color.Lerp(C_JADE_LIGHT, C_CINNABAR, net);
        _sealFill.color = new Color(fc.r, fc.g, fc.b, 0.35f);
    }

    // ── New-best banner ──────────────────────────────────────────────────────
    // Reuses the breakthrough banner machinery (same Text, same coroutine slot) —
    // gold like a breakthrough, distinct from the cinnabar death card.
    /// <summary>Gold "New Best!" banner — Game.OnNewBest fires this the moment a run passes the old record.</summary>
    public void ShowNewBest()
    {
        if (_breakthroughText == null) return;
        _breakthroughText.text  = "New Best!";
        _breakthroughText.color = C_GOLD;
        if (_btCo != null) StopCoroutine(_btCo);
        _btCo = StartCoroutine(ShowBannerThenFade(2f, 0.5f));
    }

    // ── Near-miss popup ──────────────────────────────────────────────────────
    /// <summary>Small transient "Near Miss!" near center — quick pop + ~0.6s fade. Light, non-intrusive.</summary>
    public void ShowNearMiss()
    {
        if (_nearMissText == null) return;
        if (_nmCo != null) StopCoroutine(_nmCo);
        _nmCo = StartCoroutine(NearMissPopThenFade(0.6f));
    }

    IEnumerator NearMissPopThenFade(float secs)
    {
        _nearMissText.gameObject.SetActive(true);
        float t = 0f;
        while (t < secs)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / secs);
            // Quick settle from 1.3× to 1× over the first 0.12s, alpha fades over the full window.
            float s = Mathf.Lerp(1.3f, 1f, Mathf.Clamp01(t / 0.12f));
            _nearMissText.transform.localScale = new Vector3(s, s, 1f);
            _nearMissText.color = new Color(C_JADE_LIGHT.r, C_JADE_LIGHT.g, C_JADE_LIGHT.b, 1f - k);
            yield return null;
        }
        _nearMissText.gameObject.SetActive(false);
        _nmCo = null;
    }

    // ── Breakthrough banner coroutine ────────────────────────────────────────
    IEnumerator ShowBannerThenFade(float holdSecs, float fadeSecs)
    {
        _breakthroughText.gameObject.SetActive(true);
        _breakthroughText.color = C_GOLD;

        yield return new WaitForSeconds(holdSecs);

        float t = 0f;
        while (t < fadeSecs)
        {
            t += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(t / fadeSecs);
            _breakthroughText.color = new Color(C_GOLD.r, C_GOLD.g, C_GOLD.b, alpha);
            yield return null;
        }
        _breakthroughText.gameObject.SetActive(false);
        _btCo = null;
    }

    // ════════════════════════════════════════════════════════════════════════
    // Per-frame
    // ════════════════════════════════════════════════════════════════════════
    void Update()
    {
        // Li distance — string only rebuilt when the displayed integer changes
        if (_liText != null && _player != null)
        {
            int li = (int)_player.GetDistance();
            if (li != _lastLi)
            {
                _lastLi = li;
                _liText.text = "li " + li.ToString("N0");
            }
        }

        // Slow ring rotation
        if (_sealRingOuter != null)
            _sealRingOuter.transform.Rotate(0f, 0f, -4f * Time.deltaTime);

        // Combo pop scale decay
        if (_comboScaleTimer > 0f && _comboText != null)
        {
            _comboScaleTimer -= Time.deltaTime;
            float s = Mathf.Lerp(1f, 1.6f, _comboScaleTimer / 0.25f);
            _comboText.transform.localScale = new Vector3(s, s, 1f);
        }

        // Stones tab punch-scale decay
        if (_stonesPunchTimer > 0f && _stonesTab != null)
        {
            _stonesPunchTimer -= Time.deltaTime;
            float sp = Mathf.Lerp(1f, 1.4f, _stonesPunchTimer / 0.20f);
            _stonesTab.localScale = new Vector3(sp, sp, 1f);
        }

        // Cultivation Vows — poll Trials list each frame
        UpdateTrialPanel();

        // Issue #9: shield pips, tribulation countdown, powerup timers
        UpdateShieldPips();
        UpdateTribText();
        UpdatePowerupText();
    }

    void UpdateTrialPanel()
    {
        if (_trialRoot == null) return;

        var trials = Game.I?.Core?.Trials;
        if (trials == null || trials.Count == 0)
        {
            _trialRoot.SetActive(false);
            return;
        }

        _trialRoot.SetActive(true);

        int count = Mathf.Min(trials.Count, 3);
        for (int i = 0; i < 3; i++)
        {
            if (_trialRows[i] == null) continue;

            if (i >= count)
            {
                _trialRows[i].gameObject.SetActive(false);
                continue;
            }

            var t = trials[i];
            _trialRows[i].gameObject.SetActive(true);

            int prog = (int)t.Progress;
            if (_trialRowSrc[i] == t && _trialRowProg[i] == prog && _trialRowDone[i] == t.Done)
                continue; // rendered text is already current — skip the string rebuild
            _trialRowSrc[i]  = t;
            _trialRowProg[i] = prog;
            _trialRowDone[i] = t.Done;

            // Expand %d manually — C# does not parse printf format strings
            string desc = t.Fmt.Replace("%d", t.Goal.ToString());
            // Format: "Slay 8 foes   6/8   +40"
            _trialRows[i].text = desc + "   " + prog + "/" + t.Goal + "   +" + t.Reward;

            // Done state: gold color; active: jade
            _trialRows[i].color = t.Done ? C_GOLD : C_JADE;
        }
    }

    // ── Shield pips update (Issue #9) ────────────────────────────────────────
    // Faithful to hud.gd set_shields: "Iron Body  " + "◆".repeat(n), hidden when n==0.
    void UpdateShieldPips()
    {
        if (_shieldText == null) return;
        int n = PlayerRunner.I?.Shields ?? 0;
        if (n > 0)
        {
            _shieldText.text = "Iron Body  " + new string('◆', n);
            _shieldText.gameObject.SetActive(true);
        }
        else
        {
            _shieldText.gameObject.SetActive(false);
        }
    }

    // ── Tribulation countdown update (Issue #9) ───────────────────────────────
    // Faithful to hud.gd set_tribulation: visible only while active;
    // text "⚡ HEAVENLY TRIBULATION ⚡\nEndure  Ns".
    void UpdateTribText()
    {
        if (_tribText == null) return;
        var core = Game.I?.Core;
        if (core == null) return;
        bool active = core.InTribulation;
        _tribText.gameObject.SetActive(active);
        if (active)
        {
            int secs = Mathf.CeilToInt(core.TribTimeLeft);
            if (secs != _lastTribSecs)
            {
                _lastTribSecs = secs;
                _tribText.text = "⚡ HEAVENLY TRIBULATION ⚡\nEndure  " + secs + "s";
            }
        }
    }

    // ── Powerup timers update (Issue #9) ─────────────────────────────────────
    // Faithful to hud.gd _refresh_powerups: chips "<Name> <ceil(t)>s" joined by "   ".
    // Display names from POWERUPS dict: magnet→"Soul-Attraction Talisman",
    // double→"Soul-Doubling Pill", dash→"Sword-Qi Dash".
    static readonly (string id, string name)[] _timedPowerups =
    {
        ("magnet", "Soul-Attraction Talisman"),
        ("double", "Soul-Doubling Pill"),
        ("dash",   "Sword-Qi Dash"),
    };

    void UpdatePowerupText()
    {
        if (_powerupText == null) return;
        var core = Game.I?.Core;
        if (core == null) return;

        System.Text.StringBuilder sb = null;
        foreach (var (id, name) in _timedPowerups)
        {
            float t = core.PowerupTimeLeft(id);
            if (t <= 0f) continue;
            if (sb == null) sb = new System.Text.StringBuilder();
            else sb.Append("   ");
            sb.Append(name).Append(' ').Append(Mathf.CeilToInt(t)).Append('s');
        }

        if (sb != null && sb.Length > 0)
        {
            _powerupText.text = sb.ToString();
            _powerupText.gameObject.SetActive(true);
        }
        else
        {
            _powerupText.gameObject.SetActive(false);
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // Realm block refresh
    // ════════════════════════════════════════════════════════════════════════
    void RefreshRealmBlock()
    {
        if (Game.I?.Core == null) return;
        var core = Game.I.Core;
        int realm = Mathf.Clamp(core.Realm, 0, RealmNames.Length - 1);
        string realmName = RealmNames[realm];

        if (_realmName  != null) _realmName.text  = realmName;
        if (_realmKanji != null) _realmKanji.text  = RealmKanji[realm];
        // Layer text shows beneath realm name; together they read as "{RealmName} / {LayerText}"
        if (_layerText  != null) _layerText.text  = LayerStr(core.MinorLevel());
    }

    static string LayerStr(int n)
    {
        if (n >= 10) return "Great Perfection";
        string suffix = n == 1 ? "st" : n == 2 ? "nd" : n == 3 ? "rd" : "th";
        return n + suffix + " Layer";
    }

    // ════════════════════════════════════════════════════════════════════════
    // Lifecycle
    // ════════════════════════════════════════════════════════════════════════
    void OnDestroy()
    {
        UnsubscribeCoreEvents();
        if (_btCo != null) { StopCoroutine(_btCo); _btCo = null; }
        if (_nmCo != null) { StopCoroutine(_nmCo); _nmCo = null; }
        if (_deathSeq.isAlive) _deathSeq.Stop();
    }

    // ════════════════════════════════════════════════════════════════════════
    // uGUI Helpers (no heap alloc beyond initial build)
    // ════════════════════════════════════════════════════════════════════════

    // Parchment death-card action button: RoundedPanel + Button + centered bold label.
    // Built by hand (not MakeImage/MakeText) because raycastTarget must be TRUE to
    // receive clicks — same pattern as PauseMenu's AddPauseButton. Top-anchored at
    // yOffset inside the card. 480x80 at the 720-wide reference ≈ a 43pt-tall target
    // on iPhone — Apple's 44pt tap-target guideline.
    static Button MakeCardButton(GameObject parent, string name, float yOffset,
        string label, Color labelColor, Font font)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 1f);
        rt.anchorMax        = new Vector2(0.5f, 1f);
        rt.pivot            = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, yOffset);
        rt.sizeDelta        = new Vector2(480f, 80f);

        var img = go.AddComponent<Image>();
        img.sprite        = InkArt.RoundedPanel(480, 80, 14, 2);
        img.type          = Image.Type.Simple;
        img.color         = Color.white;
        img.raycastTarget = true;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var cb = btn.colors;
        cb.normalColor      = Color.white;
        cb.highlightedColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        cb.pressedColor     = new Color(0.85f, 0.85f, 0.85f, 1f);
        cb.disabledColor    = new Color(1f, 1f, 1f, 0.45f);
        cb.colorMultiplier  = 1f;
        btn.colors = cb;

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(go.transform, false);
        var lrt = labelGO.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;

        var lbl = labelGO.AddComponent<Text>();
        lbl.font            = font;
        lbl.fontSize        = 26;
        lbl.color           = labelColor;
        lbl.alignment       = TextAnchor.MiddleCenter;
        lbl.fontStyle       = FontStyle.Bold;
        lbl.supportRichText = false;
        lbl.raycastTarget   = false;
        lbl.text            = label;
        return btn;
    }

    static GameObject MakeAnchoredRect(GameObject parent, string name,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin     = anchorMin;
        rt.anchorMax     = anchorMax;
        rt.pivot         = anchorMin; // pivot matches anchor corner
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta     = sizeDelta;
        return go;
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
        t.supportRichText  = false;
        t.raycastTarget    = false;
        // Never truncate a label to nothing: when a font's line-height exceeds a tight box, the
        // default Truncate vertical overflow emits ZERO geometry (the death-card / panel-title
        // "won't render" bug). Overflow always draws the text.
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    // pivot defaults to anchorMin (correct for corner-anchored rects).
    // Pass an explicit pivot when the anchor and desired pivot differ.
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
        rt.pivot            = pivot ?? anchorMin; // default: pivot matches anchor corner
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = sizeDelta;

        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // Parses "#RRGGBB" hex string into a Color.
    static Color HexCol(string hex)
    {
        hex = hex.TrimStart('#');
        float r = System.Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
        float g = System.Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
        float b = System.Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;
        return new Color(r, g, b, 1f);
    }
}
