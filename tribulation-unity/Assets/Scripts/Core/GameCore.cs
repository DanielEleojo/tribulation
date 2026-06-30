// Pure-C# run-state machine. No UnityEngine dependency.
// Ported from game.gd (lines 1-1059). Out-of-scope items (audio buses, shop, dailies,
// achievements, trials, HUD, camera, telemetry) are deliberately skipped;
// each omission is noted with a "ponytail:" comment below.
//
// TDD vertical slices (tests in Assets/Tests/EditMode/GameCoreTests.cs):
//   Test 1: Realm advances when run_progress >= realm_span threshold
//   Test 2: Qi gathers per kill, clamps at qi_max, burst spends Qi
//   Test 3: Net closes over time, kill pushes it back, net>=1 fires Died
//   Test 4: Combo increments on kill, resets on hit
//   Test 5: Save/load round-trips lifetime stats + settings
//   Test 6: Powerup timers (magnet/double/dash) activate + expire after Tick
//   Test 7: double active doubles soul gain from kills
//   Test 8: surge fills Qi to max; at realm<2 relieves Net; at realm>=2 triggers burst
//   Test 9: InSlashReach true inside range+tol, false outside; gated at realm<2

using System;
using System.Collections.Generic;

namespace Tribulation.Core
{
    public class GameCore
    {
        // ── Public events ────────────────────────────────────────────────────
        public event Action                    Died;
        public event Action<float, float>      QiChanged;     // (qi, qi_max)
        public event Action<float>             NetChanged;    // net 0..1
        public event Action<int>               SoulsChanged;  // souls this run
        public event Action<int, float>        ComboChanged;  // (combo, mult)
        public event Action                    Burst;         // Qi burst fired
        public event Action                    Breakthrough;  // realm advanced (tribulation surmounted)
        public event Action<int>               TrialFulfilled; // reward (for sfx/banner)

        // note: RealmChanged event omitted; callers inspect Realm property after Tick

        // ── State (read-only outside) ─────────────────────────────────────
        public int   Realm        { get; private set; }
        public float Qi           { get; private set; }
        public float Net          { get; private set; }
        public int   Souls        { get; private set; }   // this run
        public int   TotalStones  { get; private set; }   // lifetime earned
        public int   Combo        { get; private set; }
        public int   RunProgress  { get; private set; }   // progress toward next realm THIS attempt
        public int   BestLi       { get; private set; }
        public bool  IsDead       { get; private set; }
        public bool  IsStarted    { get; private set; }

        // Lifetime stats
        public int   StatRuns     { get; private set; }
        public int   StatFoes     { get; private set; }
        public int   StatTribs    { get; private set; }
        public int   StatDeaths   { get; private set; }

        // Tribulation state
        public bool  InTribulation { get; private set; }
        float        _tribT;
        const float  TRIB_DURATION = 12f;

        // Audio settings (persisted)
        public float MusicVol  { get; private set; } = 0.8f;
        public float SfxVol    { get; private set; } = 0.9f;
        public bool  Muted     { get; private set; }

        // Spent (for spendable balance = total - spent)
        int _spent;

        // ── Upgrade system ────────────────────────────────────────────────────

        /// <summary>Static definition for a single purchasable upgrade.</summary>
        public readonly struct UpgradeDef
        {
            public readonly string Id;
            public readonly string Name;
            public readonly string Desc;
            public readonly int    MaxLevel;
            public readonly int    BaseCost;

            public UpgradeDef(string id, string name, string desc, int maxLevel, int baseCost)
            {
                Id = id; Name = name; Desc = desc; MaxLevel = maxLevel; BaseCost = baseCost;
            }
        }

