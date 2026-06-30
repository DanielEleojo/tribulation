// Full hazard-trinity spawner (issues #3 + #4).
// Ports spawner.gd: block (JUMP), bar (SLIDE), enemy (LANE + Foe/slash target),
//   orb trail (Qi pickup), pill/talisman (powerup).
// Uses SpawnScheduler (Tribulation.Core) for timing + kind cycle. Object pooling per kind.
// Death routes through Game.I.OnPlayerHit() — the player's Die() does the same, giving ONE path.
//
// ponytail: gate spawning (gate.gd) — issue #5
// ponytail: _spawn_lightning (tribulation) — issue #7
// ponytail: _spawn_aerial (sword-flight) — issue #6
// ponytail: tier_style / hazard_style palette per realm — later visual polish issue
// ponytail: FoeScript animated bob/sway — visual polish
// ponytail: GLB enemy mesh — visual polish

using System.Collections.Generic;
using UnityEngine;
using Tribulation.Core;

public class Spawner : MonoBehaviour
{
    // ── Constants (ported from spawner.gd lines 12-29) ──────────────────────
    const float SPAWN_AHEAD   = 70f;   // how far ahead of player to place hazard (-Z)
    const float DESPAWN_BEHIND = 25f;  // cull once this far behind the player (+Z)
    const float LANE_WIDTH     = 2.5f; // must match PlayerRunner's lane spacing
    const float FULL_WIDTH     = 8.0f; // spans all three lanes

    // Aerial hazard (sword-flight, #7): lane-wide block floating at flight-band altitude.
    // ponytail: full _spawn_aerial parity (varied shapes, ring hazards) — deferred to visual polish
    const float AERIAL_Y     = 3.5f;  // mid of flight band MIN_Y=2.2..MAX_Y=6.0
    const float AERIAL_W     = 2.0f;
    const float AERIAL_H     = 1.0f;
    const float AERIAL_D     = 1.2f;
    static readonly Color AERIAL_COLOR = new Color(0.20f, 0.55f, 1.00f); // blue-white

    const float BLOCK_HEIGHT     = 1.0f;
    const float BLOCK_DEPTH      = 1.2f;
    const float BLOCK_LANE_WIDTH = 2.0f;

    const float BAR_HEIGHT     = 0.8f;
    const float BAR_DEPTH      = 0.8f;
    const float BAR_BOTTOM_Y   = 1.2f;  // bottom edge of the bar (player must duck under)
    const float BAR_LANE_WIDTH = 2.2f;

    // Enemy: tall (2.6), covers full height — can't jump or slide over, must lane-dodge.
    static readonly Vector3 ENEMY_SIZE = new Vector3(0.95f, 2.6f, 0.95f);

    // Orb (Qi pickup): small sphere floating at chest height.
    const float ORB_RADIUS = 0.4f;
    const float ORB_Y      = 1.2f;  // center height
    const int   ORB_TRAIL  = 5;     // orbs per trail (game.gd _spawn_orb_trail)
    const float ORB_SPACING = 3.5f; // Z gap between orbs in a trail

    // Pill (powerup): slightly larger pickup at mid-height.
    const float PILL_SIZE  = 0.55f;
    const float PILL_Y     = 1.2f;

    // Magnet pull parameters.
    const float MAGNET_RADIUS = 8f;
    const float MAGNET_EASE   = 6f; // lerp speed (units/s feels)

    // Colors (ported from spawner.gd + game.gd POWERUPS colors)
    // Block → amber qi-strike; Bar → cyan blade (telegraph read: amber=jump, cyan=slide)
    static readonly Color BLOCK_COLOR = new Color(1.00f, 0.68f, 0.20f); // amber
    static readonly Color BAR_COLOR   = new Color(0.25f, 0.80f, 0.95f); // cyan
    static readonly Color ENEMY_COLOR = new Color(0.75f, 0.12f, 0.16f);
    static readonly Color ORB_COLOR   = new Color(1.00f, 0.82f, 0.20f);  // warm gold qi — distinct from amber hazard (1,0.68,0.2) and cyan hazard (0.25,0.8,0.95)
    static readonly Color PILL_COLOR  = new Color(0.90f, 0.70f, 0.95f);  // pale purple pill

