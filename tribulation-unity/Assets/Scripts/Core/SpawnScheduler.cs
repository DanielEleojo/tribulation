// Pure-C# spawn scheduler. No UnityEngine dependency.
// Ports _current_interval() from spawner.gd (lines 155-162).
// Issue #22: NextKind() 3-cycle replaced by authored HazardPattern table + NextStep(realm).

using System;

namespace Tribulation.Core
{
    public enum HazardKind { Block, Bar, Enemy }

    /// <summary>One authored step in a hazard pattern (lane, kind, full-width flag).</summary>
    public readonly struct HazardStep
    {
        public readonly HazardKind Kind;
        /// <summary>0/1/2. Ignored when FullWidth=true.</summary>
        public readonly int Lane;
        /// <summary>Only meaningful for Block and Bar. Enemy is never full-width.</summary>
        public readonly bool FullWidth;

        public HazardStep(HazardKind kind, int lane, bool fullWidth = false)
        {
            Kind      = kind;
            Lane      = lane;
            FullWidth = fullWidth;
        }
    }

    /// <summary>Authored attack-sequence pattern tagged with the realm it unlocks at.</summary>
    internal sealed class HazardPattern
    {
        public readonly int      MinRealm;
        public readonly HazardStep[] Steps;

        public HazardPattern(int minRealm, HazardStep[] steps)
        {
            MinRealm = minRealm;
            Steps    = steps;
        }
    }

    public class SpawnScheduler
    {
        readonly BalanceData _b;
        readonly Random      _rng;

        // ── Authored pattern table ───────────────────────────────────────────
        // A small set of designed combos. Each is a short (3-5 step) sequence.
        // MinRealm gates which patterns are in the weighted pool.
        //
        // Combo tags:
        //   JUMP-JUMP-DODGE   (low-low-high): two full-width blocks then an enemy
        //   WEAVE             (lane 0→2→1 blocks): force rapid lane changes
        //   PINCER            (enemy + flanking bars): enemy forces dodge into bar
        //   TUNNEL            (full bar + full block): slide then jump, pure reflex
        //   ELITE             (high-realm only): denser enemy mix
        static readonly HazardPattern[] Patterns = new HazardPattern[]
        {
            // ── realm 0+: beginner-accessible combos ──────────────────────────
            new HazardPattern(0, new[]
            {
                // JUMP-JUMP-DODGE: two full-width jump walls then a lane enemy
                new HazardStep(HazardKind.Block, 0, fullWidth: true),
                new HazardStep(HazardKind.Block, 0, fullWidth: true),
                new HazardStep(HazardKind.Enemy, 1),
            }),
            new HazardPattern(0, new[]
            {
                // TUNNEL: full-width slide bar then full-width jump block
                new HazardStep(HazardKind.Bar,   0, fullWidth: true),
                new HazardStep(HazardKind.Block,  0, fullWidth: true),
                new HazardStep(HazardKind.Bar,    0, fullWidth: true),
            }),
            new HazardPattern(0, new[]
            {
                // WEAVE: three lane-specific blocks, left→right→centre
                new HazardStep(HazardKind.Block, 0),
                new HazardStep(HazardKind.Block, 2),
                new HazardStep(HazardKind.Block, 1),
            }),

            // ── realm 1+: intermediate combos ────────────────────────────────
            new HazardPattern(1, new[]
            {
                // PINCER: enemy in centre, then flanking bars on the escape lanes
                new HazardStep(HazardKind.Enemy, 1),
                new HazardStep(HazardKind.Bar,   0),
                new HazardStep(HazardKind.Bar,   2),
                new HazardStep(HazardKind.Block,  1, fullWidth: true),
            }),
            new HazardPattern(1, new[]
            {
                // SLIDE-DODGE-JUMP: bar + enemy pair + block
                new HazardStep(HazardKind.Bar,    0, fullWidth: true),
                new HazardStep(HazardKind.Enemy,  0),
                new HazardStep(HazardKind.Enemy,  2),
                new HazardStep(HazardKind.Block,  0, fullWidth: true),
            }),

            // ── realm 2+: elite combos ────────────────────────────────────────
            new HazardPattern(2, new[]
            {
                // ELITE GAUNTLET: enemy + full block + enemy + bar
                new HazardStep(HazardKind.Enemy,  0),
                new HazardStep(HazardKind.Block,  0, fullWidth: true),
                new HazardStep(HazardKind.Enemy,  2),
                new HazardStep(HazardKind.Bar,    0, fullWidth: true),
                new HazardStep(HazardKind.Enemy,  1),
            }),
        };