        static readonly UpgradeDef[] _upgradeDefs = new[]
        {
            new UpgradeDef("qi_flow",      "Qi Flow",        "Multiplies Qi gained from kills and orbs.",       3, 50),
            new UpgradeDef("stone_sense",  "Stone Sense",    "Multiplies Spirit Stones earned from all sources.",3, 60),
            new UpgradeDef("spirit_root",  "Spirit Root",    "Grants a head-start burst of Qi at run start.",   3, 80),
            new UpgradeDef("heavens_favor","Heaven's Favor", "Slows the Heavenly-Net's pressure over time.",    3,100),
        };

        /// <summary>Ordered table of all purchasable upgrades.</summary>
        public IReadOnlyList<UpgradeDef> Upgrades => _upgradeDefs;

        int[] _upLevels = new int[4]; // index-parallel to _upgradeDefs

        // Helper indices
        const int IDX_QI_FLOW      = 0;
        const int IDX_STONE_SENSE  = 1;
        const int IDX_SPIRIT_ROOT  = 2;
        const int IDX_HEAVENS_FAVOR = 3;

        // ── Upgrade derived multipliers ────────────────────────────────────────

        /// <summary>Qi-gain multiplier: 1 + 0.15 × level of qi_flow.</summary>
        public float QiMult        => 1f + 0.15f * _upLevels[IDX_QI_FLOW];

        /// <summary>Stone-gain multiplier: 1 + 0.20 × level of stone_sense.</summary>
        public float StoneMult     => 1f + 0.20f * _upLevels[IDX_STONE_SENSE];

        /// <summary>Head-start Qi at run start: 25 × level of spirit_root.</summary>
        public float StartQiBonus  => 25f * _upLevels[IDX_SPIRIT_ROOT];

        /// <summary>Net-close multiplier: max(0.4, 1 - 0.10 × level of heavens_favor).</summary>
        public float NetCloseMult  => Math.Max(0.4f, 1f - 0.10f * _upLevels[IDX_HEAVENS_FAVOR]);

        // ── Upgrade public API ────────────────────────────────────────────────

        /// <summary>Current level of upgrade i (0 = not purchased).</summary>
        public int UpgradeLevel(int i) => _upLevels[i];

        /// <summary>Stones currently available to spend (total earned minus total spent).</summary>
        public int SpendableStones => TotalStones - _spent;

        /// <summary>Persistence getter used by the MonoBehaviour save layer.</summary>
        public IReadOnlyList<int> UpgradeLevels => _upLevels;

        /// <summary>Cost to advance upgrade i to the next level, or -1 if already at MaxLevel.</summary>
        public int NextUpgradeCost(int i)
        {
            var def = _upgradeDefs[i];
            if (_upLevels[i] >= def.MaxLevel) return -1;
            return def.BaseCost * (_upLevels[i] + 1);
        }

        /// <summary>
        /// Attempt to purchase the next level of upgrade i.
        /// Returns true and deducts cost from SpendableStones on success.
        /// Returns false if already maxed or insufficient funds.
        /// </summary>
        public bool TryBuyUpgrade(int i)
        {
            int cost = NextUpgradeCost(i);
            if (cost < 0) return false;        // already maxed
            if (cost > SpendableStones) return false; // can't afford
            _upLevels[i]++;
            _spent += cost;
            return true;
        }

        // ── Powerup timers (id → seconds remaining) ───────────────────────────
        // Ported from game.gd _powerups dict + POWERUPS const (lines 286-330).
        // Instant powerups (surge, aegis) never enter this dict.
        readonly Dictionary<string, float> _powerups = new Dictionary<string, float>();

        // Powerup base durations (game.gd POWERUPS dict):  magnet=8, double=10, dash=3
        static readonly Dictionary<string, float> PowerupDur = new Dictionary<string, float>
        {
            { "magnet", 8f },
            { "double", 10f },
            { "dash",   3f  },
        };

