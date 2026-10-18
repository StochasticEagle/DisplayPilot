// SPDX-License-Identifier: Unlicense
using DisplayPilot.Display.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DisplayPilot.Display.Tests.Models;

[TestClass]
public sealed class DisplayLevelScaleTests
{
    [TestMethod]
    public void MapsNonstandardHardwareRange()
    {
        Assert.AreEqual(50, DisplayLevelScale.ToPercent(25, 50));
        Assert.AreEqual(25, DisplayLevelScale.FromPercent(50, 50));
    }

    [DataTestMethod]
    [DataRow(-10, 0)]
    [DataRow(110, 50)]
    public void PercentagesAreClampedBeforeScaling(int percent, int expected)
        => Assert.AreEqual(expected, DisplayLevelScale.FromPercent(percent, 50));

    [TestMethod]
    public void NonzeroMinimumAndDegenerateRanges()
    {
        Assert.AreEqual(50, DisplayLevelScale.ToPercent(35, 60, 10));
        Assert.AreEqual(35, DisplayLevelScale.FromPercent(50, 60, 10));
        Assert.AreEqual(0, DisplayLevelScale.ToPercent(10, 10, 10));
        Assert.AreEqual(10, DisplayLevelScale.FromPercent(50, 10, 10));
        Assert.AreEqual(0, DisplayLevelScale.ToPercent(150, 100));
    }
}
