// SPDX-License-Identifier: Unlicense
// DisplayPilot monitor matching regression tests
using DisplayPilot.Display.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DisplayPilot.Display.Tests.Models;

[TestClass]
public class DisplayPathKeyTests
{
    [TestMethod]
    public void DevicePathDiscardsInterfaceGuid()
    {
        Assert.AreEqual(@"\\?\DISPLAY#ABC1234#unit", DisplayPathKey.FromDevicePath(@"\\?\DISPLAY#ABC1234#unit#{guid}"));
        Assert.AreEqual(string.Empty, DisplayPathKey.FromDevicePath(null));
    }

    [TestMethod]
    public void WmiInstanceMatchesDisplayPath()
    {
        var display = DisplayPathKey.FromDevicePath(@"\\?\DISPLAY#ABC1234#unit#{guid}");
        var wmi = DisplayPathKey.FromWmiInstance(@"DISPLAY\ABC1234\unit_12");
        Assert.AreEqual(display, wmi);
    }

    [TestMethod]
    public void DistinctPhysicalPanelsRemainDistinct()
    {
        Assert.AreNotEqual(DisplayPathKey.FromWmiInstance(@"DISPLAY\ABC1234\UID1_0"),
                              DisplayPathKey.FromWmiInstance(@"DISPLAY\ABC1234\UID2_0"));
    }

    [TestMethod]
    public void InvalidInstancesProduceNoKey()
    {
        Assert.AreEqual(string.Empty, DisplayPathKey.FromWmiInstance(null));
        Assert.AreEqual(string.Empty, DisplayPathKey.FromWmiInstance(@"DISPLAY\ABC1234"));
        Assert.AreEqual(string.Empty, DisplayPathKey.FromWmiInstance(@"DISPLAY\\UID_0"));
    }
}
