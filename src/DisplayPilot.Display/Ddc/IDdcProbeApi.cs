namespace DisplayPilot.Display.Ddc;

/// <summary>
/// Read-only boundary around Windows physical-monitor and VCP query APIs.
/// </summary>
public interface IDdcProbeApi
{
    IReadOnlyList<DdcBrightnessProbeResult> ProbeBrightness();
}
