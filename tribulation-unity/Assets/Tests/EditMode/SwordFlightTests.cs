// TDD tests for SwordFlight (#7 — Sword-flight aerial mode 御剑).
// Pure-C# — no UnityEngine. Runs in coretest harness.

using NUnit.Framework;
using Tribulation.Core;

[TestFixture]
public class SwordFlightTests
{
    // ── Test 1: cooldown counts down only while grounded+canFly; enters flight after FIRST ──

    [Test]
    public void Cooldown_OnlyTicksWhileGroundedAndCanFly()
    {
        var sf = new SwordFlight();
        // Tick airborne — cooldown must NOT decrease (flight_cd stays at FIRST=7)
        sf.Tick(3f, grounded: false, canFly: true);
        Assert.IsFalse(sf.IsFlying, "Must not fly when not grounded");

        // Tick grounded but canFly=false — still no change
        sf.Tick(3f, grounded: true, canFly: false);
        Assert.IsFalse(sf.IsFlying, "Must not fly when canFly=false");

        // Still need 7s of grounded+canFly ticks to trigger
        // We've only ticked in non-counting conditions, so nothing used yet
        sf.Tick(6.9f, grounded: true, canFly: true);
        Assert.IsFalse(sf.IsFlying, "Must not fly before FIRST=7s elapses");

        sf.Tick(0.11f, grounded: true, canFly: true);
        Assert.IsTrue(sf.IsFlying, "Must enter flight once FIRST elapses");
    }

    [Test]
    public void DoesNotEnterFlight_WhenCanFlyFalse_EvenPastFirst()
    {
        var sf = new SwordFlight();
        // Tick way past FIRST but with canFly=false always
        sf.Tick(20f, grounded: true, canFly: false);
        Assert.IsFalse(sf.IsFlying, "Must not enter flight when ability locked");
    }

    // ── Test 2: ClimbVelocity band rules ──────────────────────────────────────

    [Test]
    public void ClimbVelocity_BelowMin_AlwaysLifts()
    {
        var sf = new SwordFlight();
        float vy = sf.ClimbVelocity(currentY: 1.0f, climbHeld: false, diveHeld: false);
        Assert.AreEqual(SwordFlight.CLIMB, vy, 0.001f, "Below MIN_Y must always lift at +CLIMB");
    }

    [Test]
    public void ClimbVelocity_ClimbHeld_BelowMax_Climbs()
    {
        var sf = new SwordFlight();
        float vy = sf.ClimbVelocity(currentY: 3.0f, climbHeld: true, diveHeld: false);
        Assert.AreEqual(SwordFlight.CLIMB, vy, 0.001f, "climbHeld & y<MAX → +CLIMB");
    }

    [Test]
    public void ClimbVelocity_ClimbHeld_AtOrAboveMax_Zero()
    {
        var sf = new SwordFlight();
        float vy = sf.ClimbVelocity(currentY: SwordFlight.MAX_Y, climbHeld: true, diveHeld: false);
        Assert.AreEqual(0f, vy, 0.001f, "climbHeld & y>=MAX → 0");
    }

    [Test]
    public void ClimbVelocity_DiveHeld_AboveMin_Dives()
    {
        var sf = new SwordFlight();
        float vy = sf.ClimbVelocity(currentY: 4.0f, climbHeld: false, diveHeld: true);
        Assert.AreEqual(-SwordFlight.CLIMB, vy, 0.001f, "diveHeld & y>MIN → -CLIMB");
    }

    [Test]
    public void ClimbVelocity_DiveHeld_AtOrBelowMin_Zero()
    {
        var sf = new SwordFlight();
        float vy = sf.ClimbVelocity(currentY: SwordFlight.MIN_Y, climbHeld: false, diveHeld: true);
        Assert.AreEqual(0f, vy, 0.001f, "diveHeld & y<=MIN → 0");
    }

    [Test]
    public void ClimbVelocity_NeitherHeld_InBand_Zero()
    {
        var sf = new SwordFlight();
        float vy = sf.ClimbVelocity(currentY: 4.0f, climbHeld: false, diveHeld: false);
        Assert.AreEqual(0f, vy, 0.001f, "neither held, in band → 0");
    }

    // ── Test 3: flight exits after DURATION; cooldown resets; no re-entry until COOLDOWN ──

    [Test]
    public void Flight_ExitsAfterDuration_ThenCooldownRequired()
    {
        var sf = new SwordFlight();

        // Enter flight: tick past FIRST with grounded+canFly
        sf.Tick(SwordFlight.FIRST + 0.1f, grounded: true, canFly: true);
        Assert.IsTrue(sf.IsFlying, "Should have entered flight");

        // Tick through flight duration (grounded=false while flying, canFly=true)
        sf.Tick(SwordFlight.DURATION + 0.1f, grounded: false, canFly: true);
        Assert.IsFalse(sf.IsFlying, "Must exit flight after DURATION");

        // After exiting, cooldown = COOLDOWN=16. Tick almost through it — still no flight.
        sf.Tick(SwordFlight.COOLDOWN - 0.1f, grounded: true, canFly: true);
        Assert.IsFalse(sf.IsFlying, "Must not re-enter before COOLDOWN elapses");

        // Finish the cooldown — should enter again
        sf.Tick(0.2f, grounded: true, canFly: true);
        Assert.IsTrue(sf.IsFlying, "Must re-enter flight after COOLDOWN elapses");
    }
}