        // ── Slash per-realm reach (game.gd _realms array) ─────────────────────
        // Index = realm (0..5).  range = forward reach, tol = lateral half-width.
        static readonly float[] RealmSlashRange = { 4.0f, 4.6f, 5.4f, 6.0f, 6.6f, 8.5f };
        static readonly float[] RealmSlashTol   = { 1.4f, 1.4f, 2.6f, 2.6f, 2.8f, 4.0f };

        /// <summary>Slash forward reach for the current realm (game.gd _realms[realm].range).</summary>
        public float SlashRange => RealmSlashRange[Math.Min(Realm, RealmSlashRange.Length - 1)];

        /// <summary>Slash lateral tolerance for the current realm (game.gd _realms[realm].tol).</summary>
        public float SlashTol   => RealmSlashTol  [Math.Min(Realm, RealmSlashTol.Length   - 1)];

        // ── Survivability per-realm stats (game.gd _realms array, #8) ─────────
        // Index = realm (0..5). shield = Iron-Body absorb slots, sprint = Blood-Sprint per kill.
        // speed = forward speed multiplier (applied in PlayerRunner run_speed formula).
        static readonly int[]   RealmShieldSlots  = { 0,    0,    0,    1,    1,    2    };
        static readonly float[] RealmSprintPerKill = { 0.0f, 1.5f, 1.5f, 2.0f, 2.0f, 3.0f };
        static readonly float[] RealmSpeedMult     = { 1.0f, 1.0f, 1.0f, 1.0f, 1.05f, 1.25f };

        /// <summary>Iron-Body shield slots for the current realm (game.gd _realms[realm].shield).</summary>
        public int   ShieldSlots    => RealmShieldSlots  [Math.Min(Realm, RealmShieldSlots.Length   - 1)];

        /// <summary>Blood-Sprint speed added per kill for the current realm (game.gd _realms[realm].sprint).</summary>
        public float SprintPerKill  => RealmSprintPerKill[Math.Min(Realm, RealmSprintPerKill.Length - 1)];

        /// <summary>Forward speed multiplier for the current realm (game.gd _realms[realm].speed).</summary>
        public float SpeedMult      => RealmSpeedMult    [Math.Min(Realm, RealmSpeedMult.Length     - 1)];

        // ── Trials (Cultivation Vows) ─────────────────────────────────────────
        // Ported from game.gd _trial templates + _roll_trials / _trial_add / _trial_max.

        struct TrialTemplate
        {
            public string Id;
            public string Fmt;
            public int[]  Goals;
            public int[]  Rewards;
        }

        static readonly TrialTemplate[] TrialTemplates =
        {
            new TrialTemplate { Id = "slay",    Fmt = "Slay %d foes",            Goals = new[] { 8,   16,  28   }, Rewards = new[] { 40, 90, 170 } },
            new TrialTemplate { Id = "li",      Fmt = "Flee %d li",              Goals = new[] { 400, 900, 1600 }, Rewards = new[] { 40, 90, 170 } },
            new TrialTemplate { Id = "qi",      Fmt = "Gather %d Qi",            Goals = new[] { 15,  30,  55   }, Rewards = new[] { 40, 90, 170 } },
            new TrialTemplate { Id = "combo",   Fmt = "Reach a Dao Heart of %d", Goals = new[] { 8,   16,  28   }, Rewards = new[] { 40, 90, 170 } },
            new TrialTemplate { Id = "survive", Fmt = "Endure %d seconds",       Goals = new[] { 30,  60,  100  }, Rewards = new[] { 40, 90, 170 } },
        };

        readonly List<TrialState> _trials = new List<TrialState>();
        System.Random _rng = new System.Random();

        /// <summary>Active cultivation trials this run (for HUD readout).</summary>
        public IReadOnlyList<TrialState> Trials => _trials;

