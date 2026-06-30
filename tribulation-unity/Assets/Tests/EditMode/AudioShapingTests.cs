using NUnit.Framework;
using Tribulation.Core;

[TestFixture]
public class AudioShapingTests
{
    // ── TargetPitch ────────────────────────────────────────────────────────

    [Test]
    public void TargetPitch_AtFracZero_Returns0_95()
    {
        float result = AudioShaping.TargetPitch(0f, inTribulation: false);
        Assert.AreEqual(0.95f, result, 0.0001f);
    }

    [Test]
    public void TargetPitch_AtFracOne_Returns1_12()
    {
        float result = AudioShaping.TargetPitch(1f, inTribulation: false);
        Assert.AreEqual(1.12f, result, 0.0001f);
    }

    [Test]
    public void TargetPitch_Tribulation_Adds0_08()
    {
        float withoutTrib = AudioShaping.TargetPitch(0.5f, inTribulation: false);
        float withTrib    = AudioShaping.TargetPitch(0.5f, inTribulation: true);
        Assert.AreEqual(withoutTrib + 0.08f, withTrib, 0.0001f);
    }

    // ── TargetVolumeDb ─────────────────────────────────────────────────────

    [Test]
    public void TargetVolumeDb_Dead_ReturnsMinus24()
    {
        float result = AudioShaping.TargetVolumeDb(isDead: true, inTribulation: false);
        Assert.AreEqual(-24f, result, 0.0001f);
    }

    [Test]
    public void TargetVolumeDb_DeadWinsOverTribulation()
    {
        float result = AudioShaping.TargetVolumeDb(isDead: true, inTribulation: true);
        Assert.AreEqual(-24f, result, 0.0001f);
    }

    [Test]
    public void TargetVolumeDb_Tribulation_ReturnsMinus3()
    {
        float result = AudioShaping.TargetVolumeDb(isDead: false, inTribulation: true);
        Assert.AreEqual(-3f, result, 0.0001f);
    }

    [Test]
    public void TargetVolumeDb_Normal_ReturnsMinus9()
    {
        float result = AudioShaping.TargetVolumeDb(isDead: false, inTribulation: false);
        Assert.AreEqual(-9f, result, 0.0001f);
    }

    // ── DbToLinear ─────────────────────────────────────────────────────────

    [Test]
    public void DbToLinear_Zero_ReturnsOne()
    {
        float result = AudioShaping.DbToLinear(0f);
        Assert.AreEqual(1f, result, 0.0001f);
    }

    [Test]
    public void DbToLinear_Minus6_IsApprox0_501()
    {
        float result = AudioShaping.DbToLinear(-6f);
        Assert.AreEqual(0.501f, result, 0.001f);
    }
}
