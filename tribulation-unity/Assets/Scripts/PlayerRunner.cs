using UnityEngine;
using Tribulation.Core;

// Tracer-bullet port of player.gd — CORE MOTION ONLY (powerups, dread form, particles,
// model/anim all deferred to Phase 3). Auto-runs forward (-Z) with a speed ramp + endless
// creep, eases between 3 lanes, jumps (buffer + coyote), slides (ground crouch / mid-air
// fast-fall), Qi-Leap (double jump, #6), Cloud-Tread glide (#6),
// and Sword-flight aerial mode (#7, 御剑). Uses a CharacterController.
[RequireComponent(typeof(CharacterController))]
public class PlayerRunner : MonoBehaviour
{
    [Tooltip("Child transform holding the visual; squashed on slide.")]
    public Transform visual;

    public float gravity = 48f;
    public float jumpVelocity = 17f;
    public float fastFall = 46f;

    const float LANE_WIDTH = 2.5f;
    const int LANE_COUNT = 3;
    const float LANE_SHARPNESS = 16f;  // crisper than Godot's 12 — shortens the ease tail
    const float MAX_LANE_SPEED = 28f;  // up from 18 so the initial dash isn't capped short
    const float STAND_HEIGHT = 2f;
    const float SLIDE_HEIGHT = 1f;
    const float SLIDE_DURATION = 0.65f;
    const float JUMP_BUFFER = 0.12f;
    const float COYOTE = 0.10f;
    const float GROUND_STICK = 2f;

    // Slash (ported from player.gd try_slash).
    const float SLASH_COOLDOWN = 0.25f;  // seconds between slashes (player.gd slash_cooldown)
    float _slashCd;

    // Non-lethal contact (loop redesign): a hit spikes the Net + stumbles + grants i-frames
    // so a dense cluster can't chain-spike you in a single frame. Death is the Net's job now.
    const float HIT_IFRAMES = 0.6f;   // invulnerable window after a hit
    const float HIT_STUMBLE = 0.35f;  // seconds of slowed run after a hit
    const float STUMBLE_MULT = 0.45f; // run-speed factor while stumbling
    float _iframes, _stumbleT;

    // Feel Pass v1 cached refs.
    IFeelPose _poser;            // resolved lazily — rigged char adds its driver in Start, fallback even later
    CameraFollow _camCache;
    CameraFollow Cam() { if (_camCache == null) _camCache = FindObjectOfType<CameraFollow>(); return _camCache; }
    /// <summary>Scale-pop the active character visual (game-feel). Orb collect / kill / Qi burst.</summary>
    public void FeelPop(float strength) { if (_poser == null) _poser = GetComponent<IFeelPose>(); _poser?.Pop(strength); }

    // Survivability (#8): Iron-Body shields + Blood-Sprint + Dread Form.
    readonly Tribulation.Core.Survivability _surv = new Tribulation.Core.Survivability();
    int _lastRealm = -1;          // track realm changes to call ApplyRealmStats
    bool _dread;                  // Dread Form active flag (Ascension tier)

    // Qi-Leap (double jump, #6) — mirrors player.gd _air_jumps_used / _max_air_jumps().
    int _airJumpsUsed;

    // Sword-flight (#7, 御剑) — pure state machine in Core, no UnityEngine.
    readonly SwordFlight _flight = new SwordFlight();

    // pulled from Balance in Awake
    float _baseSpeed = 12f, _maxSpeed = 22f, _rampTime = 90f, _creep = 0.07f, _creepCap = 16f;

    CharacterController _cc;
    int _lane = 1;                 // 0 left, 1 center, 2 right
    float _runSpeed, _runTime, _startZ, _vy;
    bool _sliding, _pendingSlide, _wasGrounded, _dead, _running;
    float _slideLeft, _jumpBuf, _coyote;

    public bool IsDead    => _dead;
    public bool IsFlying  => _flight.IsFlying;

    // ── Visual state hooks (read by InkCultivator; no movement logic changed) ─
    public bool  Grounded  => _cc != null && _cc.isGrounded;
    public bool  IsSliding => _sliding;
    public int   Lane      => _lane;
    public float Vy        => _vy;

    /// <summary>Fired when a slash actually executes (gates passed, cooldown reset).</summary>
    public event System.Action Slashed;