        /// <summary>
        /// Roll 3 distinct random trials, each at a random tier (0=easy, 1=mid, 2=hard).
        /// Faithfully mirrors game.gd _roll_trials(). Uses injectable Random for determinism in tests.
        /// </summary>
        public void RollTrials(System.Random rng)
        {
            _trials.Clear();
            // Fisher-Yates shuffle on indices
            int[] idx = { 0, 1, 2, 3, 4 };
            for (int i = idx.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int tmp = idx[i]; idx[i] = idx[j]; idx[j] = tmp;
            }
            for (int pick = 0; pick < 3; pick++)
            {
                var tmpl = TrialTemplates[idx[pick]];
                int d = rng.Next(3); // tier 0,1,2
                _trials.Add(new TrialState
                {
                    Id       = tmpl.Id,
                    Fmt      = tmpl.Fmt,
                    Goal     = tmpl.Goals[d],
                    Reward   = tmpl.Rewards[d],
                    Progress = 0f,
                    Done     = false,
                });
            }
        }

        /// <summary>Add amt to matching non-done trial's progress, then check completion.</summary>
        public void TrialAdd(string id, float amt)
        {
            foreach (var t in _trials)
            {
                if (t.Id == id && !t.Done)
                {
                    t.Progress += amt;
                    CheckTrial(t);
                    break;
                }
            }
        }

        /// <summary>Set progress to val if val > current progress, then check completion.</summary>
        public void TrialMax(string id, float val)
        {
            foreach (var t in _trials)
            {
                if (t.Id == id && !t.Done)
                {
                    if (val > t.Progress) t.Progress = val;
                    CheckTrial(t);
                    break;
                }
            }
        }

        void CheckTrial(TrialState t)
        {
            if (t.Progress >= t.Goal)
            {
                t.Done = true;
                Souls       += t.Reward;
                TotalStones += t.Reward;
                SoulsChanged?.Invoke(Souls);
                UpdateCultivation();
                TrialFulfilled?.Invoke(t.Reward);
                // ponytail: trial banner + HUD trial list — Batch 2 (HudOverlay)
            }
        }

        // ── Balance ──────────────────────────────────────────────────────────
        readonly BalanceData _b;

        // Number of cultivation realms (from balance realm_span array length)
        int RealmCount => _b.realm_span?.Length ?? 6;

        int RealmSpan(int r)
        {
            var s = _b.realm_span;
            if (s == null || s.Length == 0) return 50;
            return s[Math.Min(r, s.Length - 1)];
        }

        // ── Constructor ───────────────────────────────────────────────────────
        public GameCore(BalanceData balance)
        {
            _b = balance ?? new BalanceData();
        }

        /// <summary>Seconds of difficulty head-start for the current realm (higher realms start
        /// deeper in the curve). Ported from game.gd start_game: realm * DIFFICULTY_PER_REALM.</summary>
        public float DifficultyOffset() => Realm * _b.difficulty_per_realm;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Called by the MonoBehaviour wrapper each Update with Time.deltaTime.
        /// Advances the Heavenly-Net (or tribulation timer) when the run is live.
        /// </summary>
        public void Tick(float delta)
        {
            if (!IsStarted || IsDead) return;

            TickPowerups(delta);
            TrialAdd("survive", delta);

            if (InTribulation)
            {
                _tribT -= delta;
                if (_tribT <= 0f)
                    SurmountTribulation();
            }
            else
            {
                // Ascension defies heaven; net can only reach 0.85 at that realm.
                float cap = (Realm >= RealmCount - 1) ? 0.85f : 1.0f;
                Net = Math.Min(cap, Net + _b.net_close_rate * NetCloseMult * delta);
                NetChanged?.Invoke(Net);
                if (Net >= 1.0f)
                    Die(0); // no distance supplied from pure core
            }
        }

        /// <summary>
        /// Start a run. Increments stat_runs; caller should provide current distance=0.
        /// </summary>
        /// <summary>
        /// Restart after death: clears dead/started so StartRun re-inits a fresh run.
        /// Realm + lifetime stats persist (die and you restart your realm at the 1st Layer).
        /// </summary>
        public void RestartRun()
        {
            IsDead    = false;
            IsStarted = false;
            StartRun();
        }