    // Pill id rotation (game.gd picks randomly from available powerups).
    static readonly string[] PillIds = { "magnet", "double", "dash", "surge" };

    // ── State ────────────────────────────────────────────────────────────────
    SpawnScheduler _sched;
    Transform _player;
    float _elapsed;
    float _timer;
    float _orbTimer;
    float _pillTimer;

    // Gate spawn (issue #5): independent timer, no pool (low frequency — plain Instantiate/Destroy)
    float _gateTimer;
    // Gate prefab built in code — no asset needed.
    readonly List<GameObject> _liveGates = new List<GameObject>();

    // Object pools keyed by HazardKind
    readonly Dictionary<HazardKind, Stack<GameObject>> _pool
        = new Dictionary<HazardKind, Stack<GameObject>>();

    // All currently live hazard objects (for culling)
    readonly List<(GameObject go, HazardKind kind)> _live
        = new List<(GameObject, HazardKind)>();

    // Live orbs tracked separately for magnet pull + culling
    readonly List<GameObject> _liveOrbs  = new List<GameObject>();
    readonly Stack<GameObject> _orbPool  = new Stack<GameObject>();

    // Live pills tracked for culling
    readonly List<(GameObject go, string id)> _livePills = new List<(GameObject, string)>();
    readonly Stack<GameObject> _pillPool = new Stack<GameObject>();

    // Cached PlayerRunner reference (for IsFlying check).
    PlayerRunner _playerRunner;

    // Lightning bolt: tall glowing white-blue column (Heavenly Tribulation, issue #7)
    static readonly Color LIGHTNING_COLOR = new Color(0.75f, 0.88f, 1.0f); // white-blue
    const float BOLT_W  = 0.7f;
    const float BOLT_H  = 7.0f;
    const float BOLT_D  = 0.7f;

    // Shared materials (one per kind, created once)
    Material _matBlock, _matBar, _matEnemy, _matOrb, _matPill, _matAerial, _matLightning;

    // ── Lifecycle ────────────────────────────────────────────────────────────
    void Start()
    {
        _sched     = new SpawnScheduler(Balance.D);
        _timer     = Balance.D.spawn_start_interval;
        _orbTimer  = Balance.D.spawn_orb_interval;
        _pillTimer = Balance.D.spawn_pill_interval;
        _gateTimer = Balance.D.spawn_gate_interval;

        _matBlock     = MakeGlowMat(BLOCK_COLOR);
        _matBar       = MakeGlowMat(BAR_COLOR);
        _matEnemy     = MakeMat(ENEMY_COLOR); // fallback only; mesh hidden when soldier loads
        _matOrb       = MakeOrbMat(ORB_COLOR); // warm-glow (1.4× emission) — distinct from 1.8× hazard glow
        _matPill      = MakeMat(PILL_COLOR);
        _matAerial    = MakeMat(AERIAL_COLOR);
        _matLightning = MakeGlowMat(LIGHTNING_COLOR); // white-blue hot glow for tribulation bolts
    }