    // ── Singleton (mirrors Game.I / HudOverlay.I / PauseMenu.I pattern) ─────────
    public static PlayerRunner I { get; private set; }

    /// <summary>Current Iron-Body shield count (from Survivability). 0 at realms below Nascent Soul.</summary>
    public int Shields    => _surv.Shields;
    /// <summary>Maximum Iron-Body shield slots for the current realm.</summary>
    public int MaxShields => _surv.MaxShields;

    void Awake()
    {
        I = this;
        _cc = GetComponent<CharacterController>();
        var b = Balance.D;
        _baseSpeed = b.player_base_speed; _maxSpeed = b.player_max_speed; _rampTime = b.player_speed_ramp_time;
        _creep = b.player_speed_creep; _creepCap = b.player_speed_creep_cap;
        _runSpeed = _baseSpeed;
        _startZ = transform.position.z;
        SetHeight(STAND_HEIGHT);
    }

    void Start()
    {
        var s = SwipeDetector.I;
        if (s != null)
        {
            s.SwipedLeft += MoveLeft; s.SwipedRight += MoveRight;
            s.SwipedUp += TryJump; s.SwipedDown += StartSlide;
            s.Tapped += TrySlash;
        }
        _running = false; // title screen shows first; MainMenu calls BeginRunning()
    }

