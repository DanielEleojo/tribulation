// EditMode tests for GameCore (pure-C# core assembly, no MonoBehaviour).
// Run in Unity: Window → General → Test Runner → EditMode → Run All
//
// TDD vertical slices — each block follows red→green before the next was written.

using NUnit.Framework;
using Tribulation.Core;

namespace Tribulation.Tests.EditMode
{
    // ─────────────────────────────────────────────────────────────────────────
    // Slice 1 — Realm advancement via realm_span threshold
    // RED : GameCore doesn't exist yet.
    // GREEN: GameCore.UpdateCultivation() begins tribulation at RunProgress >= realm_span[realm],
    //        SurmountTribulation() increments Realm when tribulation timer expires.
    // ─────────────────────────────────────────────────────────────────────────
    [TestFixture]
    public class RealmTests
    {
        static BalanceData MinBalance()
        {
            // Use tiny realm_span values so tests don't need thousands of kills.
            return new BalanceData
            {
                realm_span      = new[] { 5, 10, 20, 40, 80, 999999 },
                qi_max          = 100f,
                qi_per_kill     = 5f,       // less than qi_max so burst doesn't trigger accidentally
                net_push_per_kill = 0.12f,
                net_close_rate  = 0f,       // freeze net so it doesn't kill the player during trib
                net_burst_relief = 0.30f,
            };
        }

        [Test]
        public void KillingEnoughEnemies_StartsHeavenlyTribulation()
        {
            // With realm_span[0]=5 and qi_per_kill=5, killing 1 enemy gives RunProgress=1,
            // killing 5 cumulative enemies (5 progress) triggers tribulation.
            var core = new GameCore(MinBalance());
            core.StartRun();

            for (int i = 0; i < 5; i++)
                core.OnEnemyKilled(1);

            Assert.IsTrue(core.InTribulation, "Should have entered tribulation after filling realm span");
            Assert.AreEqual(0, core.Realm, "Realm must not advance until tribulation is survived");
        }

        [Test]
        public void SurvivingTribulation_AdvancesRealm()
        {
            var core = new GameCore(MinBalance());
            core.StartRun();

            // Fill the span to trigger tribulation
            for (int i = 0; i < 5; i++)
                core.OnEnemyKilled(1);

            Assert.IsTrue(core.InTribulation);

            // Tick past TRIB_DURATION (12 seconds)
            core.Tick(12.1f);

            Assert.IsFalse(core.InTribulation, "Tribulation should end");
            Assert.AreEqual(1, core.Realm, "Realm should advance to 1 after surviving tribulation");
        }