    void Update()
    {
        if (_player == null)
        {
            _playerRunner = FindObjectOfType<PlayerRunner>();
            if (_playerRunner == null) return;
            _player = _playerRunner.transform;
        }

        // Don't spawn if Game says dead / not started.
        if (Game.I != null && (Game.I.Core == null || !Game.I.Core.IsStarted || Game.I.Core.IsDead))
            return;

        _elapsed   += Time.deltaTime;
        _timer     -= Time.deltaTime;
        _orbTimer  -= Time.deltaTime;
        _pillTimer -= Time.deltaTime;
        _gateTimer -= Time.deltaTime;

        if (_timer <= 0f)
        {
            Spawn();
            _timer = _sched.CurrentInterval(_elapsed);
        }

        if (_orbTimer <= 0f)
        {
            SpawnOrbTrail(_player.position.z - SPAWN_AHEAD, Random.Range(0, 3));
            _orbTimer = Balance.D.spawn_orb_interval;
        }

        if (_pillTimer <= 0f)
        {
            SpawnPill(_player.position.z - SPAWN_AHEAD, Random.Range(0, 3));
            _pillTimer = Balance.D.spawn_pill_interval;
        }

        if (_gateTimer <= 0f)
        {
            SpawnGate(_player.position.z - SPAWN_AHEAD);
            _gateTimer = Balance.D.spawn_gate_interval;
        }

        MagnetPull();
        CullOrbs();
        CullPills();
        CullGates();
        Cull();
    }

    // ── Spawn ────────────────────────────────────────────────────────────────
    void Spawn()
    {
        float z = _player.position.z - SPAWN_AHEAD;

        // ── Lightning (Heavenly Tribulation) — Godot branch order: checked FIRST ──
        // During active tribulation, replaces all normal spawns.
        // At Ascension (realm≥5), also fires probabilistically (55% chance).
        // Mirrors spawner.gd: if game.in_tribulation() → _spawn_lightning(z) and return.
        var core = Game.I?.Core;
        if (core != null && SpawnScheduler.ShouldStrikeLightning(
                core.InTribulation,
                core.HasAbility("tribulation"),
                Random.value))
        {
            SpawnLightning(z);
            return;
        }

        // While flying, swap in aerial hazards instead of the ground trinity.
        // Mirrors spawner.gd _spawn_aerial branch. ponytail: full parity deferred.
        if (_playerRunner != null && _playerRunner.IsFlying)
        {
            SpawnAerial(z);
            return;
        }

        int realm = Game.I?.Core?.Realm ?? 0;
        HazardStep step = _sched.NextStep(realm);

        switch (step.Kind)
        {
            case HazardKind.Block: SpawnBlock(z, step.Lane, step.FullWidth); break;
            case HazardKind.Bar:   SpawnBar(z,   step.Lane, step.FullWidth); break;
            case HazardKind.Enemy: SpawnEnemy(z, step.Lane);                 break;
        }
    }

    // Lightning (Heavenly Tribulation, issue #7).
    // Picks one random safe lane; spawns a lethal tall bolt column in EACH of the other two.
    // Mirrors spawner.gd _spawn_lightning(z): safe = randi()%3, bolt in every lane != safe.
    // Bolt geometry: (0.7, 7.0, 0.7), centerY = 7.0*0.5 = 3.5 — full-height lethal column.
    // Collision/death: Hazard component + trigger → PlayerRunner.OnTriggerEnter → TryAbsorbHit.
    // Pool-safe: uses existing Acquire/Apply/Track with HazardKind.Block (no new kind needed).
    void SpawnLightning(float z)
    {
        int safe    = Random.Range(0, 3);
        float cy    = BOLT_H * 0.5f; // center Y = 3.5
        var   size  = new Vector3(BOLT_W, BOLT_H, BOLT_D);

        foreach (int lane in SpawnScheduler.StrikeLanes(safe))
        {
            float x = LaneX(lane);
            var go  = Acquire(HazardKind.Block, size, cy);
            go.transform.position = new Vector3(x, cy, z);
            Apply(go, _matLightning);
            Track(go, HazardKind.Block);
            // Telegraph omitted: tribulation is already a screen-shaking alarmed state.
        }
    }

