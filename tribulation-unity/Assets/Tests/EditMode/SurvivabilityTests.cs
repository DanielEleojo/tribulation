// TDD tests for Survivability (#8 — Iron-Body shields + Blood-Sprint + Dread Form).
// Pure-C# — no UnityEngine. Runs in coretest harness.
//
// Tests:
//  1. ApplyRealmStats grants filled shield slot; raising max grants the diff; lowering clamps.
//  2. TryAbsorbHit consumes a shield, sets invuln, returns true; 0 shields+no invuln = false;
//     during invuln returns true without consuming.
//  3. Shield regen: below max, after Tick totalling 9s → Shields+1; not above max.
//  4. OnKills adds sprint capped at 8; Tick decays sprint at 4/s toward 0 (not below 0).

using NUnit.Framework;
using Tribulation.Core;

[TestFixture]
public class SurvivabilityTests
{
    // ── Test 1: ApplyRealmStats ───────────────────────────────────────────────

    [Test]
    public void ApplyRealmStats_GrantsFilledShieldSlot()
    {
        var s = new Survivability();
        s.ApplyRealmStats(shieldMax: 1, sprintPerKill: 0f);
        Assert.AreEqual(1, s.MaxShields, "MaxShields should be 1");
        Assert.AreEqual(1, s.Shields,    "Granted slot should be immediately filled");
    }

    [Test]
    public void ApplyRealmStats_RaisingMax_GrantsDiff()
    {
        var s = new Survivability();
        s.ApplyRealmStats(1, 0f);   // realm 3: 1 shield
        s.ApplyRealmStats(2, 0f);   // realm 5: 2 shields — diff of 1 more granted
        Assert.AreEqual(2, s.MaxShields);
        Assert.AreEqual(2, s.Shields, "Second slot should also be granted filled");
    }

    [Test]
    public void ApplyRealmStats_LoweringMax_ClampsShields()
    {
        var s = new Survivability();
        s.ApplyRealmStats(2, 0f);
        // Manually drop shields via absorb so Shields < MaxShields
        s.TryAbsorbHit(); // shields 1, invuln=1
        Assert.AreEqual(1, s.Shields);
        // Now apply a max of 1 — shields should clamp to max
        s.ApplyRealmStats(1, 0f);
        Assert.AreEqual(1, s.MaxShields);
        Assert.AreEqual(1, s.Shields, "Shields must not exceed new MaxShields");
    }

    // ── Test 2: TryAbsorbHit ─────────────────────────────────────────────────

    [Test]
    public void TryAbsorbHit_WithShield_ConsumesAndReturnsTrue()
    {
        var s = new Survivability();
        s.ApplyRealmStats(1, 0f);
        bool absorbed = s.TryAbsorbHit();
        Assert.IsTrue(absorbed, "Should absorb the hit");
        Assert.AreEqual(0, s.Shields, "Shield should be consumed");
        Assert.Greater(s.InvulnT, 0f, "Invuln window should be set");
    }

    [Test]
    public void TryAbsorbHit_NoShieldNoInvuln_ReturnsFalse()
    {
        var s = new Survivability();
        bool absorbed = s.TryAbsorbHit();
        Assert.IsFalse(absorbed, "No shields and no invuln — should not absorb");
    }

    [Test]
    public void TryAbsorbHit_DuringInvuln_ReturnsTrueWithoutConsuming()
    {
        var s = new Survivability();
        s.ApplyRealmStats(1, 0f);
        s.TryAbsorbHit();           // consume the shield, invuln active
        int shieldsBefore = s.Shields;
        bool absorbed = s.TryAbsorbHit(); // should absorb via invuln — NO extra consume
        Assert.IsTrue(absorbed, "Should absorb during invuln window");
        Assert.AreEqual(shieldsBefore, s.Shields, "Should not consume a shield during invuln");
    }

    // ── Test 3: Shield regen ─────────────────────────────────────────────────

