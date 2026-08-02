using DisplayPilot.Core.Theme;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DisplayPilot.Core.Tests.Theme;

[TestClass]
public sealed class ScheduledBrightnessTests
{
    [DataTestMethod]
    [DataRow(100, 50)]
    [DataRow(67, 33)]
    [DataRow(11, 5)]
    [DataRow(10, 0)]
    [DataRow(0, 0)]
    public void ReductionUsesApprovedThresholdAndIntegerDivision(int current, int expected)
    {
        Assert.AreEqual(expected, ScheduledBrightness.CalculateReducedValue(current));
    }

    [DataTestMethod]
    [DataRow(-1)]
    [DataRow(101)]
    public void ReductionRejectsInvalidPercentages(int current)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            ScheduledBrightness.CalculateReducedValue(current));
    }
}