    // Aerial hazard: lane block at flight-band altitude. Player must change lanes or slash.
    // ponytail: varied aerial shapes, ring-style hazards — deferred to visual polish (#7+)
    void SpawnAerial(float z)
    {
        int lane = Random.Range(0, 3);
        float x  = LaneX(lane);
        float cy = AERIAL_Y;
        var go   = Acquire(HazardKind.Block, new Vector3(AERIAL_W, AERIAL_H, AERIAL_D), cy);
        go.transform.position = new Vector3(x, cy, z);
        Apply(go, _matAerial);
        Track(go, HazardKind.Block);
        // ponytail: aerial telegraph deferred (rare flight band)
    }

    // Block: ground-level box. Cleared by JUMP (arc over top).
    // Lane and fullWidth come from the authored HazardStep (no local RNG for kind/lane).
    //
    // Geometry clearance: BLOCK_HEIGHT = 1.0 → top of block at Y=1.0.
    // PlayerRunner STAND_HEIGHT=2 → CharacterController bottom at Y=0, top at Y=2.
    // jumpVelocity=17, gravity=48 → peak ≈ 17²/(2*48) ≈ 3.0m above ground.
    // The player's feet clear a 1.0m block easily at peak. ✓
    void SpawnBlock(float z, int lane, bool fullWidth)
    {
        float w = fullWidth ? FULL_WIDTH : BLOCK_LANE_WIDTH;
        float x = fullWidth ? 0f : LaneX(lane);
        // Y center: block sits on the ground — bottom at Y=0, center at BLOCK_HEIGHT*0.5.
        float cy = BLOCK_HEIGHT * 0.5f;
        var go = Acquire(HazardKind.Block, new Vector3(w, BLOCK_HEIGHT, BLOCK_DEPTH), cy);
        go.transform.position = new Vector3(x, cy, z);
        Apply(go, _matBlock);
        Track(go, HazardKind.Block);
        TelegraphSystem.I?.Attach(go.transform, HazardKind.Block, w);
    }

    // Bar: overhead beam. Cleared by SLIDE (crouch under).
    // Lane and fullWidth come from the authored HazardStep (no local RNG for kind/lane).
    // Bottom of beam at BAR_BOTTOM_Y=1.2. Player SLIDE_HEIGHT=1.0 → CharacterController
    // top at Y=1.0 < 1.2 → clears the beam bottom. Standing (STAND_HEIGHT=2) → top at
    // Y=2.0 > 1.2 → collides. The clearance comes from geometry + CC height. ✓
    void SpawnBar(float z, int lane, bool fullWidth)
    {
        float w = fullWidth ? FULL_WIDTH : BAR_LANE_WIDTH;
        float x = fullWidth ? 0f : LaneX(lane);
        // Center Y: BAR_BOTTOM_Y + BAR_HEIGHT*0.5
        float cy = BAR_BOTTOM_Y + BAR_HEIGHT * 0.5f;
        var go = Acquire(HazardKind.Bar, new Vector3(w, BAR_HEIGHT, BAR_DEPTH), cy);
        go.transform.position = new Vector3(x, cy, z);
        Apply(go, _matBar);
        Track(go, HazardKind.Bar);
        TelegraphSystem.I?.Attach(go.transform, HazardKind.Bar, w);
    }

    // Enemy: tall figure in one lane. Cleared by LANE change only (or slash at realm>=2).
    // Lane comes from the authored HazardStep (no local RNG for lane selection).
    // ENEMY_SIZE.y = 2.6 → taller than jump arc peak AND taller than standing player (2.0).
    // Can't jump over (peak ~3m, but enemy is 2.6m tall and centered at Y=1.3, so top at
    // Y=2.6 which the player's CC at Y=2 would still hit mid-arc) — must dodge lane. ✓
    // Can't slide under: bottom at Y=0, fully covers SLIDE_HEIGHT=1.0. ✓
    // Foe component marks it as slashable once realm>=2.
    void SpawnEnemy(float z, int lane)
    {
        float x = LaneX(lane);
        float cy = ENEMY_SIZE.y * 0.5f;
        var go = Acquire(HazardKind.Enemy, ENEMY_SIZE, cy);
        go.transform.position = new Vector3(x, cy, z);
        // Add Foe marker so TrySlash can find it (idempotent — AddComponent skips if present).
        if (go.GetComponent<Foe>() == null) go.AddComponent<Foe>();
        EnsureEnemyVisual(go);
        var eb = go.GetComponent<EnemyBehavior>() ?? go.AddComponent<EnemyBehavior>();
        eb.Activate(_player, Game.I?.Core?.Realm ?? 0);
        Track(go, HazardKind.Enemy);
        TelegraphSystem.I?.Attach(go.transform, HazardKind.Enemy, ENEMY_SIZE.x * 2f); // ~1.9
    }

