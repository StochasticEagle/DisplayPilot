using DisplayPilot.Display.Ddc;
using DisplayPilot.Display.Discovery;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DisplayPilot.Display.Tests.Ddc;

[TestClass]
public sealed class DdcBrightnessProbeServiceTests
{
    [TestMethod]
    public void ProbeBrightnessMatchesGdiNamesCaseInsensitively()
    {
        var api = new FakeDdcProbeApi(
        [
            new("\\\\.\\display1", "Office monitor", DdcBrightnessProbeStatus.ReadSucceeded, 40, 100, 0),
        ]);
        var service = new DdcBrightnessProbeService(api);
        MonitorDisplayInfo[] displays =
        [
            new("\\\\?\\DISPLAY#DEL1234#ONE", "\\\\.\\DISPLAY1", "Dell", 1),
        ];

        var results = service.ProbeBrightness(displays);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual(DdcBrightnessProbeStatus.ReadSucceeded, results[0].PhysicalMonitors[0].Status);
        Assert.AreEqual((uint)40, results[0].PhysicalMonitors[0].CurrentValue);
        Assert.AreEqual((uint)100, results[0].PhysicalMonitors[0].MaximumValue);
    }

    [TestMethod]
    public void ProbeBrightnessPreservesMultiplePhysicalMonitorsForOneDisplayPath()
    {
        var api = new FakeDdcProbeApi(
        [
            new("\\\\.\\DISPLAY1", "Left tile", DdcBrightnessProbeStatus.ReadSucceeded, 10, 100, 0),
            new("\\\\.\\DISPLAY1", "Right tile", DdcBrightnessProbeStatus.ReadFailed, 0, 0, 31),
        ]);
        var service = new DdcBrightnessProbeService(api);
        MonitorDisplayInfo[] displays =
        [
            new("path", "\\\\.\\DISPLAY1", "Tiled display", 1),
        ];

        var results = service.ProbeBrightness(displays);

        Assert.AreEqual(2, results[0].PhysicalMonitors.Count);
        Assert.AreEqual("Left tile", results[0].PhysicalMonitors[0].PhysicalMonitorDescription);
        Assert.AreEqual("Right tile", results[0].PhysicalMonitors[1].PhysicalMonitorDescription);
    }

    [TestMethod]
    public void ProbeBrightnessReportsNoPhysicalMonitorWhenWindowsNamesDoNotMatch()
    {
        var service = new DdcBrightnessProbeService(new FakeDdcProbeApi([]));
        MonitorDisplayInfo[] displays =
        [
            new("path", "\\\\.\\DISPLAY7", "Virtual display", 7),
        ];

        var results = service.ProbeBrightness(displays);

        Assert.AreEqual(DdcBrightnessProbeStatus.NoPhysicalMonitor, results[0].PhysicalMonitors[0].Status);
    }

    private sealed class FakeDdcProbeApi(IReadOnlyList<DdcBrightnessProbeResult> results) : IDdcProbeApi
    {
        public IReadOnlyList<DdcBrightnessProbeResult> ProbeBrightness() => results;
    }
}
