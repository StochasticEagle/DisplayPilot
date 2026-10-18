namespace DisplayPilot.Display.Discovery;

/// <summary>
/// Identifies one active Windows display path without opening the physical monitor.
/// </summary>
public sealed record MonitorDisplayInfo(
    string DevicePath,
    string GdiDeviceName,
    string FriendlyName,
    int MonitorNumber);