    void Update()
    {
        if (_dead) return;
        if (_slashCd > 0f) _slashCd -= Time.deltaTime;
        if (_iframes > 0f) _iframes -= Time.deltaTime;
        if (_stumbleT > 0f) _stumbleT -= Time.deltaTime;

        // ── Survivability (#8): realm-change detect + per-frame tick ──────────
        {
            var _coreForSurv = (Game.I != null) ? Game.I.Core : null;
            if (_coreForSurv != null)
            {
                int currentRealm = _coreForSurv.Realm;
                if (currentRealm != _lastRealm)
                {
                    _surv.ApplyRealmStats(_coreForSurv.ShieldSlots, _coreForSurv.SprintPerKill);
                    // Dread Form: active at Ascension (realm 5, the final tier).
                    bool isDread = currentRealm >= 5;
                    if (isDread != _dread)
                    {
                        _dread = isDread;
                        _surv.SetDread(_dread);
                        // ponytail: Dread Form visual aura — deferred to visual polish
                    }
                    _lastRealm = currentRealm;
                }
            }
            _surv.Tick(Time.deltaTime);
        }

        if (!_running) { _vy -= gravity * Time.deltaTime; _cc.Move(Vector3.up * _vy * Time.deltaTime); if (_cc.isGrounded) _vy = -GROUND_STICK; return; }

        // Keyboard (desktop testing).
        if (Input.GetKeyDown(KeyCode.F)) TrySlash(); // desktop slash test key
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) MoveLeft();
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) MoveRight();
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow)) TryJump();
        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) StartSlide();

        bool grounded = _cc.isGrounded;

        // Cache core for ability checks; null-safe (both powers off if no Core).
        var core = (Game.I != null) ? Game.I.Core : null;

        // ── Sword-flight (#7 御剑) ────────────────────────────────────────────
        // Tick the state machine every frame; it advances the cooldown only while
        // grounded+canFly and exits after DURATION. Mirrors player.gd lines 373-377.
        bool canSwordFly = core != null && core.HasAbility("swordflight");
        _flight.Tick(Time.deltaTime, grounded, canSwordFly);

        if (_flight.IsFlying)
        {
            // Flight branch owns this frame — same as player.gd `if _flying: _process_flight; return`.
            // End any active slide when entering flight (player.gd _enter_flight line 519).
            if (_sliding) EndSlide();

            // Lane input still processed (player.gd _process_flight lines 546-548).
            // Slash still allowed (player.gd _process_flight line 550).
            // (jump/slide key input is consumed as climb/dive — NOT routed to jump/slide logic)

            // Forward run: same ramp+creep as ground (player.gd _process_flight lines 567-570).
            _runTime += Time.deltaTime;
            float ramp2 = Mathf.Clamp01(_runTime / _rampTime);
            float speedMult2 = (Game.I != null) ? Game.I.Core.SpeedMult : 1f;
            float dashBonus2 = (Game.I != null && Game.I.IsPowerupActive("dash")) ? 12.0f : 0f;
            _runSpeed = Mathf.Lerp(_baseSpeed, _maxSpeed, ramp2) * speedMult2 + EndlessCreep() + _surv.SprintBoost + dashBonus2;

            // Lane ease (same formula as ground — player.gd _process_flight lines 571-572).
            float targetX2 = -(_lane - 1) * LANE_WIDTH;
            float vx2 = Mathf.Clamp((targetX2 - transform.position.x) * LANE_SHARPNESS,
                                     -MAX_LANE_SPEED, MAX_LANE_SPEED);

            // Vertical: climb-held = jump key OR touch finger down;
            //           dive-held  = slide key (player.gd lines 560-563).
            bool climbHeld = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.UpArrow)
                             || (SwipeDetector.I != null && SwipeDetector.I.IsHolding);
            bool diveHeld  = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
            _vy = _flight.ClimbVelocity(transform.position.y, climbHeld, diveHeld);

            _cc.Move(new Vector3(vx2, _vy, -_runSpeed) * Time.deltaTime);
            // ponytail: sword-mount VFX / anim update — deferred to visual polish
            return; // ground path skipped — exactly mirrors player.gd's early return
        }
        // ── End of flight branch ──────────────────────────────────────────────

        // Jump buffer + coyote time.
        // On landing, reset air-jump counter (mirrors player.gd line 382).
        if (grounded) _airJumpsUsed = 0;
        _coyote = grounded ? COYOTE : Mathf.Max(0f, _coyote - Time.deltaTime);
        if (_jumpBuf > 0f) _jumpBuf -= Time.deltaTime;
        if (_jumpBuf > 0f && _coyote > 0f)
        {
            // Ground jump (existing — unchanged).
            if (_sliding) EndSlide();
            _vy = jumpVelocity; _coyote = 0f; _jumpBuf = 0f; _pendingSlide = false;
            Cam()?.AddFovKick(2.5f); // feel: small FOV pop on launch
            Feel.DustBurst(transform.position);
        }
        else if (_jumpBuf > 0f)
        {
            // Qi-Leap: mid-air second jump (Foundation+). Mirrors player.gd lines 397-402.
            bool hasDoubleJump = core != null && core.HasAbility("doublejump");
            int maxAir = Locomotion.MaxAirJumps(hasDoubleJump);
            if (_airJumpsUsed < maxAir)
            {
                _vy = jumpVelocity * 0.92f;
                _airJumpsUsed++;
                _jumpBuf = 0f;
                // ponytail: Qi-Leap burst VFX — deferred to visual polish
            }
        }

        // Feel: dust puff + camera trauma on a real landing (was falling, now grounded).
        if (grounded && !_wasGrounded && _vy < -4f) { Feel.Poof(transform.position, 1.3f); Cam()?.AddTrauma(0.12f); }

        // A queued fast-fall slide fires on landing.
        if (grounded && !_wasGrounded && _pendingSlide) { _pendingSlide = false; StartSlide(); }
        if (_sliding) { _slideLeft -= Time.deltaTime; if (_slideLeft <= 0f) EndSlide(); }
        _wasGrounded = grounded;

        // Forward run: ramp to max over time, then bounded endless creep.
        _runTime += Time.deltaTime;
        float ramp = Mathf.Clamp01(_runTime / _rampTime);
        float speedMult = (Game.I != null) ? Game.I.Core.SpeedMult : 1f;
        float dashBonus = (Game.I != null && Game.I.IsPowerupActive("dash")) ? 12.0f : 0f;
        _runSpeed = Mathf.Lerp(_baseSpeed, _maxSpeed, ramp) * speedMult + EndlessCreep() + _surv.SprintBoost + dashBonus;
        if (_stumbleT > 0f) _runSpeed *= STUMBLE_MULT; // briefly stagger after a hit

        // Ease sideways toward the target lane. Negated because the chase cam faces -Z and
        // Unity is left-handed, so world +X renders on screen-LEFT — without the flip, "right" moves you left.
        float targetX = -(_lane - 1) * LANE_WIDTH;
        float vx = Mathf.Clamp((targetX - transform.position.x) * LANE_SHARPNESS, -MAX_LANE_SPEED, MAX_LANE_SPEED);

        // Gravity (with ground-stick so isGrounded stays reliable).
        if (grounded && _vy < 0f) _vy = -GROUND_STICK;

        // Cloud-Tread glide: hold jump to slow the fall (Nascent Soul+).
        // Mirrors player.gd lines 438-440. glideHeld = keyboard hold OR touch finger down.
        bool canGlide = core != null && core.HasAbility("glide");
        bool glideHeld = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.UpArrow)
                         || (SwipeDetector.I != null && SwipeDetector.I.IsHolding);
        float effectiveGravity = Locomotion.GlideGravity(gravity, grounded, _vy, canGlide, glideHeld);
        // ponytail: Cloud-Tread glide VFX/SFX — deferred to visual polish
        _vy -= effectiveGravity * Time.deltaTime;

        _cc.Move(new Vector3(vx, _vy, -_runSpeed) * Time.deltaTime);
    }

    float EndlessCreep() => Mathf.Clamp((_runTime - _rampTime) * _creep, 0f, _creepCap);

    public void MoveLeft() { if (!_dead) _lane = Mathf.Max(0, _lane - 1); }
    public void MoveRight() { if (!_dead) _lane = Mathf.Min(LANE_COUNT - 1, _lane + 1); }
    public void TryJump()
    {
        if (!_dead)
        {
            _jumpBuf = JUMP_BUFFER;
            if (SoundManager.I != null) SoundManager.I.Play("jump");
        }
    }

    public void StartSlide()
    {
        if (_dead) return;
        if (_cc.isGrounded)
        {
            if (_sliding) return;
            _sliding = true; _slideLeft = SLIDE_DURATION; SetHeight(SLIDE_HEIGHT);
            if (SoundManager.I != null) SoundManager.I.Play("slide");
            Feel.DustBurst(transform.position);
        }
        else { _vy = Mathf.Min(_vy, -fastFall); _pendingSlide = true; }
    }

    void EndSlide() { _sliding = false; SetHeight(STAND_HEIGHT); }

    void SetHeight(float h)
    {
        _cc.height = h;
        _cc.center = new Vector3(0f, h * 0.5f, 0f);
        if (visual != null) visual.localScale = new Vector3(1f, h / STAND_HEIGHT, 1f);
    }

    // ── Slash ─────────────────────────────────────────────────────────────────
    // Ported from player.gd try_slash (line 615).
    // Pure reach logic lives in GameCore.InSlashReach (testable without UnityEngine).

    public void TrySlash()
    {
        if (_dead || !_running) return;
        if (_slashCd > 0f) return;
        if (Game.I == null || !Game.I.Core.HasAbility("slash")) return; // realm<2 gate

        _slashCd = SLASH_COOLDOWN;
        Slashed?.Invoke(); // notify InkCultivator (and any other visual subscriber)
        Feel.SlashArc(transform.position);
        if (SoundManager.I != null) SoundManager.I.Play("slash");
        // ponytail: slash VFX arc — driven by InkCultivator via Slashed event

        float range = Game.I.Core.SlashRange;
        float tol   = Game.I.Core.SlashTol;

        // Find all Foe components (enemies tagged as killable).
        var foes = FindObjectsByType<Foe>(FindObjectsSortMode.None);
        int killed = 0;
        foreach (var foe in foes)
        {
            // Skip null, inactive GameObjects, or disabled Foe (already dying).
            if (foe == null || !foe.gameObject.activeSelf || !foe.enabled) continue;
            float ahead   = transform.position.z - foe.transform.position.z;
            float lateral = Mathf.Abs(foe.transform.position.x - transform.position.x);
            if (Tribulation.Core.GameCore.InSlashReach(ahead, lateral, range, tol))
            {
                // Trigger death anim + delayed despawn if EnemyBehavior present,
                // else fall back to immediate deactivation.
                var eb = foe.GetComponent<EnemyBehavior>();
                if (eb != null)
                    eb.Kill();
                else
                    foe.gameObject.SetActive(false); // fallback: return-to-pool immediately
                Feel.Spark(foe.transform.position + Vector3.up * 1f);
                killed++;
            }
        }

        if (killed > 0)
        {
            if (SoundManager.I != null) SoundManager.I.Play("kill");
            // Feel: hitstop + scale-pop + FOV kick + light trauma on kill.
            Feel.Hitstop(0.05f);
            FeelPop(0.30f);
            var cam = Cam();
            cam?.AddFovKick(6f);    // bumped 4→6 for a snappier kill punch
            cam?.AddTrauma(0.18f);  // light shake so the kill registers in the camera
            // Blood-Sprint: add speed boost per kill (#8).
            _surv.OnKills(killed);
            Game.I.OnEnemyKilled(killed);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (_dead || !_running) return;
        if (other.GetComponent<Hazard>() == null) return;
        if (_iframes > 0f) return; // already reeling from a hit — don't chain-spike
        // Sword-Qi Dash: plow through hazards untouched while dashing (mirrors player.gd _powerup_tick invuln).
        if (Game.I != null && Game.I.IsPowerupActive("dash")) return;
        // Iron-Body: try to absorb the hit; absorb = no Net spike, no stumble (#8).
        if (_surv.TryAbsorbHit())
        {
            // ponytail: brief flash / SFX on absorb — deferred to visual polish
            return;
        }

        // Non-lethal contact: spike the Net + stumble + i-frames instead of dying.
        // Death is the Net's job now (Net >= 1.0). A pursuer hits harder than a dumb obstacle.
        bool isEnemy = other.GetComponent<Foe>() != null;
        _iframes  = HIT_IFRAMES;
        _stumbleT = HIT_STUMBLE;
        Feel.Hitstop(0.07f);
        var cam = Cam(); if (cam != null) { cam.AddTrauma(0.35f); cam.AddFovKick(5f); }
        if (SoundManager.I != null) SoundManager.I.Play("death"); // ponytail: reuse death sfx as a hit thud until a dedicated one exists
        if (Game.I != null) Game.I.OnContactHit(isEnemy);
        // A pursuer that lands its hit is spent — play its death (collapse) instead of
        // vanishing instantly. Kill() disables its collider immediately so it can't
        // sit on you and re-spike, then despawns after the death anim.
        if (isEnemy)
        {
            var eb = other.GetComponent<EnemyBehavior>();
            if (eb != null) eb.Kill();
            else other.gameObject.SetActive(false);
        }
    }

    // Public so Spawner-pooled hazards can trigger the same path.
    public void Die()
    {
        if (_dead) return;
        _dead = true;
        _running = false; // halt forward motion on death (ResetRun re-enables it on restart)
        if (SoundManager.I != null) SoundManager.I.Play("death");
        var cam = FindObjectOfType<CameraFollow>(); if (cam != null) cam.AddTrauma(0.8f);
        // Single death route: all hazard hits flow through Game state, not GameLoop directly.
        // Game.OnCoreDied() will call GameLoop.I.OnPlayerDied() for restart support.
        if (Game.I != null) Game.I.OnPlayerHit();
        else if (GameLoop.I != null) GameLoop.I.OnPlayerDied(); // fallback if Game not present
    }

    // Called by Game.OnCoreDied when the Heavenly Net closes (the real death path now —
    // contact is non-lethal). Halts forward motion and marks dead so the rigged model
    // plays its death animation. Does NOT re-enter the core death path (Core already died).
    public void HaltForDeath()
    {
        if (_dead) return;
        _dead = true;
        _running = false;
        if (_sliding) EndSlide();
    }

    // Called by Game when an Iron Aegis talisman is picked up.
    public void GrantShield() { _surv.GrantShield(); }

    // Called by MainMenu when the player taps "Begin Cultivation".
    public void BeginRunning(float headStart = 0f) { _running = true; _runTime = headStart; }

    // Called by GameLoop on tap-to-restart.
    public void ResetRun()
    {
        _dead = false; _running = true; _runTime = 0f; _vy = 0f;
        _sliding = false; _pendingSlide = false; _lane = 1;
        _slashCd = 0f; _airJumpsUsed = 0; _iframes = 0f; _stumbleT = 0f;
        _flight.Reset();
        _surv.Reset();
        _lastRealm = -1; // force ApplyRealmStats re-apply on next Update
        // Restart from the CURRENT Z — the ground tiles have recycled down here, so
        // teleporting back to the original startZ drops us into the void they left behind.
        // Recenter to lane 0 (x=0) on the ground (y=0); distance counts from this new start.
        _cc.enabled = false;
        transform.position = new Vector3(0f, 0f, transform.position.z);
        _cc.enabled = true;
        _startZ = transform.position.z;
        SetHeight(STAND_HEIGHT);
    }

    public float GetSpeedFraction() => Mathf.Clamp01(_runTime / _rampTime);
    public int GetDistance() => (int)Mathf.Max(0f, _startZ - transform.position.z);
}