    // ── Pool helpers ─────────────────────────────────────────────────────────

    // Take from pool or build a new trigger box.
    // The collider size is set from 'size'; center Y is set on the transform.
    GameObject Acquire(HazardKind kind, Vector3 size, float centerY)
    {
        if (!_pool.ContainsKey(kind)) _pool[kind] = new Stack<GameObject>();
        var stack = _pool[kind];

        GameObject go = null;
        while (stack.Count > 0 && go == null)
        {
            go = stack.Pop();
            if (go == null) go = null; // destroyed externally — skip
        }

        if (go == null)
        {
            // note: primitive cube ships a unit mesh + BoxCollider + MeshRenderer.
            // Scale the transform per-spawn instead of hand-building/resizing a mesh.
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Haz_{kind}";
            go.transform.SetParent(transform, false);
            go.GetComponent<Collider>().isTrigger = true; // sense-only, no physics push
            go.AddComponent<Hazard>();
        }

        go.transform.localScale = size; // collider + mesh scale together
        go.SetActive(true);
        return go;
    }

    void Release(GameObject go, HazardKind kind)
    {
        if (go == null) return;
        go.SetActive(false);
        if (!_pool.ContainsKey(kind)) _pool[kind] = new Stack<GameObject>();
        _pool[kind].Push(go);
    }

    void Track(GameObject go, HazardKind kind)
    {
        _live.Add((go, kind));
    }

    // ── Cull ────────────────────────────────────────────────────────────────
    void Cull()
    {
        float killZ = _player.position.z + DESPAWN_BEHIND;
        for (int i = _live.Count - 1; i >= 0; i--)
        {
            var (go, kind) = _live[i];
            if (go == null) { _live.RemoveAt(i); continue; }
            if (go.transform.position.z > killZ)
            {
                _live.RemoveAt(i);
                Release(go, kind);
            }
        }
    }

    // ── Orb trail ────────────────────────────────────────────────────────────
    // Ported from game.gd _spawn_orb_trail: a line of ORB_TRAIL orbs in one lane.

    void SpawnOrbTrail(float leadZ, int lane)
    {
        float x = LaneX(lane);
        for (int i = 0; i < ORB_TRAIL; i++)
        {
            float z = leadZ - i * ORB_SPACING;
            var go = AcquireOrb();
            go.transform.position = new Vector3(x, ORB_Y, z);
            // Phase-offset each orb by index so the trail ripples like a wave (not lockstep).
            var visual = go.GetComponent<OrbVisual>();
            if (visual != null) visual.Init(i * 1.2f); // ~1.2 rad apart over 5 orbs ≈ one full wave
            go.SetActive(true);
            _liveOrbs.Add(go);
        }
    }

    GameObject AcquireOrb()
    {
        while (_orbPool.Count > 0)
        {
            var c = _orbPool.Pop();
            if (c != null) return c;
        }
        // Create a small sphere trigger with OrbPickup component.
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Orb";
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * (ORB_RADIUS * 2f);
        go.GetComponent<Collider>().isTrigger = true;
        go.AddComponent<OrbPickup>();
        go.AddComponent<OrbVisual>(); // idle bob + halo; Init() called from SpawnOrbTrail after position set
        Apply(go, _matOrb);
        return go;
    }

