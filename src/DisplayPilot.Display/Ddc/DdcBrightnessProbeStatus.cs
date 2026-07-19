namespace DisplayPilot.Display.Ddc;

/// <summary>
/// Outcome of a read-only brightness VCP (0x10) probe.
/// </summary>
public enum DdcBrightnessProbeStatus
{
    NoPhysicalMonitor,
    PhysicalMonitorEnumerationFailed,
    PhysicalMonitorHandleUnavailable,
    ReadSucceeded,
    ReadFailed,
}