        public void StartRun()
        {
            if (IsStarted) return;
            IsStarted   = true;
            IsDead      = false;
            Souls       = 0;
            RunProgress = 0;
            Combo       = 0;
            Qi          = Math.Min(_b.qi_max, StartQiBonus); // spirit_root head-start
            Net         = 0f;
            StatRuns++;
            RollTrials(_rng);
            QiChanged?.Invoke(Qi, _b.qi_max);
            NetChanged?.Invoke(Net);
            SoulsChanged?.Invoke(Souls);
            ComboChanged?.Invoke(Combo, 1f);
        }

        /// <summary>
        /// Resolve a Life/Death Gate pass. Ported from game.gd on_gate() (line 968).
        /// safe=true : Qi += 25 (clamped ≤ qi_max), Net -= 0.15 (clamped ≥ 0).
        /// safe=false: Qi -= 40 (clamped ≥ 0), Net += 0.30 (clamped ≤ 1), combo reset.
        /// Guard: no-op if dead or not started (mirrors is_dead guard in game.gd).
        /// Note: if Net reaches 1.0 after a death gate the existing Tick handles death,
        /// exactly as in game.gd (net_changed.emit then die() check after emit).
        /// ponytail: sfx, HUD flash, camera shake — deferred to Game.cs
        /// </summary>
        public void OnGate(bool safe)
        {
            if (IsDead || !IsStarted) return;
            if (safe)
            {
                Qi  = Math.Min(_b.qi_max, Qi + 25f);
                Net = Math.Max(0f, Net - 0.15f);
            }
            else
            {
                Qi  = Math.Max(0f, Qi - 40f);
                Net = Math.Min(1.0f, Net + 0.30f);
                ResetCombo(); // fires ComboChanged(0,1) if combo was non-zero
            }
            QiChanged?.Invoke(Qi, _b.qi_max);
            NetChanged?.Invoke(Net);
            // Death-via-net is handled by the next Tick(), matching game.gd semantics:
            // game.gd emits net_changed, then checks net >= 1.0 and calls die().
            // We do the same — Tick() already calls Die(0) when Net >= 1.0.
        }

        /// <summary>
        /// Called when an enemy is slain (count = number killed at once).
        /// Charges Qi, pushes Net back, increments combo. Triggers burst if qi_max reached.
        /// Mirrors game.gd on_enemy_killed().
        /// </summary>
        public void OnEnemyKilled(int count = 1)
        {
            if (IsDead || !IsStarted) return;

            Combo += count;
            float m = ComboMult();
            int g = (int)Math.Round(count * m);
            if (IsPowerupActive("double")) g *= 2;  // double pill: double soul gain
            int stoneGain = (int)Math.Round(g * StoneMult); // stone_sense upgrade
            Souls       += g;
            TotalStones += stoneGain;
            RunProgress += g;
            SoulsChanged?.Invoke(Souls);
            ComboChanged?.Invoke(Combo, m);

            StatFoes += count;
            TrialAdd("slay", count);
            TrialMax("combo", Combo);
            // ponytail: achievements — deferred (Batch 3)

            UpdateCultivation();

            float qiGain = _b.qi_per_kill * count * QiMult; // qi_flow upgrade
            Qi = Math.Min(_b.qi_max, Qi + qiGain);
            QiChanged?.Invoke(Qi, _b.qi_max);

            Net = Math.Max(0f, Net - _b.net_push_per_kill * count);
            NetChanged?.Invoke(Net);

            if (Qi >= _b.qi_max)
                QiBurst();
        }

        /// <summary>
        /// Called when the player is hit by a hazard (no shield absorbed it).
        /// Resets combo, then kills the run.
        /// Mirrors game.gd player_hit() → die() path without Iron Body.
        /// </summary>
        public void OnPlayerHit(int distanceLi = 0)
        {
            if (IsDead || !IsStarted) return;
            ResetCombo();
            Die(distanceLi);
        }