    void CullOrbs()
    {
        float killZ = _player.position.z + DESPAWN_BEHIND;
        for (int i = _liveOrbs.Count - 1; i >= 0; i--)
        {
            var go = _liveOrbs[i];
            if (go == null) { _liveOrbs.RemoveAt(i); continue; }
            // Also cull if deactivated by OrbPickup.OnTriggerEnter (collected).
            if (!go.activeSelf || go.transform.position.z > killZ)
            {
                go.SetActive(false);
                _orbPool.Push(go);
                _liveOrbs.RemoveAt(i);
            }
        }
    }

    // ── Pill / talisman ──────────────────────────────────────────────────────
    // Ported from game.gd activate_powerup spawner: picks a random id from PillIds.

    void SpawnPill(float z, int lane)
    {
        string id = PillIds[Random.Range(0, PillIds.Length)];
        float x = LaneX(lane);
        var go = AcquirePill();
        go.transform.position = new Vector3(x, PILL_Y, z);
        go.GetComponent<PillPickup>().PowerupId = id;
        go.SetActive(true);
        _livePills.Add((go, id));
    }

    GameObject AcquirePill()
    {
        while (_pillPool.Count > 0)
        {
            var c = _pillPool.Pop();
            if (c != null) return c;
        }
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Pill";
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * PILL_SIZE;
        go.GetComponent<Collider>().isTrigger = true;
        if (go.GetComponent<PillPickup>() == null) go.AddComponent<PillPickup>();
        Apply(go, _matPill);
        return go;
    }

    void CullPills()
    {
        float killZ = _player.position.z + DESPAWN_BEHIND;
        for (int i = _livePills.Count - 1; i >= 0; i--)
        {
            var (go, _) = _livePills[i];
            if (go == null) { _livePills.RemoveAt(i); continue; }
            if (!go.activeSelf || go.transform.position.z > killZ)
            {
                go.SetActive(false);
                _pillPool.Push(go);
                _livePills.RemoveAt(i);
            }
        }
    }

    // ── Gate (issue #5) ─────────────────────────────────────────────────────
    // Low-frequency (11s); plain Instantiate/Destroy, no pool. Gate.cs self-destructs
    // on resolution. Spawner just culls leftovers that passed the player unresolved.

    void SpawnGate(float z)
    {
        int safeLane = Random.Range(0, 3);
        var go = new GameObject("Gate");
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(0f, 0f, z);
        var gate = go.AddComponent<Gate>();
        gate.Setup(safeLane);
        _liveGates.Add(go);
    }

    void CullGates()
    {
        float killZ = _player.position.z + DESPAWN_BEHIND;
        for (int i = _liveGates.Count - 1; i >= 0; i--)
        {
            var go = _liveGates[i];
            if (go == null) { _liveGates.RemoveAt(i); continue; }
            if (go.transform.position.z > killZ)
            {
                _liveGates.RemoveAt(i);
                Destroy(go);
            }
        }
    }

    // ── Magnet pull ──────────────────────────────────────────────────────────
    // While magnet powerup is active, live orbs within MAGNET_RADIUS ease toward the player.
    // Ported from game.gd / player.gd magnet behaviour (powerup active-flag).

    void MagnetPull()
    {
        if (Game.I == null || !Game.I.IsPowerupActive("magnet")) return;
        Vector3 pPos = _player.position;
        float sqr = MAGNET_RADIUS * MAGNET_RADIUS;
        foreach (var orb in _liveOrbs)
        {
            if (orb == null || !orb.activeSelf) continue;
            if ((orb.transform.position - pPos).sqrMagnitude <= sqr)
                orb.transform.position = Vector3.MoveTowards(
                    orb.transform.position, pPos, MAGNET_EASE * Time.deltaTime);
        }
    }

    // ── Utility ─────────────────────────────────────────────────────────────

