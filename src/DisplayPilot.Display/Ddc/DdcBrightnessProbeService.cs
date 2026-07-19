using DisplayPilot.Display.Discovery;
using DisplayPilot.Display.Interop;

namespace DisplayPilot.Display.Ddc;

/// <summary>
/// Maps physical-monitor probe results to stable DisplayConfig identities.
/// </summary>
public sealed class DdcBrightnessProbeService : IDdcBrightnessProbeService
{
    private readonly IDdcProbeApi _probeApi;

    public DdcBrightnessProbeService()
        : this(new WindowsDdcProbeApi())
    {
    }

    public DdcBrightnessProbeService(IDdcProbeApi probeApi)
    {
        ArgumentNullException.ThrowIfNull(probeApi);
        _probeApi = probeApi;
    }

    public IReadOnlyList<MonitorDdcProbeInfo> ProbeBrightness(
        IReadOnlyList<MonitorDisplayInfo> activeDisplays)
    {
        ArgumentNullException.ThrowIfNull(activeDisplays);

        var probesByDisplay = _probeApi
            .ProbeBrightness()
            .GroupBy(probe => probe.GdiDeviceName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<DdcBrightnessProbeResult>)group.ToArray(), StringComparer.OrdinalIgnoreCase);

        return activeDisplays
            .Select(display => new MonitorDdcProbeInfo(
                display,
                probesByDisplay.TryGetValue(display.GdiDeviceName, out var probes)
                    ? probes
                    : [NoPhysicalMonitor(display.GdiDeviceName)]))
            .ToArray();
    }

    private static DdcBrightnessProbeResult NoPhysicalMonitor(string gdiDeviceName)
    {
        return new DdcBrightnessProbeResult(
            gdiDeviceName,
            string.Empty,
            DdcBrightnessProbeStatus.NoPhysicalMonitor,
            0,
            0,
            0);
    }
}