        /// <summary>
        /// Non-lethal contact: a hit no longer ends the run — it spikes the Heavenly Net
        /// and breaks your combo. The Net is the only death path now; the run ends only
        /// when Net reaches 1.0. A pursuer landing its hit spikes harder than a dumb obstacle,
        /// which is what gives the slash its purpose.
        /// </summary>
        public void OnContactHit(bool isEnemy)
        {
            if (IsDead || !IsStarted) return;
            ResetCombo();
            float spike = isEnemy ? _b.net_hit_enemy : _b.net_hit_obstacle;
            Net = Math.Min(1.0f, Net + spike);
            NetChanged?.Invoke(Net);
            if (Net >= 1.0f) Die(0);
        }

        /// <summary>Record a run distance (li); updates BestLi if it's a new best. Called by the
        /// MonoBehaviour layer on death, which knows the real distance the pure core does not.</summary>
        public void RecordDistance(int li) { if (li > BestLi) BestLi = li; }

        /// <summary>
        /// Explicit death (also called internally when net >= 1.0).
        /// </summary>
        public void Die(int distanceLi = 0)
        {
            if (IsDead) return;
            IsDead = true;
            StatDeaths++;
            if (distanceLi > BestLi) BestLi = distanceLi;
            RunProgress = 0;
            if (InTribulation)
            {
                InTribulation = false;
                // ponytail: free heart demon, clear HUD tribulation — deferred
            }
            // ponytail: telemetry, sfx, camera shake — deferred
            Died?.Invoke();
        }

        /// <summary>
        /// A Spirit Orb collected (minor qi + net relief, no slash ability needed).
        /// Mirrors game.gd on_orb_collected().
        /// </summary>
        public void OnOrbCollected()
        {
            if (IsDead || !IsStarted) return;
            Combo += 1;
            float m = ComboMult();
            int g = (int)Math.Round(m);
            if (IsPowerupActive("double")) g *= 2;
            int stoneGain = (int)Math.Round(g * StoneMult); // stone_sense upgrade
            Souls       += g;
            TotalStones += stoneGain;
            RunProgress += g;
            SoulsChanged?.Invoke(Souls);
            ComboChanged?.Invoke(Combo, m);
            TrialAdd("qi", 1f);
            TrialMax("combo", Combo);

            Qi = Math.Min(_b.qi_max, Qi + 4f * QiMult); // qi_flow upgrade
            QiChanged?.Invoke(Qi, _b.qi_max);

            // Orbs always ease the Net a little (a counter-force in the Net economy).
            Net = Math.Max(0f, Net - 0.012f);
            NetChanged?.Invoke(Net);

            UpdateCultivation();
        }

        // ── Powerup public API ────────────────────────────────────────────────
        // Ported from game.gd activate_powerup / is_powerup_active (lines 296-330).

        /// <summary>
        /// Activate a pill/talisman. Instant ones (surge) fire immediately;
        /// timed ones (magnet, double, dash) start their countdown.
        /// ponytail: _pill_bonus() shop bonus treated as 0.
        /// </summary>
        public void ActivatePowerup(string id)
        {
            if (IsDead || !IsStarted) return;

            switch (id)
            {
                case "surge":
                    // Fill Qi to max immediately.
                    Qi = _b.qi_max;
                    QiChanged?.Invoke(Qi, _b.qi_max);
                    if (HasAbility("qi"))
                        QiBurst();
                    else
                    {
                        Net = Math.Max(0f, Net - 0.30f);
                        NetChanged?.Invoke(Net);
                    }
                    break;

                case "aegis":
                    // note: Iron Aegis grants a shield via the player's Survivability (Game.ActivatePowerup), not the pure core
                    break;

                default:
                    if (PowerupDur.TryGetValue(id, out float dur))
                        _powerups[id] = dur; // ponytail: _pill_bonus() shop bonus = 0
                    break;
            }
        }