    // Lane X mapping mirrors PlayerRunner: -(lane-1)*LANE_WIDTH
    // (cam faces -Z, Unity left-handed, +X = screen-left → negate so lanes align)
    static float LaneX(int lane) => -(lane - 1) * LANE_WIDTH;

    Material MakeMat(Color c)
    {
        var sh  = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(sh) { color = c };
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", c * 0.4f);
        return mat;
    }

    // Brighter glow version for qi-strike (block) and blade (bar) hazards.
    // Full-intensity emission so the shape reads as energy rather than matte geometry.
    Material MakeGlowMat(Color c)
    {
        var sh  = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(sh);
        mat.SetColor("_BaseColor", c);
        mat.color = c;
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", c * 1.8f); // hot glow — multiply above 1 for HDR bloom
        return mat;
    }

    // Orb-specific glow: 1.4× emission so bloom is present but orbs don't saturate into a white blob.
    // Hazards use 1.8× — orbs intentionally glow softer so the danger/reward read is different.
    Material MakeOrbMat(Color c)
    {
        var sh  = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(sh);
        mat.SetColor("_BaseColor", c);
        mat.color = c;
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", c * 1.4f);
        return mat;
    }

    void Apply(GameObject go, Material mat)
    {
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null) mr.sharedMaterial = mat;
    }

    // ── Enemy visual helper ──────────────────────────────────────────────────
    // Attaches a Humanoid soldier model to the enemy hazard cube.
    // Pool-safe: only runs once per pooled object (guarded by EnemyVisualHolder child).
    // Does NOT change the collider, scale, Hazard, or Foe components on 'go'.
    void EnsureEnemyVisual(GameObject go)
    {
        // Pool-safety guard: if the holder already exists, the visual is already built.
        if (go.transform.Find("EnemyVisualHolder") != null) return;

        // Load assets from Resources.
        var soldierPrefab = Resources.Load<GameObject>("Char/Solider_Ssanggeom");
        var attackCtrl    = Resources.Load<RuntimeAnimatorController>("Anim/EnemyAttack");

        if (soldierPrefab == null)
        {
            // Fallback: keep the cube's MeshRenderer visible with _matEnemy.
            Apply(go, _matEnemy);
            return;
        }

        // Hide the cube's own mesh — collider stays active for gameplay.
        var cubeMR = go.GetComponent<MeshRenderer>();
        if (cubeMR != null) cubeMR.enabled = false;

        // Create a scale-cancel holder child so the soldier is NOT stretched by ENEMY_SIZE.
        // go.localScale = ENEMY_SIZE = (0.95, 2.6, 0.95). The holder inverts that so its
        // children live in undistorted unit world-scale.
        var holder = new GameObject("EnemyVisualHolder");
        holder.transform.SetParent(go.transform, false);
        holder.transform.localPosition = Vector3.zero;
        holder.transform.localRotation = Quaternion.identity;
        holder.transform.localScale = new Vector3(
            1f / ENEMY_SIZE.x,
            1f / ENEMY_SIZE.y,
            1f / ENEMY_SIZE.z);

        // Instantiate the soldier under the holder (now in unit space).
        var soldier = Instantiate(soldierPrefab, holder.transform);
        soldier.transform.localPosition = Vector3.zero;
        soldier.transform.localRotation = Quaternion.identity;
        soldier.transform.localScale    = Vector3.one;

        // Strip any colliders inside the soldier so only the cube collider governs hits.
        foreach (var col in soldier.GetComponentsInChildren<Collider>())
            Object.Destroy(col);

        // Measure the soldier's skinned mesh height and normalize to ~2.2 units.
        // In the holder's unit space the cube spans y = -0.5 .. +0.5 (local).
        // We want the soldier standing tall at ~2.2 units so it fills that visual space.
        var smrs = soldier.GetComponentsInChildren<SkinnedMeshRenderer>();
        if (smrs != null && smrs.Length > 0)
        {
            Bounds combined = smrs[0].bounds;
            for (int i = 1; i < smrs.Length; i++)
                combined.Encapsulate(smrs[i].bounds);

            float currentHeight = combined.size.y;
            if (currentHeight > 0.001f)
            {
                float targetHeight = 2.2f;
                float uniformScale = targetHeight / currentHeight;
                soldier.transform.localScale = Vector3.one * uniformScale;
            }
        }

        // Position soldier so its feet sit at the bottom of the collider box.
        // In the holder's unit space the cube bottom is at local y = -0.5.
        // Re-measure bounds after scaling to find the foot offset.
        var smrs2 = soldier.GetComponentsInChildren<SkinnedMeshRenderer>();
        if (smrs2 != null && smrs2.Length > 0)
        {
            // We need world-space bounds relative to the holder; soldier is a child of holder.
            // soldier.transform.localPosition is still (0,0,0) at this point.
            // Gather bounds in holder-local space by reading SkinnedMeshRenderer.bounds
            // (world space), then converting to holder-local.
            Bounds b2 = smrs2[0].bounds;
            for (int i = 1; i < smrs2.Length; i++)
                b2.Encapsulate(smrs2[i].bounds);

            // b2.min.y is the world-space foot position (holder has no world-space rotation).
            // The holder-local y = -0.5 corresponds to world y = holderWorldY - 0.5
            //   (holder.localScale is ~1 in world since go absorbs the non-uniform scale
            //    and the holder cancels it back to unit scale; but holder.transform.position
            //    equals go.transform.position in world space).
            // We want soldier feet (b2.min.y) to land at holderWorld y - 0.5.
            float holderWorldY = holder.transform.position.y;
            float targetFeetWorldY = holderWorldY - 0.5f;
            float footDeltaY = targetFeetWorldY - b2.min.y;
            // Convert world delta to soldier local (soldier is child of holder, unit scale).
            soldier.transform.localPosition = new Vector3(0f, footDeltaY, 0f);
        }

        // Face the soldier toward the oncoming player.
        // Player runs toward -Z and approaches from +Z, so the enemy should face +Z.
        // Default prefab forward is +Z; if it ends up facing away, flip 180 on Y.
        soldier.transform.localRotation = Quaternion.identity; // face +Z, toward the oncoming player

        // Wire up the looping attack animation.
        var anim = soldier.GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.applyRootMotion = false;
            if (attackCtrl != null)
                anim.runtimeAnimatorController = attackCtrl;
        }
    }

    /// <summary>
    /// Read-only view of all currently live hazard objects and their kinds.
    /// Used by CoachMarks to determine which lessons to surface.
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<(GameObject go, HazardKind kind)> LiveHazards => _live;

    // Called by Game.BeginRun() to seed difficulty elapsed for the current realm.
    public void BeginRun(float off) { _elapsed = off; }

    // Called by GameLoop on restart.
    public void ClearAll()
    {
        foreach (var (go, kind) in _live)
            if (go != null) Release(go, kind);
        _live.Clear();

        foreach (var go in _liveOrbs)
            if (go != null) { go.SetActive(false); _orbPool.Push(go); }
        _liveOrbs.Clear();

        foreach (var (go, _) in _livePills)
            if (go != null) { go.SetActive(false); _pillPool.Push(go); }
        _livePills.Clear();

        foreach (var go in _liveGates)
            if (go != null) Destroy(go);
        _liveGates.Clear();

        TelegraphSystem.I?.ClearAll();

        _elapsed   = 0f;
        _timer     = Balance.D.spawn_start_interval;
        _orbTimer  = Balance.D.spawn_orb_interval;
        _pillTimer = Balance.D.spawn_pill_interval;
        _gateTimer = Balance.D.spawn_gate_interval;
        _sched     = new SpawnScheduler(Balance.D);
    }
}
