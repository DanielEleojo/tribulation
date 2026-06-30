using UnityEngine;

// Tracer-bullet port of player.gd — CORE MOTION ONLY (powerups, glide, sword-flight,
// dread form, particles, slash, model/anim all deferred to Phase 3). Auto-runs forward
// (-Z) with a speed ramp + endless creep, eases between 3 lanes, jumps (buffer + coyote),
// and slides (ground crouch / mid-air fast-fall). Uses a CharacterController.
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
    const float LANE_SHARPNESS = 12f;
    const float MAX_LANE_SPEED = 18f;
    const float STAND_HEIGHT = 2f;
    const float SLIDE_HEIGHT = 1f;
    const float SLIDE_DURATION = 0.65f;
    const float JUMP_BUFFER = 0.12f;
    const float COYOTE = 0.10f;
    const float GROUND_STICK = 2f;

    // pulled from Balance in Awake
    float _baseSpeed = 12f, _maxSpeed = 22f, _rampTime = 90f, _creep = 0.07f, _creepCap = 16f;

    CharacterController _cc;
    int _lane = 1;                 // 0 left, 1 center, 2 right
    float _runSpeed, _runTime, _startZ, _vy;
    bool _sliding, _pendingSlide, _wasGrounded, _dead, _running;
    float _slideLeft, _jumpBuf, _coyote;

    public bool IsDead => _dead;

    void Awake()
    {
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
        }
        _running = true; // tracer bullet has no title screen — start immediately
    }

    void Update()
    {
        if (_dead) return;

        if (!_running) { _vy -= gravity * Time.deltaTime; _cc.Move(Vector3.up * _vy * Time.deltaTime); if (_cc.isGrounded) _vy = -GROUND_STICK; return; }

        // Keyboard (desktop testing).
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) MoveLeft();
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) MoveRight();
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow)) TryJump();
        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) StartSlide();

        bool grounded = _cc.isGrounded;

        // Jump buffer + coyote time.
        _coyote = grounded ? COYOTE : Mathf.Max(0f, _coyote - Time.deltaTime);
        if (_jumpBuf > 0f) _jumpBuf -= Time.deltaTime;
        if (_jumpBuf > 0f && _coyote > 0f)
        {
            if (_sliding) EndSlide();
            _vy = jumpVelocity; _coyote = 0f; _jumpBuf = 0f; _pendingSlide = false;
        }

        // A queued fast-fall slide fires on landing.
        if (grounded && !_wasGrounded && _pendingSlide) { _pendingSlide = false; StartSlide(); }
        if (_sliding) { _slideLeft -= Time.deltaTime; if (_slideLeft <= 0f) EndSlide(); }
        _wasGrounded = grounded;

        // Forward run: ramp to max over time, then bounded endless creep.
        _runTime += Time.deltaTime;
        float ramp = Mathf.Clamp01(_runTime / _rampTime);
        _runSpeed = Mathf.Lerp(_baseSpeed, _maxSpeed, ramp) + EndlessCreep();

        // Ease sideways toward the target lane.
        float targetX = (_lane - 1) * LANE_WIDTH;
        float vx = Mathf.Clamp((targetX - transform.position.x) * LANE_SHARPNESS, -MAX_LANE_SPEED, MAX_LANE_SPEED);

        // Gravity (with ground-stick so isGrounded stays reliable).
        if (grounded && _vy < 0f) _vy = -GROUND_STICK;
        _vy -= gravity * Time.deltaTime;

        _cc.Move(new Vector3(vx, _vy, -_runSpeed) * Time.deltaTime);
    }

    float EndlessCreep() => Mathf.Clamp((_runTime - _rampTime) * _creep, 0f, _creepCap);

    public void MoveLeft() { if (!_dead) _lane = Mathf.Max(0, _lane - 1); }
    public void MoveRight() { if (!_dead) _lane = Mathf.Min(LANE_COUNT - 1, _lane + 1); }
    public void TryJump() { if (!_dead) _jumpBuf = JUMP_BUFFER; }

    public void StartSlide()
    {
        if (_dead) return;
        if (_cc.isGrounded)
        {
            if (_sliding) return;
            _sliding = true; _slideLeft = SLIDE_DURATION; SetHeight(SLIDE_HEIGHT);
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

    void OnTriggerEnter(Collider other)
    {
        if (!_dead && _running && other.GetComponent<Hazard>() != null) Die();
    }

    void Die()
    {
        _dead = true;
        var cam = FindObjectOfType<CameraFollow>(); if (cam != null) cam.AddTrauma(0.8f);
        if (GameLoop.I != null) GameLoop.I.OnPlayerDied();
    }

    // Called by GameLoop on tap-to-restart.
    public void ResetRun()
    {
        _dead = false; _running = true; _runTime = 0f; _vy = 0f;
        _sliding = false; _pendingSlide = false; _lane = 1;
        _cc.enabled = false; transform.position = new Vector3(0f, 0f, _startZ); _cc.enabled = true;
        SetHeight(STAND_HEIGHT);
    }

    public float GetSpeedFraction() => Mathf.Clamp01(_runTime / _rampTime);
    public int GetDistance() => (int)Mathf.Max(0f, _startZ - transform.position.z);
}