        /// <summary>
        /// Returns true while a timed powerup is running (game.gd is_powerup_active).
        /// </summary>
        public bool IsPowerupActive(string id) => _powerups.ContainsKey(id);

        // ── Slash reach predicate (pure, testable) ────────────────────────────

        /// <summary>
        /// Returns true if an enemy at (ahead, lateral) is inside slash reach.
        /// ahead  = player.z - enemy.z  (positive means enemy is in front)
        /// lateral = |enemy.x - player.x|
        /// Mirrors player.gd try_slash() reach check (line 634).
        /// </summary>
        public static bool InSlashReach(float ahead, float lateral, float range, float tol)
            => ahead >= -1.0f && ahead <= range && lateral <= tol;

        /// <summary>
        /// Audio settings mutators — persisted via Save/Load.
        /// note: applying to audio buses is the MonoBehaviour's job
        /// </summary>
        public void SetMusicVol(float v) { MusicVol = Clamp01(v); }
        public void SetSfxVol(float v)   { SfxVol   = Clamp01(v); }
        public void SetMuted(bool m)     { Muted    = m; }

        // ── Save / Load ──────────────────────────────────────────────────────

        /// <summary>
        /// Snapshot all lifetime stats + settings into a plain SaveData POCO.
        /// The MonoBehaviour wrapper calls JsonUtility.ToJson(ToSave()) and writes the file.
        /// </summary>
        public SaveData ToSave()
        {
            var upList = new System.Collections.Generic.List<int>(_upLevels);
            return new SaveData
            {
                realm         = Realm,
                totalStones   = TotalStones,
                spent         = _spent,
                bestLi        = BestLi,
                statRuns      = StatRuns,
                statFoes      = StatFoes,
                statTribs     = StatTribs,
                statDeaths    = StatDeaths,
                musicVol      = MusicVol,
                sfxVol        = SfxVol,
                muted         = Muted,
                upgradeLevels = upList,
            };
        }

        /// <summary>
        /// Restore lifetime state from a SaveData (loaded by the MonoBehaviour wrapper).
        /// Per-run transient state (qi, net, combo, souls, run_progress) is NOT restored —
        /// matches game.gd behaviour: die and you restart the realm at the 1st Layer.
        /// </summary>
        public void LoadSave(SaveData d)
        {
            if (d == null) return;
            Realm       = Clamp(d.realm, 0, RealmCount - 1);
            TotalStones = Math.Max(0, d.totalStones);
            _spent      = Math.Max(0, d.spent);
            BestLi      = Math.Max(0, d.bestLi);
            StatRuns    = Math.Max(0, d.statRuns);
            StatFoes    = Math.Max(0, d.statFoes);
            StatTribs   = Math.Max(0, d.statTribs);
            StatDeaths  = Math.Max(0, d.statDeaths);
            MusicVol    = Clamp01(d.musicVol);
            SfxVol      = Clamp01(d.sfxVol);
            Muted       = d.muted;

            // Restore upgrade levels (guard null/wrong length)
            if (d.upgradeLevels != null)
            {
                for (int i = 0; i < _upLevels.Length && i < d.upgradeLevels.Count; i++)
                    _upLevels[i] = Clamp(d.upgradeLevels[i], 0, _upgradeDefs[i].MaxLevel);
            }
        }

        // ── Minor-layer helper (exposed for HUD / debug overlay) ─────────────

        /// <summary>
        /// Wuxia minor layer (1..10) within the current major realm.
        /// Mirrors game.gd minor_level().
        /// </summary>
        public int MinorLevel()
        {
            if (Realm >= RealmCount - 1) return 10; // Ascension — Great Perfection
            int span = Math.Max(1, RealmSpan(Realm));
            float f  = Math.Min((float)RunProgress / span, 0.999f);
            return (int)(f * 10f) + 1;
        }