        // ── Pattern playback state ───────────────────────────────────────────
        HazardPattern _current;
        int           _stepIndex;

        public SpawnScheduler(BalanceData balance, Random rng = null)
        {
            _b   = balance ?? new BalanceData();
            _rng = rng ?? new Random();
            _current   = PickPattern(0);
            _stepIndex = 0;
        }

        /// <summary>
        /// Port of spawner.gd _current_interval() (lines 155-162).
        /// Phase 1: lerp start_interval→min_interval over ramp_time.
        /// Phase 2: lerp min_interval→hard_min_interval over endless_ramp (past ramp_time).
        /// UNCHANGED — issue #22 must not touch interval logic.
        /// </summary>
        public float CurrentInterval(float elapsed)
        {
            float t = Math.Min(elapsed / Math.Max(_b.spawn_ramp_time, 0.0001f), 1f);
            float i = Lerp(_b.spawn_start_interval, _b.spawn_min_interval, t);
            if (elapsed > _b.spawn_ramp_time)
            {
                float e = Clamp01((elapsed - _b.spawn_ramp_time)
                                  / Math.Max(_b.spawn_endless_ramp, 0.0001f));
                i = Lerp(_b.spawn_min_interval, _b.spawn_hard_min_interval, e);
            }
            return i;
        }

        /// <summary>
        /// Returns the next authored HazardStep for the given realm.
        /// Plays through the current pattern step-by-step; when exhausted, picks a new
        /// pattern from the realm-appropriate pool using the injected RNG.
        /// Replaces the old NextKind() 3-cycle.
        /// </summary>
        public HazardStep NextStep(int realm)
        {
            // If we've played every step in the current pattern, pick the next one.
            if (_stepIndex >= _current.Steps.Length)
            {
                _current   = PickPattern(realm);
                _stepIndex = 0;
            }

            return _current.Steps[_stepIndex++];
        }

        // ── Private helpers ──────────────────────────────────────────────────

        /// <summary>Picks a random pattern whose MinRealm ≤ realm.</summary>
        HazardPattern PickPattern(int realm)
        {
            // Collect eligible patterns (minRealm <= realm).
            // Use a simple reservoir so we don't allocate a list on every call.
            HazardPattern chosen = null;
            int count = 0;
            foreach (var p in Patterns)
            {
                if (p.MinRealm > realm) continue;
                count++;
                // Reservoir sampling: replace with probability 1/count.
                if (_rng.Next(count) == 0) chosen = p;
            }
            // Fallback: first pattern (always realm 0, always eligible).
            return chosen ?? Patterns[0];
        }

        // ── Lightning hazard helpers (issue #7) ─────────────────────────────
        // Pure / static / no RNG — fully deterministic, harness-testable.

        /// <summary>
        /// Mirrors spawner.gd lightning routing (Godot branch order):
        ///   1. inTribulation → always strike.
        ///   2. hasTribulationAbility (realm≥5) + rand01 &lt; 0.55 → strike at Ascension.
        ///   3. else → no lightning.
        /// rand01 is injected (typically Random.value / System.Random) so tests are deterministic.
        /// </summary>
        public static bool ShouldStrikeLightning(bool inTribulation, bool hasTribulationAbility, double rand01)
        {
            if (inTribulation) return true;
            if (hasTribulationAbility && rand01 < 0.55) return true;
            return false;
        }

        /// <summary>
        /// Returns the two lethal-bolt lanes (not the safe lane).
        /// safeLane must be 0, 1, or 2.
        /// </summary>
        public static int[] StrikeLanes(int safeLane)
        {
            // Fast path: enumerate {0,1,2} minus safeLane in order.
            var result = new int[2];
            int idx = 0;
            for (int lane = 0; lane < 3; lane++)
                if (lane != safeLane) result[idx++] = lane;
            return result;
        }

        static float Lerp(float a, float b, float t) => a + (b - a) * t;
        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