        [Test]
        public void RealmDoesNotAdvanceBeyondMax()
        {
            // Start at realm 5 (Ascension = last realm).
            var b = MinBalance();
            var core = new GameCore(b);
            var save = new SaveData { realm = 5 };
            core.LoadSave(save);
            core.StartRun();

            // Killing enemies should not trigger tribulation (already at last realm)
            for (int i = 0; i < 100; i++)
                core.OnEnemyKilled(1);

            Assert.IsFalse(core.InTribulation, "No tribulation at final realm");
            Assert.AreEqual(5, core.Realm, "Realm must not exceed max");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Slice 2 — Qi economy: gather per kill, clamp at qi_max, burst spends Qi
    // RED : No Qi logic yet.
    // GREEN: OnEnemyKilled adds qi_per_kill*count, Min-clamps at qi_max, QiBurst resets to 0.
    // ─────────────────────────────────────────────────────────────────────────
    [TestFixture]
    public class QiTests
    {
        static BalanceData QiBalance() => new BalanceData
        {
            realm_span       = new[] { 999999, 999999, 999999, 999999, 999999, 999999 },
            qi_max           = 100f,
            qi_per_kill      = 20f,
            net_push_per_kill = 0.12f,
            net_close_rate   = 0f,
            net_burst_relief = 0.30f,
        };

        [Test]
        public void KillingEnemy_IncreasesQi()
        {
            var core = new GameCore(QiBalance());
            core.StartRun();
            float before = core.Qi;
            core.OnEnemyKilled(1);
            Assert.AreEqual(20f, core.Qi - before, 0.001f, "1 kill should add qi_per_kill=20");
        }

        [Test]
        public void Qi_ClampsAtQiMax()
        {
            var core = new GameCore(QiBalance());
            core.StartRun();
            // Kill 10 enemies; each gives 20 Qi -> would be 200 without clamp
            for (int i = 0; i < 6; i++) // 6 kills = 120 Qi raw
                core.OnEnemyKilled(1);
            // After burst (triggered at 5 kills = 100 Qi), Qi resets to 0 and
            // the 6th kill adds 20 again; so Qi == 20f after burst + 1 kill.
            // We just verify it never exceeds qi_max.
            Assert.LessOrEqual(core.Qi, 100f, "Qi must not exceed qi_max");
        }

        [Test]
        public void ReachingQiMax_TriggersBurstAndResetsQi()
        {
            var core = new GameCore(QiBalance());
            core.StartRun();

            bool burstFired = false;
            // Burst resets Qi to 0 and fires QiChanged(0, qi_max)
            core.QiChanged += (q, _) => { if (q == 0f) burstFired = true; };

            // 5 kills × 20 = 100 = qi_max → burst
            for (int i = 0; i < 5; i++)
                core.OnEnemyKilled(1);

            Assert.IsTrue(burstFired, "Qi burst should fire and reset Qi to 0");
            Assert.AreEqual(0f, core.Qi, 0.001f, "Qi should be 0 after burst");
        }

        [Test]
        public void QiBurst_PushesNetBack_ByBurstRelief()
        {
            var b = new BalanceData
            {
                realm_span       = new[] { 999999, 999999, 999999, 999999, 999999, 999999 },
                qi_max           = 20f,    // burst after 1 kill
                qi_per_kill      = 20f,
                net_push_per_kill = 0f,    // isolate burst relief
                net_close_rate   = 0f,
                net_burst_relief = 0.30f,
            };
            var core = new GameCore(b);
            // Manually seed net to 0.50 via a known state is hard without a setter.
            // We'll Tick net up first via a tiny mock: net_close_rate * t.
            // Instead, use save/load to start at a known net is not available (net is transient).
            // Workaround: temporarily use net_close_rate to advance net, then single kill burst.
            var b2 = new BalanceData
            {
                realm_span       = new[] { 999999, 999999, 999999, 999999, 999999, 999999 },
                qi_max           = 20f,
                qi_per_kill      = 20f,
                net_push_per_kill = 0f,
                net_close_rate   = 0.50f,   // 0.50/s
                net_burst_relief = 0.30f,
            };
            var core2 = new GameCore(b2);
            core2.StartRun();
            core2.Tick(1f);    // net = 0.50
            Assert.AreEqual(0.50f, core2.Net, 0.001f, "Net should be 0.50 after 1s at 0.50/s");

            core2.OnEnemyKilled(1);  // triggers burst (qi_per_kill=20 = qi_max=20)

            // After burst, net should be 0.50 - 0.30 = 0.20
            Assert.AreEqual(0.20f, core2.Net, 0.001f, "Burst should relieve net by net_burst_relief=0.30");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Slice 3 — Heavenly-Net: closes at net_close_rate/s, kill pushes back,
    //           reaching 1.0 triggers death (fires Died event)
    // RED : Net logic missing.
    // GREEN: Tick() advances net, OnEnemyKilled pushes back, net>=1 calls Die().
    // ─────────────────────────────────────────────────────────────────────────
    [TestFixture]
    public class NetTests
    {
        static BalanceData NetBalance() => new BalanceData
        {
            realm_span        = new[] { 999999, 999999, 999999, 999999, 999999, 999999 },
            qi_max            = 999f,       // prevent burst from firing
            qi_per_kill       = 1f,
            net_close_rate    = 0.10f,
            net_push_per_kill = 0.12f,
            net_burst_relief  = 0.30f,
        };

        [Test]
        public void Net_ClosesAtNetCloseRatePerSecond()
        {
            var core = new GameCore(NetBalance());
            core.StartRun();
            core.Tick(5f);  // 5s × 0.10 = 0.50
            Assert.AreEqual(0.50f, core.Net, 0.001f);
        }

        [Test]
        public void Kill_PushesNetBack()
        {
            var core = new GameCore(NetBalance());
            core.StartRun();
            core.Tick(2f);  // net = 0.20
            core.OnEnemyKilled(1);  // net -= 0.12 → 0.08
            Assert.AreEqual(0.08f, core.Net, 0.001f);
        }

        [Test]
        public void Net_CannotGoBelowZero()
        {
            var core = new GameCore(NetBalance());
            core.StartRun();
            core.OnEnemyKilled(1);  // would be -0.12 without clamp
            Assert.AreEqual(0f, core.Net, 0.001f, "Net must clamp to 0");
        }

        [Test]
        public void NetReachingOne_FiresDied()
        {
            var core = new GameCore(NetBalance());
            core.StartRun();
            bool died = false;
            core.Died += () => died = true;
            core.Tick(10.1f); // 10.1s × 0.10 = 1.01 → triggers death at 1.0
            Assert.IsTrue(died, "Died event should fire when net reaches 1.0");
            Assert.IsTrue(core.IsDead);
        }

        [Test]
        public void NetFiresNetChangedEvent()
        {
            float lastNet = -1f;
            var core = new GameCore(NetBalance());
            core.NetChanged += n => lastNet = n;
            core.StartRun();
            core.Tick(1f);
            Assert.AreEqual(0.10f, lastNet, 0.001f, "NetChanged event should carry the new net value");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Slice 4 — Combo: increments on kill, resets to 0 on hit
    // RED : Combo logic missing.
    // GREEN: OnEnemyKilled increments Combo, OnPlayerHit resets to 0.
    // ─────────────────────────────────────────────────────────────────────────
    [TestFixture]
    public class ComboTests
    {
        static BalanceData ComboBalance() => new BalanceData
        {
            realm_span        = new[] { 999999, 999999, 999999, 999999, 999999, 999999 },
            qi_max            = 999f,
            qi_per_kill       = 1f,
            net_push_per_kill = 0.12f,
            net_close_rate    = 0f,
            net_burst_relief  = 0.30f,
        };

        [Test]
        public void Kill_IncrementsCombo()
        {
            var core = new GameCore(ComboBalance());
            core.StartRun();
            core.OnEnemyKilled(1);
            Assert.AreEqual(1, core.Combo);
            core.OnEnemyKilled(1);
            Assert.AreEqual(2, core.Combo);
        }

        [Test]
        public void KillingMultipleAtOnce_AddsBatchToCombo()
        {
            var core = new GameCore(ComboBalance());
            core.StartRun();
            core.OnEnemyKilled(3);
            Assert.AreEqual(3, core.Combo);
        }

        [Test]
        public void Hit_ResetsComboToZero()
        {
            var core = new GameCore(ComboBalance());
            core.StartRun();
            core.OnEnemyKilled(5);
            Assert.AreEqual(5, core.Combo);
            core.OnPlayerHit();
            Assert.AreEqual(0, core.Combo, "Hit should reset combo");
        }

        [Test]
        public void Hit_FiresComboChangedWithZero()
        {
            int lastCombo = -1;
            var core = new GameCore(ComboBalance());
            core.ComboChanged += (c, _) => lastCombo = c;
            core.StartRun();
            core.OnEnemyKilled(3);
            core.OnPlayerHit();
            Assert.AreEqual(0, lastCombo, "ComboChanged event should carry 0 after hit");
        }

        [Test]
        public void ComboMult_CapsAtFive()
        {
            // Combo mult is 1 + combo*0.1, capped at 5.0 (game.gd _combo_mult).
            // At combo=50, mult would be 6 without cap; should be 5.
            // We verify via ComboChanged event's mult argument.
            float lastMult = 0f;
            var core = new GameCore(ComboBalance());
            core.ComboChanged += (_, m) => lastMult = m;
            core.StartRun();
            // 50 kills in one call → combo becomes 50
            core.OnEnemyKilled(50);
            Assert.AreEqual(5f, lastMult, 0.001f, "Combo mult must be capped at 5.0");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Slice 5 — Save/Load round-trip: lifetime stats + settings survive a cycle
    // RED : ToSave/LoadSave missing.
    // GREEN: Build core, mutate state, ToSave(), new core, LoadSave(), assert equality.
    // ─────────────────────────────────────────────────────────────────────────
    [TestFixture]
    public class SaveLoadTests
    {
        static BalanceData AnyBalance() => new BalanceData
        {
            realm_span = new[] { 50, 120, 300, 750, 1800, 999999 },
            qi_max     = 100f,
            qi_per_kill = 20f,
            net_push_per_kill = 0.12f,
            net_close_rate = 0f,
            net_burst_relief = 0.30f,
        };

        [Test]
        public void ToSave_ThenLoadSave_RoundTripsLifetimeStats()
        {
            var coreA = new GameCore(AnyBalance());
            // Seed via LoadSave to set realm + lifetime values (the normal path)
            coreA.LoadSave(new SaveData
            {
                realm       = 2,
                totalStones = 9999,
                spent       = 500,
                bestLi      = 3000,
                statRuns    = 42,
                statFoes    = 1234,
                statTribs   = 7,
                statDeaths  = 21,
                musicVol    = 0.6f,
                sfxVol      = 0.7f,
                muted       = true,
            });

            SaveData snap = coreA.ToSave();

            var coreB = new GameCore(AnyBalance());
            coreB.LoadSave(snap);

            Assert.AreEqual(2,    coreB.Realm);
            Assert.AreEqual(9999, coreB.TotalStones);
            Assert.AreEqual(3000, coreB.BestLi);
            Assert.AreEqual(42,   coreB.StatRuns);
            Assert.AreEqual(1234, coreB.StatFoes);
            Assert.AreEqual(7,    coreB.StatTribs);
            Assert.AreEqual(21,   coreB.StatDeaths);
            Assert.AreEqual(0.6f, coreB.MusicVol, 0.001f);
            Assert.AreEqual(0.7f, coreB.SfxVol,   0.001f);
            Assert.IsTrue(coreB.Muted);
        }

        [Test]
        public void LoadSave_ClampsRealmToValidRange()
        {
            var core = new GameCore(AnyBalance());
            core.LoadSave(new SaveData { realm = 99 });   // out of range
            Assert.AreEqual(5, core.Realm, "Realm should clamp to last valid index (5)");
        }

        [Test]
        public void LoadSave_WithNull_DoesNotThrow()
        {
            var core = new GameCore(AnyBalance());
            Assert.DoesNotThrow(() => core.LoadSave(null));
        }

        [Test]
        public void RunStats_AccumulateAfterLoad_AndRoundTrip()
        {
            // Load existing save, run a game (which adds StatRuns/StatFoes), save again.
            var b = new BalanceData
            {
                realm_span = new[] { 999999, 999999, 999999, 999999, 999999, 999999 },
                qi_max = 999f, qi_per_kill = 1f, net_push_per_kill = 0f,
                net_close_rate = 0f, net_burst_relief = 0f,
            };
            var core = new GameCore(b);
            core.LoadSave(new SaveData { statRuns = 10, statFoes = 50 });
            core.StartRun();               // adds 1 to StatRuns
            core.OnEnemyKilled(3);         // adds 3 to StatFoes
            core.OnPlayerHit();            // kills the run (no shield)

            SaveData snap = core.ToSave();
            Assert.AreEqual(11, snap.statRuns, "Run count should include this run");
            Assert.AreEqual(53, snap.statFoes, "Foe count should accumulate");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Slice 6 — Powerup timers: activate→active, Tick past duration→expired
    // RED : ActivatePowerup / IsPowerupActive / TickPowerups missing.
    // GREEN: timed powerups stored in dict, decremented by Tick, removed at 0.
    // ─────────────────────────────────────────────────────────────────────────
    [TestFixture]
    public class PowerupTimerTests
    {
        static BalanceData NoKillBalance() => new BalanceData
        {
            realm_span      = new[] { 999999, 999999, 999999, 999999, 999999, 999999 },
            qi_max          = 999f,
            qi_per_kill     = 0f,
            net_push_per_kill = 0f,
            net_close_rate  = 0f,
            net_burst_relief = 0f,
        };

        [Test]
        public void ActivateMagnet_IsActive_ThenExpiresAfterDuration()
        {
            var core = new GameCore(NoKillBalance());
            core.StartRun();

            core.ActivatePowerup("magnet");
            Assert.IsTrue(core.IsPowerupActive("magnet"), "magnet should be active immediately");

            // Tick just under 8s — still active
            core.Tick(7.9f);
            Assert.IsTrue(core.IsPowerupActive("magnet"), "magnet should still be active at 7.9s");

            // Tick past the remaining 0.2s
            core.Tick(0.2f);
            Assert.IsFalse(core.IsPowerupActive("magnet"), "magnet should have expired after 8s");
        }

        [Test]
        public void ActivateDash_IsActive_ThenExpiresAfterDuration()
        {
            var core = new GameCore(NoKillBalance());
            core.StartRun();
            core.ActivatePowerup("dash");
            Assert.IsTrue(core.IsPowerupActive("dash"));
            core.Tick(3.1f);
            Assert.IsFalse(core.IsPowerupActive("dash"), "dash expires after 3s");
        }

        [Test]
        public void ActivateDouble_IsActive_ThenExpiresAfterDuration()
        {
            var core = new GameCore(NoKillBalance());
            core.StartRun();
            core.ActivatePowerup("double");
            Assert.IsTrue(core.IsPowerupActive("double"));
            core.Tick(10.1f);
            Assert.IsFalse(core.IsPowerupActive("double"), "double expires after 10s");
        }

        [Test]
        public void PowerupIgnoredWhenDead()
        {
            var core = new GameCore(NoKillBalance());
            core.StartRun();
            core.OnPlayerHit(); // kill the run
            core.ActivatePowerup("magnet");
            Assert.IsFalse(core.IsPowerupActive("magnet"), "powerup should not activate when dead");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Slice 7 — double pill doubles soul gain per kill
    // RED : OnEnemyKilled does not yet check IsPowerupActive("double").
    // GREEN: with double active, soul gain is 2× the baseline.
    // ─────────────────────────────────────────────────────────────────────────
    [TestFixture]
    public class DoublePillTests
    {
        static BalanceData DoubleBalance() => new BalanceData
        {
            realm_span       = new[] { 999999, 999999, 999999, 999999, 999999, 999999 },
            qi_max           = 999f,
            qi_per_kill      = 0f,
            net_push_per_kill = 0f,
            net_close_rate   = 0f,
            net_burst_relief = 0f,
        };

        [Test]
        public void KillWithDoubleActive_GivesTwiceTheSouls()
        {
            // Baseline: 1 kill at combo=0 → mult=1.0 → round(1*1)=1 soul.
            var baseCore = new GameCore(DoubleBalance());
            baseCore.StartRun();
            baseCore.OnEnemyKilled(1);
            int baseline = baseCore.Souls;

            // With double: same kill should yield 2× souls.
            var dblCore = new GameCore(DoubleBalance());
            dblCore.StartRun();
            dblCore.ActivatePowerup("double");
            dblCore.OnEnemyKilled(1);
            int withDouble = dblCore.Souls;

            Assert.AreEqual(baseline * 2, withDouble, "double pill must double soul gain");
        }

        [Test]
        public void KillAfterDoubleExpires_GivesNormalSouls()
        {
            var core = new GameCore(DoubleBalance());
            core.StartRun();
            core.ActivatePowerup("double");
            core.Tick(10.1f); // expire double
            Assert.IsFalse(core.IsPowerupActive("double"));
            int before = core.Souls;
            core.OnEnemyKilled(1);
            int after = core.Souls;

            var refCore = new GameCore(DoubleBalance());
            refCore.StartRun();
            refCore.OnEnemyKilled(1);

            Assert.AreEqual(refCore.Souls, after - before, "normal souls after double expires");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Slice 7b — double pill doubles soul gain per orb collect (Batch 3a)
    // RED : OnOrbCollected did not check IsPowerupActive("double").
    // GREEN: after g computed, if double active g *= 2 — same pattern as OnEnemyKilled.
    // ─────────────────────────────────────────────────────────────────────────
    [TestFixture]
    public class DoubleOnOrbTests
    {
        static BalanceData OrbBalance() => new BalanceData
        {
            realm_span       = new[] { 999999, 999999, 999999, 999999, 999999, 999999 },
            qi_max           = 999f,
            qi_per_kill      = 0f,
            net_push_per_kill = 0f,
            net_close_rate   = 0f,
            net_burst_relief = 0f,
        };

        [Test]
        public void OrbWithDoubleActive_GivesTwiceTheSouls()
        {
            // Baseline: 1 orb at combo=0 → mult=1.0 → round(1)=1 soul.
            var baseCore = new GameCore(OrbBalance());
            baseCore.StartRun();
            int soulsBefore = baseCore.Souls;
            baseCore.OnOrbCollected();
            int baselineDelta = baseCore.Souls - soulsBefore;

            // With double active: same orb should yield 2× souls.
            var dblCore = new GameCore(OrbBalance());
            dblCore.StartRun();
            dblCore.ActivatePowerup("double");
            int beforeDouble = dblCore.Souls;
            dblCore.OnOrbCollected();
            int doubleDelta = dblCore.Souls - beforeDouble;

            Assert.AreEqual(baselineDelta * 2, doubleDelta, "double pill must double orb soul gain");
        }

    }

    // ─────────────────────────────────────────────────────────────────────────
    // Slice 8 — surge pill
    //   realm<2: fills Qi to max, relieves Net by 0.30
    //   realm>=2 (has "qi"): fills Qi to max then fires QiBurst (Qi resets to 0,
    //             net pushed back by net_burst_relief)
    // RED : ActivatePowerup("surge") not implemented.
    // GREEN: surge case in ActivatePowerup.
    // ─────────────────────────────────────────────────────────────────────────
    [TestFixture]
    public class SurgePillTests
    {
        BalanceData SurgeBalance(float netCloseRate = 0.50f) => new BalanceData
        {
            realm_span       = new[] { 999999, 999999, 999999, 999999, 999999, 999999 },
            qi_max           = 100f,
            qi_per_kill      = 0f,
            net_push_per_kill = 0f,
            net_close_rate   = netCloseRate,
            net_burst_relief = 0.30f,
        };

        [Test]
        public void Surge_AtRealmZero_FiresQiBurst()
        {
            // Loop redesign: Qi Burst is a core verb from realm 0, so surge bursts immediately
            // (fills Qi to max, then QiBurst resets it to 0).
            var core = new GameCore(SurgeBalance(0f));
            core.StartRun();
            core.ActivatePowerup("surge");
            Assert.AreEqual(0f, core.Qi, 0.001f, "surge at realm 0 fills then bursts Qi → 0");
        }

        [Test]
        public void Surge_AtRealmZero_RelievesNetByBurst()
        {
            var core = new GameCore(SurgeBalance(0.50f));
            core.StartRun();
            core.Tick(1f);  // net = 0.50
            Assert.AreEqual(0.50f, core.Net, 0.001f);
            core.ActivatePowerup("surge");
            // realm 0 now has Qi Burst → QiBurst relieves net by net_burst_relief=0.30 → 0.20
            Assert.AreEqual(0.20f, core.Net, 0.001f, "surge burst relieves net by net_burst_relief");
        }

        [Test]
        public void Surge_AtRealmTwo_TriggersQiBurstAndResetsQiToZero()
        {
            var core = new GameCore(SurgeBalance(0f));
            // Move to realm 2 via LoadSave.
            core.LoadSave(new SaveData { realm = 2 });
            core.StartRun();

            bool burstSeen = false;
            core.QiChanged += (q, _) => { if (q == 0f) burstSeen = true; };

            core.ActivatePowerup("surge");
            // fills to qi_max=100, then HasAbility("qi") true → QiBurst → Qi=0
            Assert.IsTrue(burstSeen, "surge at realm>=2 should fire QiBurst (Qi→0)");
            Assert.AreEqual(0f, core.Qi, 0.001f);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Slice 9 — InSlashReach predicate + HasAbility("slash") gate
    // RED : InSlashReach static method missing.
    // GREEN: static pure method returns correct bool; slash gated at realm<2.
    // ─────────────────────────────────────────────────────────────────────────
    [TestFixture]
    public class SlashReachTests
    {
        [Test]
        public void InSlashReach_InsideRangeAndTol_ReturnsTrue()
        {
            // ahead=2.0 (in front), lateral=1.0 (< tol=1.4)  → inside
            Assert.IsTrue(GameCore.InSlashReach(2.0f, 1.0f, 4.0f, 1.4f));
        }

        [Test]
        public void InSlashReach_AheadBeyondRange_ReturnsFalse()
        {
            // ahead=5.0 > range=4.0  → outside
            Assert.IsFalse(GameCore.InSlashReach(5.0f, 0f, 4.0f, 1.4f));
        }

        [Test]
        public void InSlashReach_LateralBeyondTol_ReturnsFalse()
        {
            // ahead=2.0, lateral=1.5 > tol=1.4  → outside
            Assert.IsFalse(GameCore.InSlashReach(2.0f, 1.5f, 4.0f, 1.4f));
        }

        [Test]
        public void InSlashReach_SlightlyBehindPlayer_AllowedByMinus1Clamp()
        {
            // ahead=-0.5 ≥ -1.0 → still inside (enemy just behind player's centre)
            Assert.IsTrue(GameCore.InSlashReach(-0.5f, 0f, 4.0f, 1.4f));
        }

        [Test]
        public void InSlashReach_TooFarBehind_ReturnsFalse()
        {
            // ahead=-1.5 < -1.0  → outside
            Assert.IsFalse(GameCore.InSlashReach(-1.5f, 0f, 4.0f, 1.4f));
        }

        [Test]
        public void CoreVerbsUngated_SpectacleStillGated()
        {
            // Loop redesign: slash + Qi Burst are core survival verbs, available from realm 0.
            // Realms instead hand out spectacle (double-jump r1, glide r3, sword-flight r4).
            var b = new BalanceData
            {
                realm_span       = new[] { 999999, 999999, 999999, 999999, 999999, 999999 },
                qi_max           = 999f, qi_per_kill = 0f, net_push_per_kill = 0f,
                net_close_rate   = 0f, net_burst_relief = 0f,
            };
            var core0 = new GameCore(b);
            Assert.IsTrue(core0.HasAbility("slash"), "realm 0 — slash available (core verb)");
            Assert.IsTrue(core0.HasAbility("qi"),    "realm 0 — Qi Burst available (core verb)");
            Assert.IsFalse(core0.HasAbility("glide"),       "realm 0 — glide still gated");
            Assert.IsFalse(core0.HasAbility("swordflight"), "realm 0 — sword-flight still gated");

            core0.LoadSave(new SaveData { realm = 3 });
            Assert.IsTrue(core0.HasAbility("glide"), "realm 3 — glide unlocked");
        }

        [Test]
        public void SlashRange_IncreasesWithRealm()
        {
            var b = new BalanceData
            {
                realm_span       = new[] { 999999, 999999, 999999, 999999, 999999, 999999 },
                qi_max = 999f, qi_per_kill = 0f, net_push_per_kill = 0f,
                net_close_rate = 0f, net_burst_relief = 0f,
            };
            var core = new GameCore(b);

            core.LoadSave(new SaveData { realm = 0 });
            float r0 = core.SlashRange;

            core.LoadSave(new SaveData { realm = 2 });
            float r2 = core.SlashRange;

            Assert.Greater(r2, r0, "SlashRange should increase from realm 0 to realm 2");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Slice 11 — Locomotion helpers: MaxAirJumps + GlideGravity (issue #6)
    // RED  : Locomotion class missing.
    // GREEN: Pure helpers in Locomotion.cs (no UnityEngine).
    // ─────────────────────────────────────────────────────────────────────────
    [TestFixture]
    public class LocomotionTests
    {
        // ── MaxAirJumps ──────────────────────────────────────────────────────

        [Test]
        public void MaxAirJumps_WithDoubleJump_ReturnsOne()
        {
            Assert.AreEqual(1, Locomotion.MaxAirJumps(true));
        }

        [Test]
        public void MaxAirJumps_WithoutDoubleJump_ReturnsZero()
        {
            Assert.AreEqual(0, Locomotion.MaxAirJumps(false));
        }

        // ── GlideGravity — reduced only when ALL conditions hold ─────────────

        [Test]
        public void GlideGravity_AllConditionsTrue_ReturnsReducedGravity()
        {
            // grounded=false, vy<0, canGlide=true, glideHeld=true → 48 * 0.22
            float result = Locomotion.GlideGravity(48f, grounded: false, vy: -1f, canGlide: true, glideHeld: true);
            Assert.AreEqual(48f * 0.22f, result, 0.0001f);
        }

        [Test]
        public void GlideGravity_WhenGrounded_ReturnsFullGravity()
        {
            float result = Locomotion.GlideGravity(48f, grounded: true, vy: -1f, canGlide: true, glideHeld: true);
            Assert.AreEqual(48f, result, 0.0001f);
        }

        [Test]
        public void GlideGravity_WhenVyNotNegative_ReturnsFullGravity()
        {
            float result = Locomotion.GlideGravity(48f, grounded: false, vy: 0f, canGlide: true, glideHeld: true);
            Assert.AreEqual(48f, result, 0.0001f);
        }

        [Test]
        public void GlideGravity_WhenCannotGlide_ReturnsFullGravity()
        {
            float result = Locomotion.GlideGravity(48f, grounded: false, vy: -1f, canGlide: false, glideHeld: true);
            Assert.AreEqual(48f, result, 0.0001f);
        }

        [Test]
        public void GlideGravity_WhenNotHeld_ReturnsFullGravity()
        {
            float result = Locomotion.GlideGravity(48f, grounded: false, vy: -1f, canGlide: true, glideHeld: false);
            Assert.AreEqual(48f, result, 0.0001f);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Slice 10 — Gate rewards/penalties (issue #5)
    // RED : GameCore.OnGate missing.
    // GREEN: safe gate raises Qi by 25 (clamped at qi_max) and lowers Net by 0.15
    //        (clamped ≥ 0); death gate lowers Qi by 40 (clamped ≥ 0), raises Net by
    //        0.30 (clamped ≤ 1), and resets combo to 0.
    // ─────────────────────────────────────────────────────────────────────────
    [TestFixture]
    public class GateTests
    {
        static BalanceData GateBalance() => new BalanceData
        {
            realm_span        = new[] { 999999, 999999, 999999, 999999, 999999, 999999 },
            qi_max            = 100f,
            qi_per_kill       = 0f,
            net_push_per_kill = 0f,
            net_close_rate    = 0f,
            net_burst_relief  = 0.30f,
        };

        // Helper: advance Net to a known value via Tick.
        static GameCore CoreWithNet(float net)
        {
            var b = new BalanceData
            {
                realm_span        = new[] { 999999, 999999, 999999, 999999, 999999, 999999 },
                qi_max            = 100f,
                qi_per_kill       = 0f,
                net_push_per_kill = 0f,
                net_close_rate    = net,   // 1 second → Net == net
                net_burst_relief  = 0.30f,
            };
            var c = new GameCore(b);
            c.StartRun();
            if (net > 0f) c.Tick(1f); // net_close_rate × 1s = target net
            return c;
        }

        // Helper: advance Qi via a BalanceData trick — qi_per_kill applied by 1 kill.
        static GameCore CoreWithQi(float qi)
        {
            var b = new BalanceData
            {
                realm_span        = new[] { 999999, 999999, 999999, 999999, 999999, 999999 },
                qi_max            = 100f,
                qi_per_kill       = qi,   // 1 kill → Qi == qi (clamped at 100)
                net_push_per_kill = 0f,
                net_close_rate    = 0f,
                net_burst_relief  = 0.30f,
            };
            var c = new GameCore(b);
            c.StartRun();
            if (qi > 0f) c.OnEnemyKilled(1);
            return c;
        }

        // ── Test 1: safe gate raises Qi by 25, lowers Net by 0.15 ────────────

        [Test]
        public void SafeGate_RaisesQiBy25()
        {
            var core = CoreWithNet(0.50f);
            float before = core.Qi; // 0
            core.OnGate(safe: true);
            Assert.AreEqual(before + 25f, core.Qi, 0.001f, "safe gate should add 25 Qi");
        }

        [Test]
        public void SafeGate_LowersNetBy015()
        {
            var core = CoreWithNet(0.50f);
            core.OnGate(safe: true);
            Assert.AreEqual(0.35f, core.Net, 0.001f, "safe gate should reduce Net by 0.15");
        }

        [Test]
        public void SafeGate_Qi_ClampsAtQiMax()
        {
            var core = CoreWithQi(90f); // Qi = 90
            core.OnGate(safe: true);   // +25 → would be 115, clamped to 100
            Assert.AreEqual(100f, core.Qi, 0.001f, "safe gate Qi must not exceed qi_max");
        }

        [Test]
        public void SafeGate_Net_ClampsAtZero()
        {
            var core = CoreWithNet(0f); // Net = 0
            core.OnGate(safe: true);   // -0.15 → clamped to 0
            Assert.AreEqual(0f, core.Net, 0.001f, "safe gate Net must not go below 0");
        }

        // ── Test 2: death gate lowers Qi by 40, raises Net by 0.30, resets combo ──

        [Test]
        public void DeathGate_LowersQiBy40()
        {
            var core = CoreWithQi(60f); // Qi = 60
            core.OnGate(safe: false);
            Assert.AreEqual(20f, core.Qi, 0.001f, "death gate should subtract 40 Qi");
        }

        [Test]
        public void DeathGate_RaisesNetBy030()
        {
            var core = CoreWithNet(0.20f);
            core.OnGate(safe: false);
            Assert.AreEqual(0.50f, core.Net, 0.001f, "death gate should add 0.30 Net");
        }

        [Test]
        public void DeathGate_ResetsCombo()
        {
            // Build a core with combo > 0
            var b = new BalanceData
            {
                realm_span        = new[] { 999999, 999999, 999999, 999999, 999999, 999999 },
                qi_max            = 100f,
                qi_per_kill       = 10f,
                net_push_per_kill = 0f,
                net_close_rate    = 0f,
                net_burst_relief  = 0.30f,
            };
            var core = new GameCore(b);
            core.StartRun();
            core.OnEnemyKilled(3); // combo = 3
            Assert.AreEqual(3, core.Combo);

            core.OnGate(safe: false);
            Assert.AreEqual(0, core.Combo, "death gate should reset combo to 0");
        }

        [Test]
        public void DeathGate_Qi_ClampsAtZero()
        {
            var core = CoreWithQi(20f); // Qi = 20
            core.OnGate(safe: false);  // -40 → clamped to 0
            Assert.AreEqual(0f, core.Qi, 0.001f, "death gate Qi must not go below 0");
        }

        // ── Test 3 (edge): death gate at Net 0.80 → Net clamps to 1.0 ────────

        [Test]
        public void DeathGate_AtNet080_ClampsNetToOne()
        {
            var core = CoreWithNet(0.80f);
            core.OnGate(safe: false); // +0.30 → would be 1.10, clamped to 1.0
            Assert.AreEqual(1.0f, core.Net, 0.001f,
                "death gate Net must clamp to 1.0 (death then resolves via Tick)");
        }

        // ── Guard: no-op when dead ────────────────────────────────────────────

        [Test]
        public void OnGate_NoOp_WhenDead()
        {
            var core = CoreWithQi(50f);
            core.OnPlayerHit(); // kill the run
            Assert.IsTrue(core.IsDead);
            float qiBefore = core.Qi;
            core.OnGate(safe: true);
            Assert.AreEqual(qiBefore, core.Qi, 0.001f, "OnGate should be no-op when dead");
        }

        [Test]
        public void OnGate_NoOp_WhenNotStarted()
        {
            var core = new GameCore(GateBalance());
            // Do not call StartRun
            core.OnGate(safe: true);
            Assert.AreEqual(0f, core.Qi, 0.001f, "OnGate should be no-op when not started");
        }

        // ── Events fire ──────────────────────────────────────────────────────

        [Test]
        public void SafeGate_FiresQiChangedAndNetChanged()
        {
            var core = CoreWithNet(0.50f);
            bool qiFired = false, netFired = false;
            core.QiChanged  += (q, _) => qiFired  = true;
            core.NetChanged += n      => netFired  = true;
            core.OnGate(safe: true);
            Assert.IsTrue(qiFired,  "safe gate must fire QiChanged");
            Assert.IsTrue(netFired, "safe gate must fire NetChanged");
        }

        [Test]
        public void DeathGate_FiresQiChangedNetChangedComboChanged()
        {
            var b = new BalanceData
            {
                realm_span        = new[] { 999999, 999999, 999999, 999999, 999999, 999999 },
                qi_max            = 100f,
                qi_per_kill       = 10f,
                net_push_per_kill = 0f,
                net_close_rate    = 0.20f,
                net_burst_relief  = 0.30f,
            };
            var core = new GameCore(b);
            core.StartRun();
            core.Tick(1f);         // net = 0.20
            core.OnEnemyKilled(1); // combo = 1

            bool qiFired = false, netFired = false, comboFired = false;
            core.QiChanged   += (q, _) => qiFired   = true;
            core.NetChanged  += n      => netFired   = true;
            core.ComboChanged += (c, _) => comboFired = true;
            core.OnGate(safe: false);
            Assert.IsTrue(qiFired,    "death gate must fire QiChanged");
            Assert.IsTrue(netFired,   "death gate must fire NetChanged");
            Assert.IsTrue(comboFired, "death gate must fire ComboChanged");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Slice 12 — RecordDistance updates BestLi only on improvement
    // ─────────────────────────────────────────────────────────────────────────
    [TestFixture]
    public class RecordDistanceTests
    {
        static BalanceData B() => new BalanceData
        {
            realm_span = new[] { 999999, 999999, 999999, 999999, 999999, 999999 },
            qi_max = 100f, qi_per_kill = 0f, net_push_per_kill = 0f,
            net_close_rate = 0f, net_burst_relief = 0f,
        };

        [Test]
        public void RecordDistance_UpdatesBestLi_OnlyOnImprovement()
        {
            var core = new GameCore(B());
            core.StartRun();

            core.RecordDistance(500);
            Assert.AreEqual(500, core.BestLi, "first record should set BestLi to 500");

            core.RecordDistance(300);
            Assert.AreEqual(500, core.BestLi, "lesser distance should not reduce BestLi");

            core.RecordDistance(900);
            Assert.AreEqual(900, core.BestLi, "greater distance should update BestLi to 900");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Slice 13 — DifficultyOffset: realm × difficulty_per_realm (Batch 3c)
    // Ported from game.gd start_game: var off := float(realm) * DIFFICULTY_PER_REALM.
    // ─────────────────────────────────────────────────────────────────────────
    [TestFixture]
    public class DifficultyOffsetTests
    {
        static BalanceData OffsetBalance() => new BalanceData
        {
            realm_span        = new[] { 999999, 999999, 999999, 999999, 999999, 999999 },
            qi_max            = 100f,
            qi_per_kill       = 0f,
            net_push_per_kill = 0f,
            net_close_rate    = 0f,
            net_burst_relief  = 0f,
            difficulty_per_realm = 12f,
        };

        [Test]
        public void DifficultyOffset_ScalesWithRealm()
        {
            // realm 0 → 0; realm 2 → 2 × 12 = 24  (game.gd: off = float(realm)*DIFFICULTY_PER_REALM)
            var core = new GameCore(OffsetBalance());
            Assert.AreEqual(0f, core.DifficultyOffset(), 0.001f, "realm 0 → offset 0");

            core.LoadSave(new SaveData { realm = 2 });
            Assert.AreEqual(24f, core.DifficultyOffset(), 0.001f,
                "realm 2 × difficulty_per_realm=12 → offset 24");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Slice 14 — Cultivation Trials (Batch 3b)
    //   Roll 3 distinct vows per run; TrialAdd/Max track progress; CheckTrial
    //   awards Souls+TotalStones and fires TrialFulfilled on completion.
    // ─────────────────────────────────────────────────────────────────────────
    [TestFixture]
    public class CultivationTrialTests
    {
        static BalanceData TrialBalance() => new BalanceData
        {
            realm_span        = new[] { 999999, 999999, 999999, 999999, 999999, 999999 },
            qi_max            = 999f,
            qi_per_kill       = 0f,
            net_push_per_kill = 0f,
            net_close_rate    = 0f,
            net_burst_relief  = 0f,
        };

        static readonly int[] ValidGoalsSlay    = { 8,   16,  28   };
        static readonly int[] ValidGoalsLi      = { 400, 900, 1600 };
        static readonly int[] ValidGoalsQi      = { 15,  30,  55   };
        static readonly int[] ValidGoalsCombo   = { 8,   16,  28   };
        static readonly int[] ValidGoalsSurvive = { 30,  60,  100  };
        static readonly int[] ValidRewards      = { 40,  90,  170  };

        // ── Test 1: RollTrials produces 3 distinct trials with valid tiers ────

        [Test]
        public void RollTrials_Produces3DistinctTrialsWithValidTierValues()
        {
            var core = new GameCore(TrialBalance());
            var rng  = new System.Random(42);
            core.RollTrials(rng);

            Assert.AreEqual(3, core.Trials.Count, "should roll exactly 3 trials");

            // All ids must be distinct
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var t in core.Trials)
                Assert.IsTrue(ids.Add(t.Id), $"duplicate trial id: {t.Id}");

            // Each trial's Goal and Reward must appear in the corresponding template tier lists
            foreach (var t in core.Trials)
            {
                Assert.Contains(t.Reward, ValidRewards, $"reward {t.Reward} not a valid tier for {t.Id}");
                int[] validGoals = t.Id switch
                {
                    "slay"    => ValidGoalsSlay,
                    "li"      => ValidGoalsLi,
                    "qi"      => ValidGoalsQi,
                    "combo"   => ValidGoalsCombo,
                    "survive" => ValidGoalsSurvive,
                    _         => throw new System.Exception($"unknown id {t.Id}"),
                };
                Assert.Contains(t.Goal, validGoals, $"goal {t.Goal} not a valid tier for {t.Id}");
            }
        }

        // ── Test 2: TrialAdd completes trial, awards souls, fires TrialFulfilled ──

        [Test]
        public void TrialAdd_WhenGoalMet_CompletesTrial_AwardsSouls_FiresEvent()
        {
            var core = new GameCore(TrialBalance());
            core.StartRun(); // rolls trials with internal _rng

            // Override: roll with a known seed so we can inspect Trials[0]
            core.RollTrials(new System.Random(7));
            var t0 = core.Trials[0];
            Assert.IsFalse(t0.Done, "trial should start incomplete");

            int fulfilledReward = -1;
            core.TrialFulfilled += r => fulfilledReward = r;

            int soulsBefore = core.Souls;
            core.TrialAdd(t0.Id, t0.Goal); // meet the goal in one shot

            Assert.IsTrue(t0.Done, "trial should be done after goal met");
            Assert.AreEqual(t0.Reward, core.Souls - soulsBefore, "Souls should increase by Reward");
            Assert.AreEqual(t0.Reward, fulfilledReward, "TrialFulfilled should carry the reward");
        }

        // ── Test 3: TrialMax semantics (no regression, completion on reach) ───

        [Test]
        public void TrialMax_DoesNotDecrease_AndCompletesOnGoalReached()
        {
            var core = new GameCore(TrialBalance());
            core.StartRun();
            core.RollTrials(new System.Random(99));

            // Find the "combo" or any trial to use
            var t = core.Trials[0];
            string id = t.Id;

            // Raise to 5, then try to lower to 3 — should stay at 5
            core.TrialMax(id, 5f);
            Assert.AreEqual(5f, t.Progress, 0.001f, "TrialMax should set progress to 5");
            core.TrialMax(id, 3f);
            Assert.AreEqual(5f, t.Progress, 0.001f, "TrialMax must not decrease progress");

            // Now reach the goal
            core.TrialMax(id, t.Goal);
            Assert.IsTrue(t.Done, "trial should complete when TrialMax reaches goal");
        }
    }

    // Slice 11 — Restart after death re-inits the run but keeps realm + lifetime.
    [TestFixture]
    public class RestartTests
    {
        static BalanceData B() => new BalanceData
        {
            realm_span = new[] { 999999, 999999, 999999, 999999, 999999, 999999 },
            qi_max = 100f, qi_per_kill = 1f, net_close_rate = 0.1f,
            net_push_per_kill = 0f, net_burst_relief = 0.3f,
        };

        [Test]
        public void RestartRun_AfterDeath_ClearsDeathAndResetsRunState()
        {
            var core = new GameCore(B());
            core.LoadSave(new SaveData { realm = 3, totalStones = 500 });
            core.StartRun();
            core.Tick(11f);                  // net closes past 1.0 → death
            Assert.IsTrue(core.IsDead);

            core.RestartRun();

            Assert.IsFalse(core.IsDead,   "restart clears death");
            Assert.IsTrue(core.IsStarted, "restart begins a fresh run");
            Assert.AreEqual(0f, core.Net, 0.001f, "net resets");
            Assert.AreEqual(0,  core.Combo, "combo resets");
            Assert.AreEqual(3,  core.Realm, "realm persists across death");
            // realm=3 means r1+r2+r3 achievements unlock on first Die() call (3 × ACH_REWARD = 450)
            Assert.GreaterOrEqual(core.TotalStones, 500, "lifetime stones persist (may include ach rewards)");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Slice 15 — Upgrade system (Cultivation Shop data/logic foundation)
    // RED : TryBuyUpgrade / NextUpgradeCost / SpendableStones / UpgradeLevel missing.
    // GREEN: upgrade definitions + purchase logic + persistence round-trip.
    // ─────────────────────────────────────────────────────────────────────────
    [TestFixture]
    public class UpgradeTests
    {
        static BalanceData UpgradeBalance() => new BalanceData
        {
            realm_span        = new[] { 999999, 999999, 999999, 999999, 999999, 999999 },
            qi_max            = 200f,
            qi_per_kill       = 10f,
            net_push_per_kill = 0f,
            net_close_rate    = 0f,
            net_burst_relief  = 0f,
        };

        // Find index of an upgrade by id (matches _upgradeDefs order)
        static int IdxOf(GameCore core, string id)
        {
            for (int i = 0; i < core.Upgrades.Count; i++)
                if (core.Upgrades[i].Id == id) return i;
            throw new System.Exception($"Upgrade id '{id}' not found");
        }

        // ── 1. TryBuyUpgrade succeeds when affordable ─────────────────────────

        [Test]
        public void TryBuyUpgrade_WhenAffordable_IncrementsLevelAndDeductsStones()
        {
            var core = new GameCore(UpgradeBalance());
            // Give 500 stones via LoadSave so we can afford the first qi_flow (cost = 50*1 = 50)
            core.LoadSave(new SaveData { totalStones = 500 });

            int idx = IdxOf(core, "qi_flow");
            int costBefore = core.NextUpgradeCost(idx); // should be 50
            int spendableBefore = core.SpendableStones;

            bool bought = core.TryBuyUpgrade(idx);

            Assert.IsTrue(bought, "should succeed when affordable");
            Assert.AreEqual(1, core.UpgradeLevel(idx), "level should be 1 after first purchase");
            Assert.AreEqual(spendableBefore - costBefore, core.SpendableStones,
                "spendable stones should decrease by the cost");
        }

        // ── 2. TryBuyUpgrade fails when too expensive ─────────────────────────

        [Test]
        public void TryBuyUpgrade_WhenTooExpensive_ReturnsFalseAndLevelUnchanged()
        {
            var core = new GameCore(UpgradeBalance());
            // Give only 10 stones — first qi_flow costs 50
            core.LoadSave(new SaveData { totalStones = 10 });

            int idx = IdxOf(core, "qi_flow");
            bool bought = core.TryBuyUpgrade(idx);

            Assert.IsFalse(bought, "should fail when insufficient funds");
            Assert.AreEqual(0, core.UpgradeLevel(idx), "level must remain 0");
        }

        // ── 3. Cannot exceed MaxLevel ──────────────────────────────────────────

        [Test]
        public void TryBuyUpgrade_CannotExceedMaxLevel()
        {
            var core = new GameCore(UpgradeBalance());
            // qi_flow costs 50, 100, 150 → total 300 to max; give plenty
            core.LoadSave(new SaveData { totalStones = 9999 });

            int idx = IdxOf(core, "qi_flow"); // maxLevel = 3

            // Buy 3 times to reach max
            for (int i = 0; i < 3; i++)
                Assert.IsTrue(core.TryBuyUpgrade(idx), $"buy {i+1} should succeed");

            Assert.AreEqual(3, core.UpgradeLevel(idx), "level should be 3 (maxLevel)");

            // 4th attempt must fail
            bool fourth = core.TryBuyUpgrade(idx);
            Assert.IsFalse(fourth, "4th purchase must fail — already at maxLevel");
            Assert.AreEqual(3, core.UpgradeLevel(idx), "level must stay at 3");
        }

        // ── 4. NextUpgradeCost rises per level and returns -1 at max ──────────

        [Test]
        public void NextUpgradeCost_RisesPerLevel_AndReturnsNegativeOneAtMax()
        {
            var core = new GameCore(UpgradeBalance());
            core.LoadSave(new SaveData { totalStones = 9999 });

            int idx = IdxOf(core, "qi_flow"); // baseCost=50
            // Level 0→1 cost = 50*1 = 50
            Assert.AreEqual(50, core.NextUpgradeCost(idx), "L0→1 cost = baseCost*1 = 50");
            core.TryBuyUpgrade(idx);

            // Level 1→2 cost = 50*2 = 100
            Assert.AreEqual(100, core.NextUpgradeCost(idx), "L1→2 cost = baseCost*2 = 100");
            core.TryBuyUpgrade(idx);

            // Level 2→3 cost = 50*3 = 150
            Assert.AreEqual(150, core.NextUpgradeCost(idx), "L2→3 cost = baseCost*3 = 150");
            core.TryBuyUpgrade(idx);

            // At max, returns -1
            Assert.AreEqual(-1, core.NextUpgradeCost(idx), "at maxLevel NextUpgradeCost must return -1");
        }

        // ── 5a. spirit_root sets Qi at run start ──────────────────────────────

        [Test]
        public void SpiritRoot_Level2_SetsQiTo50AtRunStart()
        {
            var core = new GameCore(UpgradeBalance()); // qi_max=200
            core.LoadSave(new SaveData { totalStones = 9999 });

            int idx = IdxOf(core, "spirit_root"); // baseCost=80; L0→1=80, L1→2=160
            core.TryBuyUpgrade(idx); // level 1
            core.TryBuyUpgrade(idx); // level 2 → startQi = 25*2 = 50

            core.StartRun();

            Assert.AreEqual(50f, core.Qi, 0.001f,
                "spirit_root level 2 should set starting Qi to 25*2=50");
        }

        // ── 5b. qi_flow boosts Qi gain from kills ─────────────────────────────

        [Test]
        public void QiFlow_Level1_IncreasesQiGainFromKills()
        {
            var balance = UpgradeBalance(); // qi_per_kill=10, qi_max=200

            // Baseline core (no upgrades)
            var baseCore = new GameCore(balance);
            baseCore.StartRun();
            baseCore.OnEnemyKilled(1);
            float baseQi = baseCore.Qi; // 10

            // Upgraded core with qi_flow level 1 → QiMult = 1.15
            var upgCore = new GameCore(balance);
            upgCore.LoadSave(new SaveData { totalStones = 9999 });
            int idx = IdxOf(upgCore, "qi_flow");
            upgCore.TryBuyUpgrade(idx); // level 1
            upgCore.StartRun();
            upgCore.OnEnemyKilled(1);
            float upgQi = upgCore.Qi; // 10 * 1.15 = 11.5 ... but StartQiBonus=0 for qi_flow

            Assert.Greater(upgQi, baseQi,
                "qi_flow level≥1 should increase Qi gained per kill");
        }

        // ── 5c. qi_flow boosts Qi gain from orbs ─────────────────────────────

        [Test]
        public void QiFlow_Level1_IncreasesQiGainFromOrbs()
        {
            var balance = UpgradeBalance(); // qi_max=200

            // Baseline
            var baseCore = new GameCore(balance);
            baseCore.StartRun();
            baseCore.OnOrbCollected();
            float baseQi = baseCore.Qi;

            // With qi_flow level 1
            var upgCore = new GameCore(balance);
            upgCore.LoadSave(new SaveData { totalStones = 9999 });
            int idx = IdxOf(upgCore, "qi_flow");
            upgCore.TryBuyUpgrade(idx);
            upgCore.StartRun();
            upgCore.OnOrbCollected();
            float upgQi = upgCore.Qi;

            Assert.Greater(upgQi, baseQi,
                "qi_flow level≥1 should increase Qi gained per orb");
        }

        // ── 6. Save round-trip: levels + spent survive LoadSave ───────────────

        [Test]
        public void UpgradeLevels_RoundTripThroughSaveData()
        {
            var coreA = new GameCore(UpgradeBalance());
            coreA.LoadSave(new SaveData { totalStones = 9999 });

            int idxFlow  = IdxOf(coreA, "qi_flow");      // buy 2×
            int idxSense = IdxOf(coreA, "stone_sense");  // buy 1×
            coreA.TryBuyUpgrade(idxFlow);
            coreA.TryBuyUpgrade(idxFlow);
            coreA.TryBuyUpgrade(idxSense);

            int spentA = 9999 - coreA.SpendableStones;

            // Snapshot and restore
            SaveData snap = coreA.ToSave();
            var coreB = new GameCore(UpgradeBalance());
            coreB.LoadSave(snap);

            Assert.AreEqual(2, coreB.UpgradeLevel(idxFlow),
                "qi_flow level should survive round-trip");
            Assert.AreEqual(1, coreB.UpgradeLevel(idxSense),
                "stone_sense level should survive round-trip");
            Assert.AreEqual(coreA.SpendableStones, coreB.SpendableStones,
                "SpendableStones must match after round-trip");
        }

        // ── 7. heavens_favor slows net-close pressure ─────────────────────────

        [Test]
        public void HeavensFavor_Level1_SlowsNetCloseRate()
        {
            // net_close_rate = 0.10; with level 1 → NetCloseMult = 0.90 → effective rate = 0.09
            var b = new BalanceData
            {
                realm_span        = new[] { 999999, 999999, 999999, 999999, 999999, 999999 },
                qi_max            = 999f,
                qi_per_kill       = 0f,
                net_push_per_kill = 0f,
                net_close_rate    = 0.10f,
                net_burst_relief  = 0f,
            };

            // Baseline (no upgrade)
            var baseCore = new GameCore(b);
            baseCore.StartRun();
            baseCore.Tick(10f);
            float baseNet = baseCore.Net; // 0.10 * 10 = 1.0 → capped at 1.0 (dies); use Tick carefully
            // Re-run safely with 5s
            var baseCore2 = new GameCore(b);
            baseCore2.StartRun();
            baseCore2.Tick(5f);
            float baseNet5 = baseCore2.Net; // 0.50

            // Upgraded core with heavens_favor level 1
            var upgCore = new GameCore(b);
            upgCore.LoadSave(new SaveData { totalStones = 9999 });
            int idx = IdxOf(upgCore, "heavens_favor");
            upgCore.TryBuyUpgrade(idx); // level 1 → NetCloseMult = 0.90
            upgCore.StartRun();
            upgCore.Tick(5f);
            float upgNet5 = upgCore.Net; // 0.10 * 0.90 * 5 = 0.45

            Assert.Less(upgNet5, baseNet5,
                "heavens_favor level 1 should result in lower net after same time");
        }

        // ── 8. stone_sense increases TotalStones from kills ───────────────────

        [Test]
        public void StoneSense_Level1_IncreasesTotalStonesFromKills()
        {
            var balance = UpgradeBalance(); // qi_per_kill=10, qi_max=200

            // Kill 10 enemies at once so base soul gain is large enough for 1.20x to show a diff.
            // Baseline: 10 kills, combo starts at 0 → mult=1 → g=round(10*1)=10
            var baseCore = new GameCore(balance);
            baseCore.StartRun();
            int stonesBefore = baseCore.TotalStones;
            baseCore.OnEnemyKilled(10);
            int baseGain = baseCore.TotalStones - stonesBefore;

            // With stone_sense level 1 → StoneMult = 1.20 → stoneGain=round(10*1.20)=12
            var upgCore = new GameCore(balance);
            upgCore.LoadSave(new SaveData { totalStones = 9999 });
            int idx = IdxOf(upgCore, "stone_sense");
            upgCore.TryBuyUpgrade(idx);
            upgCore.StartRun();
            int stonesBeforeUpg = upgCore.TotalStones;
            upgCore.OnEnemyKilled(10);
            int upgGain = upgCore.TotalStones - stonesBeforeUpg;

            Assert.Greater(upgGain, baseGain,
                "stone_sense level≥1 should yield more TotalStones per kill");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Slice 10 — ResetCultivation (issue #11 accept criterion: confirm-gated reset)
    // RED : GameCore.ResetCultivation() does not exist yet.
    // GREEN: zeroes realm/stones/spent/best/runProgress/all upgrade levels;
    //        leaves lifetime stats (statRuns/statFoes/statTribs/statDeaths) intact;
    //        leaves learnedLessons intact;
    //        ToSave→LoadSave round-trip persists the zeroed values.
    // ─────────────────────────────────────────────────────────────────────────
    [TestFixture]
    public class ResetCultivationTests
    {
        static BalanceData ResetBalance() => new BalanceData
        {
            realm_span        = new[] { 999999, 999999, 999999, 999999, 999999, 999999 },
            qi_max            = 999f,
            qi_per_kill       = 0f,
            net_push_per_kill = 0f,
            net_close_rate    = 0f,
            net_burst_relief  = 0f,
        };

        GameCore BuildAdvancedCore()
        {
            var core = new GameCore(ResetBalance());
            // Load a save that has realm 2, stones, spent, bestLi, lifetime stats, and a learned lesson.
            // statRuns=9 so that after StartRun() (which increments by 1) it becomes 10.
            var save = new SaveData
            {
                realm         = 2,
                totalStones   = 500,
                spent         = 100,
                bestLi        = 1234,
                statRuns      = 9,
                statFoes      = 77,
                statTribs     = 3,
                statDeaths    = 7,
                upgradeLevels = new System.Collections.Generic.List<int> { 2, 1, 3, 1 },
                learnedLessons = new System.Collections.Generic.List<string> { "slash", "jump" },
            };
            core.LoadSave(save);
            // StartRun increments StatRuns to 10, and sets RunProgress>0 via kills below.
            core.StartRun();
            return core;
        }

        [Test]
        public void ResetCultivation_ZeroesRealmAndStones()
        {
            var core = BuildAdvancedCore();
            core.ResetCultivation();

            Assert.AreEqual(0, core.Realm,        "Realm should be 0 after reset");
            Assert.AreEqual(0, core.TotalStones,  "TotalStones should be 0 after reset");
            Assert.AreEqual(0, core.SpendableStones, "SpendableStones should be 0 after reset");
            Assert.AreEqual(0, core.BestLi,       "BestLi should be 0 after reset");
            Assert.AreEqual(0, core.RunProgress,  "RunProgress should be 0 after reset");
        }

        [Test]
        public void ResetCultivation_ZeroesAllUpgradeLevels()
        {
            var core = BuildAdvancedCore();
            core.ResetCultivation();

            for (int i = 0; i < core.Upgrades.Count; i++)
                Assert.AreEqual(0, core.UpgradeLevel(i),
                    "UpgradeLevel[" + i + "] should be 0 after reset");
        }

        [Test]
        public void ResetCultivation_PreservesLifetimeStats()
        {
            var core = BuildAdvancedCore();
            core.ResetCultivation();

            Assert.AreEqual(10, core.StatRuns,   "StatRuns must survive reset");
            Assert.AreEqual(77, core.StatFoes,   "StatFoes must survive reset");
            Assert.AreEqual(3,  core.StatTribs,  "StatTribs must survive reset");
            Assert.AreEqual(7,  core.StatDeaths, "StatDeaths must survive reset");
        }

        [Test]
        public void ResetCultivation_PreservesLearnedLessons()
        {
            var core = BuildAdvancedCore();
            core.ResetCultivation();

            Assert.IsTrue(core.Tutorial.IsLearned("slash"), "slash lesson must survive reset");
            Assert.IsTrue(core.Tutorial.IsLearned("jump"),  "jump lesson must survive reset");
        }

        [Test]
        public void ResetCultivation_SaveRoundTripPersistsZeroes()
        {
            var core = BuildAdvancedCore();
            core.ResetCultivation();

            // Simulate the MonoBehaviour: ToSave → LoadSave
            var saved = core.ToSave();
            var fresh = new GameCore(ResetBalance());
            fresh.LoadSave(saved);

            Assert.AreEqual(0, fresh.Realm,       "Saved realm should be 0");
            Assert.AreEqual(0, fresh.TotalStones, "Saved totalStones should be 0");
            Assert.AreEqual(0, fresh.BestLi,      "Saved bestLi should be 0");
            Assert.AreEqual(0, fresh.RunProgress, "RunProgress not persisted — should be 0");
            for (int i = 0; i < fresh.Upgrades.Count; i++)
                Assert.AreEqual(0, fresh.UpgradeLevel(i),
                    "Saved upgradeLevel[" + i + "] should be 0");
            // Lifetime stats survive the round-trip too.
            Assert.AreEqual(10, fresh.StatRuns,  "StatRuns must round-trip through save");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Slice 16 — Daily reward + achievements (issue #13)
    // RED : ClaimDaily / DailyAvailable / CheckAchievements / IsAchUnlocked missing.
    // GREEN: port of Godot game.gd daily + achievement logic.
    // ─────────────────────────────────────────────────────────────────────────
    [TestFixture]
    public class DailyAndAchievementTests
    {
        static BalanceData B() => new BalanceData
        {
            realm_span        = new[] { 999999, 999999, 999999, 999999, 999999, 999999 },
            qi_max            = 999f,
            qi_per_kill       = 0f,
            net_push_per_kill = 0f,
            net_close_rate    = 0f,
            net_burst_relief  = 0f,
        };

        // ── Daily availability ────────────────────────────────────────────────

        [Test]
        public void DailyAvailable_FreshCore_IsTrue()
        {
            var core = new GameCore(B());
            // _dailyLastDay starts at -1, so any today > -1 is available
            Assert.IsTrue(core.DailyAvailable(0), "daily should be available on fresh core");
        }

        [Test]
        public void DailyAvailable_SameDay_IsFalse()
        {
            var core = new GameCore(B());
            core.ClaimDaily(100);
            Assert.IsFalse(core.DailyAvailable(100), "daily should be unavailable same day");
        }

        [Test]
        public void DailyAvailable_NextDay_IsTrue()
        {
            var core = new GameCore(B());
            core.ClaimDaily(100);
            Assert.IsTrue(core.DailyAvailable(101), "daily should be available the next day");
        }

        // ── ClaimDaily reward and streak ──────────────────────────────────────

        [Test]
        public void ClaimDaily_FirstClaim_Returns80AndStreak1()
        {
            var core = new GameCore(B());
            int before = core.TotalStones;
            int reward = core.ClaimDaily(100);
            Assert.AreEqual(80, reward, "first claim should return DAILY_BASE = 80");
            Assert.AreEqual(1,  core.DailyStreak, "streak should be 1 after first claim");
            Assert.AreEqual(before + 80, core.TotalStones, "TotalStones should increase by 80");
        }

        [Test]
        public void ClaimDaily_ConsecutiveDays_GrowsStreak()
        {
            var core = new GameCore(B());
            core.ClaimDaily(100);  // streak=1, reward=80
            int before = core.TotalStones;
            int reward = core.ClaimDaily(101);  // streak=2, reward=160
            Assert.AreEqual(2, core.DailyStreak, "streak should be 2 on consecutive day");
            Assert.AreEqual(160, reward, "reward should be DAILY_BASE*2 = 160");
            Assert.AreEqual(before + 160, core.TotalStones, "TotalStones should increase by 160");
        }

        [Test]
        public void ClaimDaily_GapBreaksStreak_ResetsTo1()
        {
            var core = new GameCore(B());
            core.ClaimDaily(100);  // streak=1
            core.ClaimDaily(101);  // streak=2
            // gap: day 105 — streak should reset to 1
            int reward = core.ClaimDaily(105);
            Assert.AreEqual(1, core.DailyStreak, "gap should reset streak to 1");
            Assert.AreEqual(80, reward, "reward should be DAILY_BASE*1 = 80 after reset");
        }

        [Test]
        public void ClaimDaily_StreakCapsRewardAtX7()
        {
            var core = new GameCore(B());
            // Claim 7 consecutive days to reach streak=7
            for (int i = 0; i < 7; i++)
                core.ClaimDaily(200 + i);
            Assert.AreEqual(7, core.DailyStreak, "streak should be 7 after 7 consecutive days");
            // Claim day 8 — streak=8 but reward is capped at DAILY_BASE*7
            int reward = core.ClaimDaily(207);
            Assert.AreEqual(8, core.DailyStreak, "streak continues past 7");
            Assert.AreEqual(80 * 7, reward, "reward must cap at DAILY_BASE * 7 = 560");
        }

        [Test]
        public void ClaimDaily_WhenUnavailable_Returns0AndDoesNotChangeState()
        {
            var core = new GameCore(B());
            core.ClaimDaily(100);
            int stonesBefore = core.TotalStones;
            int streakBefore = core.DailyStreak;
            int reward = core.ClaimDaily(100); // same day → unavailable
            Assert.AreEqual(0, reward, "second claim same day should return 0");
            Assert.AreEqual(stonesBefore, core.TotalStones, "TotalStones must not change");
            Assert.AreEqual(streakBefore, core.DailyStreak, "streak must not change");
        }

        [Test]
        public void ClaimDaily_FiresDailyClaimedEvent()
        {
            var core = new GameCore(B());
            int evStreak = -1, evReward = -1;
            core.DailyClaimed += (s, r) => { evStreak = s; evReward = r; };
            core.ClaimDaily(100);
            Assert.AreEqual(1,  evStreak, "DailyClaimed should carry streak=1");
            Assert.AreEqual(80, evReward, "DailyClaimed should carry reward=80");
        }

        // ── Achievement unlock ────────────────────────────────────────────────

        [Test]
        public void CheckAchievements_UnlocksFoundationLaid_WhenRealm1()
        {
            var core = new GameCore(B());
            core.LoadSave(new SaveData { realm = 1 });
            int stonesBefore = core.TotalStones;
            string unlockedId = null;
            core.AchievementUnlocked += id => unlockedId = id;
            core.CheckAchievements();
            Assert.IsTrue(core.IsAchUnlocked("r1"), "r1 should be unlocked at realm 1");
            Assert.AreEqual("r1", unlockedId, "AchievementUnlocked should fire with id 'r1'");
            Assert.AreEqual(stonesBefore + 150, core.TotalStones,
                "TotalStones should increase by ACH_REWARD = 150");
        }

        [Test]
        public void CheckAchievements_DoesNotReFire_WhenAlreadyUnlocked()
        {
            var core = new GameCore(B());
            core.LoadSave(new SaveData { realm = 1 });
            core.CheckAchievements(); // unlock r1
            Assert.IsTrue(core.IsAchUnlocked("r1"));

            int stonesAfterFirst = core.TotalStones;
            int eventCount = 0;
            core.AchievementUnlocked += _ => eventCount++;
            core.CheckAchievements(); // should not re-unlock

            Assert.AreEqual(0, eventCount, "AchievementUnlocked should not re-fire");
            Assert.AreEqual(stonesAfterFirst, core.TotalStones, "TotalStones must not change on repeat check");
        }

        [Test]
        public void CheckAchievements_Slay100_UnlocksOnFoes100()
        {
            var core = new GameCore(B());
            core.LoadSave(new SaveData { statFoes = 100 });
            core.CheckAchievements();
            Assert.IsTrue(core.IsAchUnlocked("slay100"), "slay100 should unlock at statFoes=100");
        }

        [Test]
        public void CheckAchievements_MultipleSameCheck_AwardsCorrectStones()
        {
            // Load realm=1 and statFoes=100 → both r1 and slay100 unlock in one CheckAchievements call
            var core = new GameCore(B());
            core.LoadSave(new SaveData { realm = 1, statFoes = 100 });
            int stonesBefore = core.TotalStones;
            core.CheckAchievements();
            Assert.IsTrue(core.IsAchUnlocked("r1"),      "r1 should unlock");
            Assert.IsTrue(core.IsAchUnlocked("slay100"), "slay100 should unlock");
            Assert.AreEqual(stonesBefore + 150 * 2, core.TotalStones,
                "two achievements should award 2 × ACH_REWARD = 300");
        }

        // ── Save / load round-trip ────────────────────────────────────────────

        [Test]
        public void SaveLoad_RoundTrips_DailyLastDay_DailyStreak_AchUnlocked()
        {
            var coreA = new GameCore(B());
            coreA.ClaimDaily(300);  // streak=1, lastDay=300
            coreA.ClaimDaily(301);  // streak=2, lastDay=301
            coreA.LoadSave(new SaveData { realm = 1, statFoes = 100 });
            // Need to re-apply daily after LoadSave (LoadSave resets daily to saved defaults),
            // so set up a core that has daily and ach state via LoadSave directly:
            var saveA = coreA.ToSave();
            // Manually patch the save to simulate having claimed day 301 with streak 2 + r1 unlocked
            saveA.dailyLastDay = 301;
            saveA.dailyStreak  = 2;
            saveA.achUnlocked  = new System.Collections.Generic.List<string> { "r1", "slay100" };

            var coreB = new GameCore(B());
            coreB.LoadSave(saveA);

            Assert.AreEqual(301, coreB.ToSave().dailyLastDay, "dailyLastDay must round-trip");
            Assert.AreEqual(2,   coreB.DailyStreak,           "dailyStreak must round-trip");
            Assert.IsTrue(coreB.IsAchUnlocked("r1"),      "r1 must round-trip");
            Assert.IsTrue(coreB.IsAchUnlocked("slay100"), "slay100 must round-trip");
            Assert.IsFalse(coreB.IsAchUnlocked("r2"),     "r2 must NOT be marked unlocked");
            Assert.IsFalse(coreB.DailyAvailable(301),     "daily unavailable after loading day=301");
            Assert.IsTrue(coreB.DailyAvailable(302),      "daily available on next day");
        }

        [Test]
        public void LoadSave_NullAchUnlocked_DoesNotThrow()
        {
            var core = new GameCore(B());
            var save = new SaveData { achUnlocked = null };
            Assert.DoesNotThrow(() => core.LoadSave(save), "null achUnlocked must not throw");
            Assert.IsFalse(core.IsAchUnlocked("r1"), "no achievements should be unlocked");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HUD Readout — Issue #9 getters: TribTimeLeft + PowerupTimeLeft
    // These are the pure-core getters surfaced so HudOverlay can read them
    // without reaching into private fields.
    // ─────────────────────────────────────────────────────────────────────────
    [TestFixture]
    public class HudReadoutTests
    {
        // Tiny realm_span so we can trigger tribulation in a unit test with few kills.
        static BalanceData TribBalance() => new BalanceData
        {
            realm_span        = new[] { 5, 10, 20, 40, 80, 999999 },
            qi_max            = 999f,
            qi_per_kill       = 1f,
            net_push_per_kill = 0f,
            net_close_rate    = 0f,    // freeze net so it can't kill before trib ends
            net_burst_relief  = 0f,
        };

        static BalanceData NoKillBalance() => new BalanceData
        {
            realm_span        = new[] { 999999, 999999, 999999, 999999, 999999, 999999 },
            qi_max            = 999f,
            qi_per_kill       = 0f,
            net_push_per_kill = 0f,
            net_close_rate    = 0f,
            net_burst_relief  = 0f,
        };

        // ── TribTimeLeft ─────────────────────────────────────────────────────

        [Test]
        public void TribTimeLeft_IsZero_WhenNotInTribulation()
        {
            var core = new GameCore(TribBalance());
            core.StartRun();
            Assert.IsFalse(core.InTribulation, "sanity: not in tribulation on fresh run");
            Assert.AreEqual(0f, core.TribTimeLeft, 0.001f,
                "TribTimeLeft must be 0 when InTribulation is false");
        }

        [Test]
        public void TribTimeLeft_EqualsApproxTribDuration_WhenJustEntered()
        {
            // Trigger tribulation by reaching realm_span[0]=5 kills.
            var core = new GameCore(TribBalance());
            core.StartRun();
            for (int i = 0; i < 5; i++)
                core.OnEnemyKilled(1);
            Assert.IsTrue(core.InTribulation, "should have entered tribulation");
            // TribTimeLeft should be close to TRIB_DURATION=12 immediately after entry.
            Assert.AreEqual(12f, core.TribTimeLeft, 0.5f,
                "TribTimeLeft should be ~12s (TRIB_DURATION) just after tribulation starts");
        }

        [Test]
        public void TribTimeLeft_DecreasesAfterTick()
        {
            var core = new GameCore(TribBalance());
            core.StartRun();
            for (int i = 0; i < 5; i++)
                core.OnEnemyKilled(1);
            Assert.IsTrue(core.InTribulation);
            float before = core.TribTimeLeft;
            core.Tick(2f);
            float after = core.TribTimeLeft;
            Assert.Less(after, before, "TribTimeLeft must decrease after Tick");
            Assert.AreEqual(before - 2f, after, 0.05f, "TribTimeLeft should decrease by ~delta");
        }

        [Test]
        public void TribTimeLeft_IsZero_AfterTribulationExpires()
        {
            var core = new GameCore(TribBalance());
            core.StartRun();
            for (int i = 0; i < 5; i++)
                core.OnEnemyKilled(1);
            Assert.IsTrue(core.InTribulation);
            core.Tick(12.1f); // tick past TRIB_DURATION
            Assert.IsFalse(core.InTribulation, "tribulation should have ended");
            Assert.AreEqual(0f, core.TribTimeLeft, 0.001f,
                "TribTimeLeft must be 0 once tribulation ends");
        }

        [Test]
        public void TribTimeLeft_NeverNegative()
        {
            // Even if we call the getter while InTribulation with _tribT near 0,
            // TribTimeLeft must clamp to 0, not go negative.
            var core = new GameCore(TribBalance());
            core.StartRun();
            for (int i = 0; i < 5; i++)
                core.OnEnemyKilled(1);
            // Tick to just before expiry — timer should be tiny but >= 0.
            core.Tick(11.99f);
            Assert.GreaterOrEqual(core.TribTimeLeft, 0f, "TribTimeLeft must never be negative");
        }

        // ── PowerupTimeLeft ──────────────────────────────────────────────────

        [Test]
        public void PowerupTimeLeft_IsZero_BeforeActivation()
        {
            var core = new GameCore(NoKillBalance());
            core.StartRun();
            Assert.AreEqual(0f, core.PowerupTimeLeft("magnet"), 0.001f,
                "magnet time must be 0 before activation");
        }

        [Test]
        public void PowerupTimeLeft_EqualsDuration_AfterActivation_Magnet()
        {
            // magnet dur = 8s
            var core = new GameCore(NoKillBalance());
            core.StartRun();
            core.ActivatePowerup("magnet");
            Assert.AreEqual(8f, core.PowerupTimeLeft("magnet"), 0.05f,
                "magnet time should be ~8s immediately after activation");
        }

        [Test]
        public void PowerupTimeLeft_Decreases_AfterTick_Magnet()
        {
            var core = new GameCore(NoKillBalance());
            core.StartRun();
            core.ActivatePowerup("magnet");
            core.Tick(1f);
            Assert.AreEqual(7f, core.PowerupTimeLeft("magnet"), 0.05f,
                "magnet time should be ~7s after Tick(1)");
        }

        [Test]
        public void PowerupTimeLeft_IsZero_AfterExpiry_Magnet()
        {
            var core = new GameCore(NoKillBalance());
            core.StartRun();
            core.ActivatePowerup("magnet");
            core.Tick(8.1f); // past 8s duration
            Assert.AreEqual(0f, core.PowerupTimeLeft("magnet"), 0.001f,
                "magnet time must be 0 after expiry");
        }

        [Test]
        public void PowerupTimeLeft_EqualsDuration_AfterActivation_Dash()
        {
            // dash dur = 3s
            var core = new GameCore(NoKillBalance());
            core.StartRun();
            core.ActivatePowerup("dash");
            Assert.AreEqual(3f, core.PowerupTimeLeft("dash"), 0.05f,
                "dash time should be ~3s immediately after activation");
        }

        [Test]
        public void PowerupTimeLeft_IsZero_AfterExpiry_Dash()
        {
            var core = new GameCore(NoKillBalance());
            core.StartRun();
            core.ActivatePowerup("dash");
            core.Tick(3.1f);
            Assert.AreEqual(0f, core.PowerupTimeLeft("dash"), 0.001f,
                "dash time must be 0 after its 3s duration");
        }

        [Test]
        public void PowerupTimeLeft_IsZero_ForUnknownId()
        {
            var core = new GameCore(NoKillBalance());
            core.StartRun();
            Assert.AreEqual(0f, core.PowerupTimeLeft("nonexistent"), 0.001f,
                "unknown powerup id must return 0");
        }
    }
}
