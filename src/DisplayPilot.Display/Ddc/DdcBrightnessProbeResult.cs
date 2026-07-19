namespace DisplayPilot.Display.Ddc;

/// <summary>
/// Result of probing brightness VCP code 0x10 on one physical monitor handle.
/// </summary>
public sealed record DdcBrightnessProbeResult(
    string GdiDeviceName,
    string PhysicalMonitorDescription,
    DdcBrightnessProbeStatus Status,
    uint CurrentValue,
    uint MaximumValue,
    int Win32Error,
    int AttemptCount = 0);
