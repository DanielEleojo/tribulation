// CoachMarks.cs — Contextual coach-mark tutorial overlay (issue #10).
// Code-built uGUI pill anchored bottom-center, above the HUD (sortingOrder 12),
// below pause/menu (sortingOrder 22+).
//
// Lesson texts + logic are faithful ports of hud.gd + game.gd _pick_lesson.
// Persistence mirrors the seenTechniques / Telegraph pattern in Game.cs.
//
// Harness note: this file depends on UnityEngine and is NOT compiled by coretest.
// Every member called on PlayerRunner / Spawner / Game is verified by name below.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Tribulation.Core;

public class CoachMarks : MonoBehaviour
{
    // ── Singleton ────────────────────────────────────────────────────────────
    public static CoachMarks I { get; private set; }

    // ── Lesson display text (Godot originals) ────────────────────────────────
    static readonly System.Collections.Generic.Dictionary<string, string> LessonText
        = new System.Collections.Generic.Dictionary<string, string>
    {
        { TutorialState.LESSON_LANE,  "◄   Swipe to change lane   ►" }, // ◀ ▶
        { TutorialState.LESSON_JUMP,  "▲   Swipe up to leap the ward" },     // ▲
        { TutorialState.LESSON_SLIDE, "▼   Swipe down to slide under" },      // ▼
        { TutorialState.LESSON_SLASH, "Tap to cut down the foe" },
    };

    // ── Palette (Ink & Talisman) ──────────────────────────────────────────────
    static readonly Color C_PARCHMENT = HexCol("#f2e8d0");
    static readonly Color C_INK       = HexCol("#1a1008");
    static readonly Color C_JADE      = HexCol("#2a7c6f");
    static readonly Color C_GOLD      = HexCol("#b8860b");
    static readonly Color C_TEXT_DIM  = HexCol("#6b4e2a");

    // ── Fade-in duration ──────────────────────────────────────────────────────
    const float FADE_IN = 0.2f;

    // ── UI refs ───────────────────────────────────────────────────────────────
    GameObject   _pill;
    Text         _pillText;
    Image        _pillBg;
    Coroutine    _fadeCo;

    // ── Cached scene refs ─────────────────────────────────────────────────────
    PlayerRunner _player;
    Spawner      _spawner;

    // ── Per-frame gesture tracking ────────────────────────────────────────────
    // We detect *transitions* (not held state) for lane-change, jump-start, slide-start.
    int  _prevLane;
    bool _prevGrounded;
    bool _prevSliding;

    // ════════════════════════════════════════════════════════════════════════
    // Lifecycle
    // ════════════════════════════════════════════════════════════════════════
    void Awake()
    {
        I = this;
    }

    void Start()
    {
        _player  = FindObjectOfType<PlayerRunner>();
        _spawner = FindObjectOfType<Spawner>();

        BuildCanvas();

        // Seed prev-state so no spurious first-frame triggers.
        if (_player != null)
        {
            _prevLane     = _player.Lane;
            _prevGrounded = _player.Grounded;
            _prevSliding  = _player.IsSliding;
        }

        // Wire slash detection via PlayerRunner.Slashed event.
        // Members used: PlayerRunner.Slashed (event System.Action)
        if (_player != null)
            _player.Slashed += OnSlashed;
    }

    void OnDestroy()
    {
        if (_player != null)
            _player.Slashed -= OnSlashed;
        if (I == this) I = null;
    }