        // ── Ability gate (mirrors game.gd ABILITY_REALM) ─────────────────────

        static readonly Dictionary<string, int> AbilityRealm
            = new Dictionary<string, int>
        {
            { "run",          0 }, { "jump",   0 }, { "slide",        0 }, { "lane",  0 },
            // Core survival verbs (slash + Qi Burst) are available from realm 0 — they are
            // how you push back the Heavenly Net, so the loop is incomplete without them.
            // Realms instead hand out SPECTACLE: double-jump, glide, sword-flight, Dread Form.
            { "slash",        0 }, { "qi",     0 },
            { "doublejump",   1 },
            { "glide",        3 },
            { "swordflight",  4 },
            { "tribulation",  5 },
        };

        public bool HasAbility(string name)
        {
            return AbilityRealm.TryGetValue(name, out int r) && Realm >= r;
        }

        // ── Internal helpers ──────────────────────────────────────────────────

        /// <summary>
        /// Check for realm advancement or tribulation trigger.
        /// Mirrors game.gd _update_cultivation().
        /// </summary>
        void UpdateCultivation()
        {
            if (!InTribulation && Realm < RealmCount - 1 && RunProgress >= RealmSpan(Realm))
                BeginTribulation();
        }

        void BeginTribulation()
        {
            InTribulation = true;
            _tribT        = TRIB_DURATION;
            // ponytail: sfx, camera shake, HUD tribulation banner — deferred to Game.cs
        }

        void SurmountTribulation()
        {
            InTribulation = false;
            RunProgress   = Math.Max(0, RunProgress - RealmSpan(Realm));
            Realm++;
            StatTribs++;

            int r = 150; // breakthrough soul reward (game.gd line 457)
            Souls       += r;
            TotalStones += r;
            SoulsChanged?.Invoke(Souls);

            // ponytail: telemetry, sfx, camera, HUD breakthrough banner — deferred to Game.cs
            Breakthrough?.Invoke();
        }

        /// <summary>
        /// Qi Burst: resets Qi to 0, pushes Net back by net_burst_relief.
        /// Mirrors game.gd _qi_burst().
        /// ponytail: clearing enemy nodes, burst VFX, sfx — deferred to Game.cs
        /// </summary>
        void QiBurst()
        {
            Qi = 0f;
            QiChanged?.Invoke(Qi, _b.qi_max);
            Net = Math.Max(0f, Net - _b.net_burst_relief);
            NetChanged?.Invoke(Net);
            Burst?.Invoke();
        }

        /// <summary>
        /// Tick all active timed powerups; remove expired ones.
        /// Ported from game.gd _tick_powerups (line 323).
        /// </summary>
        void TickPowerups(float delta)
        {
            if (_powerups.Count == 0) return;
            var toRemove = new List<string>();
            // Snapshot keys: on Mono (Unity), updating a dict value via the indexer bumps the
            // collection version and invalidates a live Keys enumerator. (net10/the test harness
            // tolerates it, so this only repros in the editor — snapshot to be runtime-safe.)
            foreach (var key in new List<string>(_powerups.Keys))
            {
                _powerups[key] -= delta;
                if (_powerups[key] <= 0f)
                    toRemove.Add(key);
            }
            foreach (var key in toRemove)
                _powerups.Remove(key);
        }

        float ComboMult()
        {
            // +0.1x per streak, capped at 5x (game.gd _combo_mult)
            return Math.Min(5f, 1f + Combo * 0.1f);
        }

        void ResetCombo()
        {
            if (Combo != 0)
            {
                Combo = 0;
                ComboChanged?.Invoke(0, 1f);
            }
        }

        static float Clamp01(float v) => Math.Max(0f, Math.Min(1f, v));
        static int   Clamp(int v, int lo, int hi) => Math.Max(lo, Math.Min(hi, v));
    }
}
