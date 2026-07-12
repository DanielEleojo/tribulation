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
    const float SPAWN_AHEAD   = 105f;  // how far ahead of player to place hazard (-Z).
                                       // Beyond the fog wall (expSq density 0.018 ≈ 97%
                                       // fogged at 105) so spawns fade in with distance
                                       // instead of popping into view at 70.
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

    // Near-miss reward (positive-milestone juice): the lateral collision envelope is the
    // enemy's real half-width (ENEMY_SIZE.x/2 = 0.475) + the player's CharacterController
    // radius (0.4, set in Bootstrap) = 0.875. A pass with gap in (0.875 .. 0.875+band]
    // is "cleared it, but barely" — band 1.0 lets a one-lane-late dodge (gap up to ~1.9,
    // still mid-ease toward the next lane at LANE_WIDTH 2.5) count.
    const float PLAYER_RADIUS       = 0.4f; // mirrors Bootstrap's cc.radius
    static readonly float NEAR_MISS_HALF_SUM = ENEMY_SIZE.x * 0.5f + PLAYER_RADIUS; // 0.875
    const float NEAR_MISS_BAND      = 1.0f;

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
        NearMissScan(); // before Cull so a just-passed hazard is still in _live
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
        // Apply sets the cube MeshRenderer material; EnsureBlockVisual hides it and shows crescent.
        Apply(go, _matBlock);
        EnsureBlockVisual(go, w, BLOCK_HEIGHT, BLOCK_DEPTH);
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
        // Apply sets the cube MeshRenderer material; EnsureBarVisual hides it and shows slash blade.
        Apply(go, _matBar);
        EnsureBarVisual(go, w, BAR_HEIGHT, BAR_DEPTH);
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

    // ── Near-miss scan ──────────────────────────────────────────────────────
    // A lane Enemy that passes the player untouched by a whisker is a skillful dodge —
    // reward it (Game.OnNearMiss) exactly once. Scoped to HazardKind.Enemy only:
    // full-width bars/blocks are vertical-timing dodges (out of scope), and lightning/
    // aerial hazards are tracked as Block so the kind filter excludes them for free.
    // Allocation-free: index loop over _live, tuple deconstruction is a struct copy,
    // GetComponent allocates nothing (a passed enemy costs one GetComponent+flag-read
    // per frame until culled). NearChecked latches on the first evaluation after the
    // pass, hit or miss, so the reward fires at most once per hazard life.
    void NearMissScan()
    {
        float playerZ = _player.position.z;
        float playerX = _player.position.x;
        for (int i = 0; i < _live.Count; i++)
        {
            var (go, kind) = _live[i];
            if (kind != HazardKind.Enemy) continue;
            if (go == null || !go.activeSelf) continue;
            if (go.transform.position.z <= playerZ) continue; // still ahead — hasn't passed yet

            var hz = go.GetComponent<Hazard>();
            if (hz == null || hz.NearChecked) continue;
            hz.NearChecked = true; // verdict is final: at most one evaluation per life

            // A slashed / spent enemy isn't a dodge (EnemyBehavior.Kill disables Foe).
            var foe = go.GetComponent<Foe>();
            if (foe == null || !foe.enabled) continue;

            // No reward while invulnerable: reeling from a hit, or dashing through.
            if (_playerRunner != null && _playerRunner.InIFrames) continue;
            if (Game.I != null && Game.I.IsPowerupActive("dash")) continue;

            float gap = Mathf.Abs(go.transform.position.x - playerX);
            if (NearMiss.IsNearMiss(gap, NEAR_MISS_HALF_SUM, NEAR_MISS_BAND))
            {
                if (Game.I != null) Game.I.OnNearMiss();
            }
        }
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

    static bool _loggedOrbState; // one-time device diagnostic (visible in Xcode console)

    void SpawnOrbTrail(float leadZ, int lane)
    {
        float x = LaneX(lane);
        for (int i = 0; i < ORB_TRAIL; i++)
        {
            float z = leadZ - i * ORB_SPACING;
            var go = AcquireOrb();
            go.transform.position = new Vector3(x, ORB_Y, z);
            // Phase-offset each orb by index so the trail ripples like a wave (not lockstep).
            // Guarded: a failure inside the visual/halo setup must never abort the trail
            // or leave orbs inactive (invisible coins on device, run unwinnable).
            try
            {
                var visual = go.GetComponent<OrbVisual>();
                if (visual != null) visual.Init(i * 1.2f); // ~1.2 rad apart over 5 orbs ≈ one full wave
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[Spawner] OrbVisual.Init failed — orb spawns without halo. " + ex);
            }
            go.SetActive(true);
            _liveOrbs.Add(go);

            if (!_loggedOrbState)
            {
                _loggedOrbState = true;
                var mf = go.GetComponent<MeshFilter>();
                var mr = go.GetComponent<MeshRenderer>();
                Debug.Log("[Spawner] first orb: pos=" + go.transform.position
                    + " activeInHierarchy=" + go.activeInHierarchy
                    + " mesh=" + (mf != null && mf.sharedMesh != null ? mf.sharedMesh.name + "(" + mf.sharedMesh.vertexCount + "v)" : "NULL")
                    + " rendererEnabled=" + (mr != null && mr.enabled)
                    + " shader=" + (mr != null && mr.sharedMaterial != null && mr.sharedMaterial.shader != null ? mr.sharedMaterial.shader.name : "NULL")
                    + " scale=" + go.transform.localScale.x.ToString("F2"));
            }
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
        // Device safety: orbs are the only gameplay use of the built-in sphere mesh;
        // if a player build stripped it, fall back to the cube mesh (hazards/pills
        // prove it ships) — coins must never be invisible-but-collectible.
        var meshFilter = go.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh == null)
        {
            Debug.LogWarning("[Spawner] built-in sphere mesh missing from build — orb falls back to cube visual.");
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            meshFilter.sharedMesh = cube.GetComponent<MeshFilter>().sharedMesh;
            Destroy(cube);
        }
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

        // A prefab whose source model files aren't in the project still loads, but every
        // SkinnedMeshRenderer has a null sharedMesh — instantiating it would hide the cube
        // and render nothing, leaving an invisible enemy. Treat it the same as missing.
        if (soldierPrefab == null || !HasUsableSkinnedMesh(soldierPrefab))
        {
            if (soldierPrefab != null && !_warnedBrokenSoldier)
            {
                _warnedBrokenSoldier = true;
                Debug.LogWarning("[Spawner] Char/Solider_Ssanggeom prefab has no usable meshes (source model files missing from project) — using cube enemy visuals.");
            }
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

        // Wire up animation before measuring bounds so the skeleton is in a known pose.
        // Force updateWhenOffscreen so SMR bounds are always computed (even off-camera).
        var anim = soldier.GetComponentInChildren<Animator>();
        foreach (var smr in soldier.GetComponentsInChildren<SkinnedMeshRenderer>())
            smr.updateWhenOffscreen = true;
        if (anim != null)
        {
            anim.applyRootMotion = false;
            if (attackCtrl != null)
                anim.runtimeAnimatorController = attackCtrl;
            // Tick the animator once so the skeleton settles into the controller's default
            // state (Run) before we sample bounds — ensures accurate foot grounding even
            // though the default state changed from Attack to Run (issue #15 Task D).
            anim.Update(0f);
        }

        // Measure the soldier's skinned mesh bounds with soldier at localPos (0,0,0).
        // anim.Update(0f) above has baked the Run pose into the SMRs so bounds are accurate.
        var smrs = soldier.GetComponentsInChildren<SkinnedMeshRenderer>();
        if (smrs != null && smrs.Length > 0)
        {
            // Step 1: measure at localPos=(0,0,0) to get raw T-pose bounds.
            Bounds b = smrs[0].bounds;
            for (int i = 1; i < smrs.Length; i++) b.Encapsulate(smrs[i].bounds);

            // Step 2: scale to targetHeight.
            float currentH = b.size.y;
            float uniformScale = currentH > 0.001f ? 2.2f / currentH : 1f;
            soldier.transform.localScale = Vector3.one * uniformScale;

            // Step 3: re-measure bounds AFTER scaling (scale changes world bounds immediately).
            b = smrs[0].bounds;
            for (int i = 1; i < smrs.Length; i++) b.Encapsulate(smrs[i].bounds);

            // Step 4: move soldier so feet land at the world-space bottom of the collider box.
            // go.lossyScale.y = ENEMY_SIZE.y = 2.6; go center at Y=1.3; box bottom at Y=0.
            // holder.lossyScale ≈ (1,1,1) so world delta = soldier local delta.
            float targetFeetWorldY = go.transform.position.y - go.transform.lossyScale.y * 0.5f;
            float footDeltaY = targetFeetWorldY - b.min.y;
            soldier.transform.localPosition = new Vector3(0f, footDeltaY, 0f);
        }

        // Face the soldier toward the oncoming player (+Z = toward camera).
        soldier.transform.localRotation = Quaternion.identity;
    }

    static bool _warnedBrokenSoldier;

    static bool HasUsableSkinnedMesh(GameObject prefab)
    {
        foreach (var smr in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (smr.sharedMesh != null) return true;
        return false;
    }

    // ── Procedural hazard visuals (issue #25) ───────────────────────────────
    // Replace the primitive-cube visuals with attack-shaped meshes while keeping
    // colliders, sizes, and telegraph timing byte-for-byte identical.
    // Meshes are built once (static cache) and shared across all pooled instances.

    static Mesh _crescentMesh;
    static Mesh _slashMesh;

    // BuildCrescentMesh: a ground-sweep qi-crescent lying in the X-Z plane.
    // Spans ~1 unit X, shallow Y (~0.35 thick), curved leading edge bowing toward -Z.
    // Triangle strip between outer arc (radius R) and inner arc (radius r), 16 segments.
    // Visible from +Z looking toward -Z (player approaching from +Z).
    static Mesh BuildCrescentMesh()
    {
        const int   segs   = 16;
        const float R      = 0.55f;  // outer radius
        const float r      = 0.25f;  // inner radius
        const float thick  = 0.30f;  // Y thickness (top-to-bottom of blade)
        const float tilt   = 0.12f;  // slight upward bow at ends (lip)

        // Arc sweeps from -70° to +70° (in X-Z plane, bowing toward -Z).
        const float halfAng = 70f * Mathf.Deg2Rad;

        int vCount = segs + 1;
        // Top face (y = +thick*0.5) and bottom face (y = -thick*0.5), each with inner+outer ring.
        // Total: 4 rings × (segs+1) verts.
        int totalVerts = 4 * vCount;
        var verts  = new Vector3[totalVerts];
        var norms  = new Vector3[totalVerts];
        var uvs    = new Vector2[totalVerts];

        // Ring indices (rows of segs+1):
        // 0 = top outer, 1 = top inner, 2 = bottom outer, 3 = bottom inner
        for (int i = 0; i <= segs; i++)
        {
            float t   = (float)i / segs;            // 0..1
            float ang = Mathf.Lerp(-halfAng, halfAng, t);
            float x   = Mathf.Sin(ang);             // -sin(70°)..+sin(70°) ≈ -0.94..+0.94
            float zOuter = -Mathf.Cos(ang) * R;     // bow toward -Z (negative = toward player)
            float zInner = -Mathf.Cos(ang) * r;

            // Lip: ends curve upward slightly so it reads as a blade edge, not a plank.
            float lipY = tilt * (1f - Mathf.Abs(2f * t - 1f)); // 0 at ends, tilt at center

            float yTop = +thick * 0.5f + lipY;
            float yBot = -thick * 0.5f;

            int topOuter  = 0 * vCount + i;
            int topInner  = 1 * vCount + i;
            int botOuter  = 2 * vCount + i;
            int botInner  = 3 * vCount + i;

            verts[topOuter] = new Vector3(x * R, yTop, zOuter);
            verts[topInner] = new Vector3(x * r, yTop, zInner);
            verts[botOuter] = new Vector3(x * R, yBot, zOuter);
            verts[botInner] = new Vector3(x * r, yBot, zInner);

            norms[topOuter] = Vector3.up;
            norms[topInner] = Vector3.up;
            norms[botOuter] = Vector3.down;
            norms[botInner] = Vector3.down;

            float u = t;
            uvs[topOuter] = new Vector2(u, 1);
            uvs[topInner] = new Vector2(u, 0);
            uvs[botOuter] = new Vector2(u, 1);
            uvs[botInner] = new Vector2(u, 0);
        }

        // Build tris: top face (outer→inner strip), bottom face (inner→outer strip),
        // outer rim (side), inner rim (side), left cap, right cap.
        var tris = new System.Collections.Generic.List<int>();

        // Helper: quad from (a,b,c,d) in correct winding order
        // For top face (facing up): a=topOuter[i], b=topInner[i], c=topOuter[i+1], d=topInner[i+1]
        // winding: a,b,c  then b,d,c  (Unity CCW when viewed from outside)
        void AddQuad(int a, int b, int c, int d)
        {
            tris.Add(a); tris.Add(c); tris.Add(b);
            tris.Add(b); tris.Add(c); tris.Add(d);
        }

        for (int i = 0; i < segs; i++)
        {
            int to0 = 0 * vCount + i, to1 = 0 * vCount + i + 1;
            int ti0 = 1 * vCount + i, ti1 = 1 * vCount + i + 1;
            int bo0 = 2 * vCount + i, bo1 = 2 * vCount + i + 1;
            int bi0 = 3 * vCount + i, bi1 = 3 * vCount + i + 1;

            // Top face (faces up — normal +Y already set)
            AddQuad(to0, ti0, to1, ti1);
            // Bottom face (faces down — winding reversed)
            AddQuad(bi0, bo0, bi1, bo1);
            // Outer rim (faces outward)
            AddQuad(bo0, to0, bo1, to1);
            // Inner rim (faces inward, winding reversed)
            AddQuad(ti0, bi0, ti1, bi1);
        }

        // Left cap (i=0) and right cap (i=segs)
        // Left cap: topOuter[0], topInner[0], botOuter[0], botInner[0]
        {
            int to = 0*vCount, ti = 1*vCount, bo = 2*vCount, bi = 3*vCount;
            tris.Add(to); tris.Add(bi); tris.Add(ti);
            tris.Add(to); tris.Add(bo); tris.Add(bi);
        }
        // Right cap: topOuter[segs], topInner[segs], botOuter[segs], botInner[segs]
        {
            int to = 0*vCount+segs, ti = 1*vCount+segs, bo = 2*vCount+segs, bi = 3*vCount+segs;
            tris.Add(ti); tris.Add(bi); tris.Add(to);
            tris.Add(bi); tris.Add(bo); tris.Add(to);
        }

        var mesh = new Mesh();
        mesh.name = "CrescentMesh";
        mesh.vertices  = verts;
        mesh.normals   = norms;
        mesh.uv        = uvs;
        mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds();
        mesh.RecalculateNormals(); // override the manual normals for proper lighting
        return mesh;
    }

    // BuildSlashMesh: a high horizontal slash blade spanning ~1 unit X.
    // Thin in Y (~0.33 total), tapered (wider in middle, pointed tips), diagonal cant.
    // 4 verts per cross-section (top-front, top-back, bot-front, bot-back), 14 segments.
    // Visible from +Z looking toward -Z (player approaching from +Z).
    static Mesh BuildSlashMesh()
    {
        const int   segs  = 14;
        const float halfX = 0.52f;   // half-length in X
        const float maxTY = 0.18f;   // max top-edge Y at center (blade crown)
        const float maxBY = -0.15f;  // max bot-edge Y at center (cutting edge)
        const float cant  = 0.08f;   // diagonal tilt: +X end higher, -X end lower
        const float depth = 0.18f;   // Z thickness (blade cross-section)

        // Ring layout (rows of segs+1): 0=top-front, 1=top-back, 2=bot-front, 3=bot-back
        int vCount     = segs + 1;
        int totalVerts = 4 * vCount;
        var verts = new Vector3[totalVerts];
        var uvs   = new Vector2[totalVerts];

        for (int i = 0; i <= segs; i++)
        {
            float t     = (float)i / segs;
            float x     = Mathf.Lerp(-halfX, halfX, t);
            float taper = Mathf.Sin(t * Mathf.PI); // 0 at tips, 1 at center
            float topY  = maxTY * taper + cant * (t - 0.5f);
            float botY  = maxBY * taper + cant * (t - 0.5f);
            float zF    = -depth * 0.5f;
            float zB    = +depth * 0.5f;

            verts[0*vCount+i] = new Vector3(x, topY, zF); // top-front
            verts[1*vCount+i] = new Vector3(x, topY, zB); // top-back
            verts[2*vCount+i] = new Vector3(x, botY, zF); // bot-front
            verts[3*vCount+i] = new Vector3(x, botY, zB); // bot-back

            uvs[0*vCount+i] = new Vector2(t, 1);
            uvs[1*vCount+i] = new Vector2(t, 1);
            uvs[2*vCount+i] = new Vector2(t, 0);
            uvs[3*vCount+i] = new Vector2(t, 0);
        }

        var tris = new System.Collections.Generic.List<int>();

        for (int i = 0; i < segs; i++)
        {
            int tf0=0*vCount+i, tf1=0*vCount+i+1;
            int tb0=1*vCount+i, tb1=1*vCount+i+1;
            int bf0=2*vCount+i, bf1=2*vCount+i+1;
            int bb0=3*vCount+i, bb1=3*vCount+i+1;

            // Front face (–Z, player-facing): CCW from –Z
            tris.Add(tf0); tris.Add(tf1); tris.Add(bf0);
            tris.Add(bf0); tris.Add(tf1); tris.Add(bf1);
            // Back face (+Z): winding flipped
            tris.Add(tb1); tris.Add(tb0); tris.Add(bb1);
            tris.Add(bb1); tris.Add(tb0); tris.Add(bb0);
            // Top spine
            tris.Add(tf0); tris.Add(tb0); tris.Add(tf1);
            tris.Add(tf1); tris.Add(tb0); tris.Add(tb1);
            // Bottom cutting edge
            tris.Add(bf1); tris.Add(bb0); tris.Add(bf0);
            tris.Add(bf1); tris.Add(bb1); tris.Add(bb0);
        }
        // Left cap
        {
            int tf=0*vCount, tb=1*vCount, bf=2*vCount, bb=3*vCount;
            tris.Add(tf); tris.Add(bf); tris.Add(tb);
            tris.Add(tb); tris.Add(bf); tris.Add(bb);
        }
        // Right cap
        {
            int tf=0*vCount+segs, tb=1*vCount+segs, bf=2*vCount+segs, bb=3*vCount+segs;
            tris.Add(tf); tris.Add(tb); tris.Add(bf);
            tris.Add(tb); tris.Add(bb); tris.Add(bf);
        }

        var mesh = new Mesh();
        mesh.name      = "SlashMesh";
        mesh.vertices  = verts;
        mesh.uv        = uvs;
        mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        return mesh;
    }

    // EnsureBlockVisual: hide the cube's MeshRenderer; show a qi-crescent child mesh.
    // Pool-safe: holder creation guarded; re-hide MeshRenderer every spawn to survive reuse.
    // ONLY called from SpawnBlock — NOT from SpawnLightning or SpawnAerial which share
    // HazardKind.Block but should keep their own plain appearances.
    void EnsureBlockVisual(GameObject go, float w, float h, float d)
    {
        // Always re-hide the cube renderer (pool reuse may have re-shown it).
        var cubeMR = go.GetComponent<MeshRenderer>();
        if (cubeMR != null) cubeMR.enabled = false;

        // Idempotent: if holder already exists, we're done (mesh and mat already wired).
        if (go.transform.Find("BlockVisualHolder") != null) return;

        // Scale-cancel holder: go.localScale = (w, h, d); invert so holder children live
        // in unit space and the crescent mesh is not non-uniformly stretched.
        var holder = new GameObject("BlockVisualHolder");
        holder.transform.SetParent(go.transform, false);
        holder.transform.localPosition = Vector3.zero;
        holder.transform.localRotation = Quaternion.identity;
        holder.transform.localScale = new Vector3(1f / w, 1f / h, 1f / d);

        // Build (or reuse) the cached crescent mesh.
        if (_crescentMesh == null) _crescentMesh = BuildCrescentMesh();

        // Visual child: MeshFilter + MeshRenderer with the block glow material.
        var visual = new GameObject("CrescentVisual");
        visual.transform.SetParent(holder.transform, false);
        // Position crescent near the bottom of the block so it reads as a floor sweep.
        // In holder (unit) space, block bottom is at local y = -0.5; offset up a little.
        visual.transform.localPosition = new Vector3(0f, -0.3f, 0f);
        // Scale the crescent to span the full width (holder inverts the box scale,
        // so we scale back by w to fill the lane width in world space).
        visual.transform.localScale = new Vector3(w, h, d);
        // Slight tilt: rotate the arc so the sweep bow faces the player (-Z direction).
        visual.transform.localRotation = Quaternion.identity;

        var mf = visual.AddComponent<MeshFilter>();
        mf.sharedMesh = _crescentMesh;
        var mr = visual.AddComponent<MeshRenderer>();
        mr.sharedMaterial = _matBlock;
    }

    // EnsureBarVisual: hide the cube's MeshRenderer; show a horizontal slash blade child mesh.
    // Pool-safe: same idiom as EnsureBlockVisual.
    void EnsureBarVisual(GameObject go, float w, float h, float d)
    {
        // Always re-hide the cube renderer.
        var cubeMR = go.GetComponent<MeshRenderer>();
        if (cubeMR != null) cubeMR.enabled = false;

        if (go.transform.Find("BarVisualHolder") != null) return;

        var holder = new GameObject("BarVisualHolder");
        holder.transform.SetParent(go.transform, false);
        holder.transform.localPosition = Vector3.zero;
        holder.transform.localRotation = Quaternion.identity;
        holder.transform.localScale = new Vector3(1f / w, 1f / h, 1f / d);

        if (_slashMesh == null) _slashMesh = BuildSlashMesh();

        var visual = new GameObject("SlashVisual");
        visual.transform.SetParent(holder.transform, false);
        // Center the blade in the bar volume.
        visual.transform.localPosition = Vector3.zero;
        // Scale to fill the bar's world-space extent.
        visual.transform.localScale = new Vector3(w, h, d);
        visual.transform.localRotation = Quaternion.identity;

        var mf = visual.AddComponent<MeshFilter>();
        mf.sharedMesh = _slashMesh;
        var mr = visual.AddComponent<MeshRenderer>();
        mr.sharedMaterial = _matBar;
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
        Feel.ClearActive();

        _elapsed   = 0f;
        _timer     = Balance.D.spawn_start_interval;
        _orbTimer  = Balance.D.spawn_orb_interval;
        _pillTimer = Balance.D.spawn_pill_interval;
        _gateTimer = Balance.D.spawn_gate_interval;
        _sched     = new SpawnScheduler(Balance.D);
    }
}
