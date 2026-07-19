using DisplayPilot.Display.Discovery;

namespace DisplayPilot.Display.Ddc;

/// <summary>
/// Correlates read-only DDC/CI brightness probes with active display paths.
/// </summary>
public interface IDdcBrightnessProbeService
{
    IReadOnlyList<MonitorDdcProbeInfo> ProbeBrightness(
        IReadOnlyList<MonitorDisplayInfo> activeDisplays);
}