    [Test]
    public void ShieldRegen_BelowMax_RegainsAfter9s()
    {
        var s = new Survivability();
        s.ApplyRealmStats(1, 0f);
        s.TryAbsorbHit();               // shield -> 0, regen timer = 9s
        // Skip past invuln first so that's not a factor
        s.Tick(Survivability.INVULN_ON_ABSORB + 0.01f);
        Assert.AreEqual(0, s.Shields, "Not enough time yet");

        // Tick remaining time to reach 9s total
        float remaining = Survivability.SHIELD_REGEN_TIME
                        - (Survivability.INVULN_ON_ABSORB + 0.01f)
                        + 0.01f; // tiny extra to cross threshold
        s.Tick(remaining);
        Assert.AreEqual(1, s.Shields, "Shield should have regenerated after 9s");
    }

    [Test]
    public void ShieldRegen_AtMax_DoesNotExceedMax()
    {
        var s = new Survivability();
        s.ApplyRealmStats(1, 0f);
        // Already at max — tick a long time
        s.Tick(20f);
        Assert.AreEqual(1, s.Shields, "Must not exceed MaxShields");
    }

    // ── Test 4: Blood-Sprint (OnKills + Tick decay) ───────────────────────────

    [Test]
    public void OnKills_AddsSprint_CappedAt8()
    {
        var s = new Survivability();
        s.ApplyRealmStats(0, sprintPerKill: 3.0f);
        s.OnKills(3); // 3*3 = 9 → capped at 8
        Assert.AreEqual(Survivability.SPRINT_CAP, s.SprintBoost, 0.001f,
            "SprintBoost must be capped at SPRINT_CAP");
    }

    [Test]
    public void OnKills_AccumulatesCorrectly()
    {
        var s = new Survivability();
        s.ApplyRealmStats(0, sprintPerKill: 1.5f);
        s.OnKills(2); // 1.5 * 2 = 3.0
        Assert.AreEqual(3.0f, s.SprintBoost, 0.001f, "Sprint should accumulate to 3.0");
    }

    [Test]
    public void Tick_DecaysSprint_At4PerSecond()
    {
        var s = new Survivability();
        s.ApplyRealmStats(0, sprintPerKill: 4.0f);
        s.OnKills(1); // SprintBoost = 4.0
        s.Tick(0.5f); // 4.0 - 4.0*0.5 = 2.0
        Assert.AreEqual(2.0f, s.SprintBoost, 0.001f, "Sprint should decay by 2.0 in 0.5s");
    }

    [Test]
    public void Tick_SprintDoesNotGoBelowZero()
    {
        var s = new Survivability();
        s.ApplyRealmStats(0, sprintPerKill: 1.0f);
        s.OnKills(1); // SprintBoost = 1.0
        s.Tick(10f);  // way more than needed to drain it
        Assert.AreEqual(0f, s.SprintBoost, 0.001f, "Sprint must not go below 0");
    }

    // ── Test 4b: GrantShield (Iron Aegis consumable, Batch 3a) ───────────────

    [Test]
    public void GrantShield_RaisesShieldsByOne()
    {
        var s = new Survivability(); // Shields=0, MaxShields=0
        s.GrantShield();
        Assert.AreEqual(1, s.Shields,    "GrantShield should raise Shields to 1");
        Assert.AreEqual(1, s.MaxShields, "MaxShields should stretch to match the aegis charge");
    }

    // ── Test 5: Reset ─────────────────────────────────────────────────────────

    [Test]
    public void Reset_RefillsShields_ClearsTransient()
    {
        var s = new Survivability();
        s.ApplyRealmStats(2, sprintPerKill: 2.0f);
        s.TryAbsorbHit(); // shields 1, invuln set, sprint 0
        s.OnKills(1);     // sprint 2
        s.Reset();
        Assert.AreEqual(2, s.MaxShields, "MaxShields should persist");
        Assert.AreEqual(2, s.Shields,    "Shields should be refilled on reset");
        Assert.AreEqual(0f, s.InvulnT,   0.001f, "InvulnT should be cleared");
        Assert.AreEqual(0f, s.SprintBoost, 0.001f, "SprintBoost should be cleared");
    }
}