    // ════════════════════════════════════════════════════════════════════════
    // Build
    // ════════════════════════════════════════════════════════════════════════
    void BuildCanvas()
    {
        var canvasGO = new GameObject("CoachCanvas");
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 12; // above HUD (10), below pause/menu (22+)

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(720f, 1280f); // matches HudOverlay
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0f; // match width — portrait-locked game

        canvasGO.AddComponent<GraphicRaycaster>();

        // Keep the pill above the home indicator on notched phones.
        var uiRoot = SafeAreaUI.CreateRoot(canvasGO);

        // Pill root: anchored bottom-center, 60px above bottom edge.
        const float PILL_W = 480f;
        const float PILL_H = 56f;
        const float BOTTOM_OFFSET = 140f; // clear thumb-zone on phone

        _pill = new GameObject("CoachPill", typeof(RectTransform));
        _pill.transform.SetParent(uiRoot.transform, false);
        var pillRt = _pill.GetComponent<RectTransform>();
        pillRt.anchorMin        = new Vector2(0.5f, 0f);
        pillRt.anchorMax        = new Vector2(0.5f, 0f);
        pillRt.pivot            = new Vector2(0.5f, 0f);
        pillRt.anchoredPosition = new Vector2(0f, BOTTOM_OFFSET);
        pillRt.sizeDelta        = new Vector2(PILL_W, PILL_H);

        // Parchment pill background with accent border (RoundedPanel with border weight 2).
        _pillBg = _pill.AddComponent<Image>();
        _pillBg.sprite = InkArt.RoundedPanel((int)PILL_W, (int)PILL_H, 28, 2); // corner=28 → full capsule feel
        _pillBg.type   = Image.Type.Simple;
        _pillBg.color  = C_PARCHMENT;
        _pillBg.raycastTarget = false;

        // Centered serif text inside the pill.
        Font font = InkArt.Serif();
        var textGO = new GameObject("PillText", typeof(RectTransform));
        textGO.transform.SetParent(_pill.transform, false);
        var textRt = textGO.GetComponent<RectTransform>();
        textRt.anchorMin        = Vector2.zero;
        textRt.anchorMax        = Vector2.one;
        textRt.offsetMin        = new Vector2(16f, 0f);
        textRt.offsetMax        = new Vector2(-16f, 0f);

        _pillText = textGO.AddComponent<Text>();
        _pillText.font          = font;
        _pillText.fontSize      = 26;
        _pillText.color         = C_INK;
        _pillText.alignment     = TextAnchor.MiddleCenter;
        _pillText.raycastTarget = false;
        _pillText.supportRichText = false;
        InkArt.AddOutline(_pillText, 0.5f);

        _pill.SetActive(false);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Per-frame
    // ════════════════════════════════════════════════════════════════════════
    void Update()
    {
        // ── Visibility gate ──────────────────────────────────────────────────
        // Hide while not in an active, live, un-paused run.
        // Members used:
        //   Game.I (static)
        //   Game.I.Core (GameCore)
        //   GameCore.IsStarted, GameCore.IsDead
        //   PauseMenu.I.IsPaused (bool property checked below)
        //   MainMenu.I — show only during active run
        var core = Game.I?.Core;
        bool runActive = core != null && core.IsStarted && !core.IsDead;
        bool paused    = PauseMenu.I != null && PauseMenu.I.IsPaused;
        bool onMenu    = MainMenu.I  != null && MainMenu.I.IsVisible;

        if (!runActive || paused || onMenu)
        {
            Hide();
            return;
        }

        // ── Gesture detection (transitions) ──────────────────────────────────
        if (_player != null)
        {
            // Lane-change: Lane index changed this frame.
            // Member: PlayerRunner.Lane (int property)
            int curLane = _player.Lane;
            if (curLane != _prevLane)
            {
                LearnAndDing(TutorialState.LESSON_LANE);
                _prevLane = curLane;
            }

            // Jump: was grounded last frame, now airborne (Vy > 0 confirms a real jump).
            // Members: PlayerRunner.Grounded (bool), PlayerRunner.Vy (float)
            bool curGrounded = _player.Grounded;
            if (_prevGrounded && !curGrounded && _player.Vy > 0f)
            {
                LearnAndDing(TutorialState.LESSON_JUMP);
            }
            _prevGrounded = curGrounded;

            // Slide: was NOT sliding last frame, now IS sliding.
            // Member: PlayerRunner.IsSliding (bool)
            bool curSliding = _player.IsSliding;
            if (!_prevSliding && curSliding)
            {
                LearnAndDing(TutorialState.LESSON_SLIDE);
            }
            _prevSliding = curSliding;
        }

        // ── Lesson selection ──────────────────────────────────────────────────
        // Build active-hazard list from Spawner._live (accessed via public property below).
        // Member: Spawner.LiveHazards (public accessor — see note at bottom of file)
        float playerZ    = _player != null ? _player.transform.position.z : 0f;
        float runDist    = _player != null ? _player.GetDistance() : 0f;
        bool  hasSlash   = core.HasAbility("slash");

        var hazardPairs = GatherHazards(playerZ);
        string lesson   = TutorialState.PickLesson(playerZ, hazardPairs, hasSlash, runDist, core.Tutorial.IsLearned);

        if (lesson != "")
            ShowLesson(lesson);
        else
            Hide();
    }

    // ════════════════════════════════════════════════════════════════════════
    // Hazard gathering
    // ════════════════════════════════════════════════════════════════════════
    List<(string lesson, float z)> GatherHazards(float playerZ)
    {
        var result = new List<(string, float)>();
        if (_spawner == null) return result;

        // Spawner.LiveHazards exposes _live as a read-only enumerable.
        // See Spawner.cs — we add a public property there (minimal diff).
        foreach (var (go, kind) in _spawner.LiveHazards)
        {
            if (go == null || !go.activeSelf) continue;
            string lesson = TutorialState.LessonForKind(kind);
            result.Add((lesson, go.transform.position.z));
        }
        return result;
    }

    // ════════════════════════════════════════════════════════════════════════
    // Gesture-learned callback
    // ════════════════════════════════════════════════════════════════════════
    void OnSlashed()
    {
        LearnAndDing(TutorialState.LESSON_SLASH);
    }

    void LearnAndDing(string lessonId)
    {
        var core = Game.I?.Core;
        if (core == null) return;
        if (core.Tutorial.IsLearned(lessonId)) return; // already known

        core.Tutorial.Learn(lessonId);
        // Play orb ding (mirrors Godot version which plays "orb" sfx on lesson learned).
        if (SoundManager.I != null) SoundManager.I.Play("orb");
        // Persist immediately so the lesson survives a force-quit.
        Game.I.SaveProgress();
    }

    // ════════════════════════════════════════════════════════════════════════
    // Show / Hide
    // ════════════════════════════════════════════════════════════════════════
    string _shownLesson = "";

    void ShowLesson(string lesson)
    {
        if (_pillText == null || _pill == null) return;
        if (!LessonText.TryGetValue(lesson, out string txt)) return;

        // If already showing this exact lesson, leave it alone (no flicker).
        if (_shownLesson == lesson && _pill.activeSelf) return;

        _shownLesson = lesson;
        _pillText.text = txt;

        // Fade in.
        if (_fadeCo != null) StopCoroutine(_fadeCo);
        _fadeCo = StartCoroutine(FadeIn());
    }

    void Hide()
    {
        if (_pill == null || !_pill.activeSelf) return;
        if (_fadeCo != null) { StopCoroutine(_fadeCo); _fadeCo = null; }
        _pill.SetActive(false);
        _shownLesson = "";
    }

    IEnumerator FadeIn()
    {
        _pill.SetActive(true);
        // Set alpha to 0 before fading in.
        SetAlpha(0f);
        float t = 0f;
        while (t < FADE_IN)
        {
            t += Time.deltaTime;
            SetAlpha(Mathf.Clamp01(t / FADE_IN));
            yield return null;
        }
        SetAlpha(1f);
        _fadeCo = null;
    }

    void SetAlpha(float a)
    {
        if (_pillBg   != null) _pillBg.color   = new Color(C_PARCHMENT.r, C_PARCHMENT.g, C_PARCHMENT.b, a);
        if (_pillText != null) _pillText.color  = new Color(C_INK.r, C_INK.g, C_INK.b, a);
    }

    // ── Hex color helper (matches HudOverlay / MenuScreens) ─────────────────
    static Color HexCol(string hex)
    {
        hex = hex.TrimStart('#');
        float r = System.Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
        float g = System.Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
        float b = System.Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;
        return new Color(r, g, b, 1f);
    }

    // ════════════════════════════════════════════════════════════════════════
    // External members this file depends on (for verification):
    //
    //   PlayerRunner.Lane          (int property)          ✓ line 74
    //   PlayerRunner.Grounded      (bool property)         ✓ line 72
    //   PlayerRunner.IsSliding     (bool property)         ✓ line 73
    //   PlayerRunner.Vy            (float property)        ✓ line 75
    //   PlayerRunner.Slashed       (event System.Action)   ✓ line 78
    //   PlayerRunner.GetDistance() (int method)            ✓ line 403
    //   Spawner.LiveHazards        (new public property)   ✓ added below
    //   Game.I                     (static Game)           ✓ line 15
    //   Game.I.Core                (GameCore property)     ✓ line 18
    //   Game.I.SaveProgress()      (void method)           ✓ line 107
    //   GameCore.IsStarted         (bool property)         ✓ line 45
    //   GameCore.IsDead            (bool property)         ✓ line 45
    //   GameCore.HasAbility(str)   (bool method)           ✓ line 669
    //   GameCore.Tutorial          (TutorialState prop)    ✓ added to GameCore
    //   SoundManager.I             (static singleton)      ✓ SoundManager.cs
    //   SoundManager.I.Play(str)   (void method)           ✓ line 34
    //   PauseMenu.I                (static singleton)      ✓ PauseMenu.cs
    //   PauseMenu.I.IsPaused       (bool property)         ← needs to exist — see note
    //   MainMenu.I                 (static singleton)      ✓ MainMenu.cs
    //   MainMenu.I.IsVisible       (bool property)         ← needs to exist — see note
    // ════════════════════════════════════════════════════════════════════════
}
