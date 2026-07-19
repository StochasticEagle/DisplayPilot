using DisplayPilot.Display.Discovery;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DisplayPilot.Display.Tests.Discovery;

[TestClass]
public sealed class DisplayConfigMonitorDiscoveryTests
{
    [TestMethod]
    public void DiscoveryFiltersIncompletePathsAndDeduplicatesDevicePaths()
    {
        IDisplayConfigApi api = new FakeDisplayConfigApi(
        [
            new("  \\\\?\\DISPLAY#DEL1234#FIRST  ", "  \\\\.\\DISPLAY2  ", "  Office monitor  ", 2),
            new("\\\\?\\display#del1234#first", "\\\\.\\DISPLAY9", "Duplicate", 9),
            new(null, "\\\\.\\DISPLAY3", "Missing path", 3),
            new("\\\\?\\DISPLAY#ACR0001#SECOND", null, "Missing GDI name", 4),
        ]);
        var discovery = new DisplayConfigMonitorDiscovery(api);

        var monitors = discovery.DiscoverActiveMonitors();

        Assert.AreEqual(1, monitors.Count);
        Assert.AreEqual("\\\\?\\DISPLAY#DEL1234#FIRST", monitors[0].DevicePath);
        Assert.AreEqual("\\\\.\\DISPLAY2", monitors[0].GdiDeviceName);
        Assert.AreEqual("Office monitor", monitors[0].FriendlyName);
        Assert.AreEqual(2, monitors[0].MonitorNumber);
    }

    [TestMethod]
    public void DiscoverySortsPathsAndSuppliesMissingFriendlyNames()
    {
        IDisplayConfigApi api = new FakeDisplayConfigApi(
        [
            new("\\\\?\\DISPLAY#TWO", "\\\\.\\DISPLAY2", "", 2),
            new("\\\\?\\DISPLAY#ONE", "\\\\.\\DISPLAY1", "Primary", 1),
        ]);
        var discovery = new DisplayConfigMonitorDiscovery(api);

        var monitors = discovery.DiscoverActiveMonitors();

        Assert.AreEqual(2, monitors.Count);
        Assert.AreEqual("Primary", monitors[0].FriendlyName);
        Assert.AreEqual("Display 2", monitors[1].FriendlyName);
    }

    private sealed class FakeDisplayConfigApi(IReadOnlyList<DisplayConfigPathDescriptor> paths) : IDisplayConfigApi
    {
        public IReadOnlyList<DisplayConfigPathDescriptor> QueryActiveDisplayPaths() => paths;
    }
}
