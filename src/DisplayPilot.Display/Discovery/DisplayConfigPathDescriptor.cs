namespace DisplayPilot.Display.Discovery;

/// <summary>
/// Raw names returned for an active QueryDisplayConfig path.
/// </summary>
public readonly record struct DisplayConfigPathDescriptor(
    string? DevicePath,
    string? GdiDeviceName,
    string? FriendlyName,
    int MonitorNumber);
