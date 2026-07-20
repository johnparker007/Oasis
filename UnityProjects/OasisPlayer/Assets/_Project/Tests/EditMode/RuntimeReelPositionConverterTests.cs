using NUnit.Framework;
using OasisPlayer.RuntimeBuild;
using UnityEngine;

public sealed class RuntimeReelPositionConverterTests
{
    [TestCase(0f, 180f)]
    [TestCase(24f, 90f)]
    [TestCase(48f, 0f)]
    [TestCase(72f, 270f)]
    [TestCase(95f, 183.75f)]
    [TestCase(96f, 180f)]
    [TestCase(-1f, 183.75f)]
    [TestCase(120f, 90f)]
    public void ToLocalRotation_MapsCanonicalPositions(float position, float expectedDegrees)
    {
        AssertAngle(position, false, 0f, 180f, -1f, expectedDegrees);
    }

    [Test]
    public void ToLocalRotation_AppliesDirectionBaselineReversalAndBandOffset()
    {
        AssertAngle(24f, false, 0f, 10f, 1f, 100f);
        AssertAngle(24f, true, 0f, 180f, -1f, 270f);
        AssertAngle(0f, false, 0.25f, 180f, -1f, 90f);
    }

    private static void AssertAngle(float position, bool reversed, float offset, float baseline, float direction, float expected)
    {
        var q = RuntimeReelPositionConverter.ToLocalRotation(position, reversed, offset, baseline, direction);
        var actual = q.eulerAngles.x;
        Assert.AreEqual(expected, actual, 0.01f);
    }
}
