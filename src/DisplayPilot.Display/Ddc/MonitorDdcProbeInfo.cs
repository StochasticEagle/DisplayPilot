using DisplayPilot.Display.Discovery;

namespace DisplayPilot.Display.Ddc;

/// <summary>
/// Correlates an active Windows display path with its read-only DDC/CI probe results.
/// </summary>
public sealed record MonitorDdcProbeInfo(
    MonitorDisplayInfo Display,
    IReadOnlyList<DdcBrightnessProbeResult> PhysicalMonitors);
