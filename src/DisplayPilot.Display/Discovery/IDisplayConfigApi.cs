namespace DisplayPilot.Display.Discovery;

/// <summary>
/// Read-only boundary around the Windows DisplayConfig APIs.
/// </summary>
public interface IDisplayConfigApi
{
    IReadOnlyList<DisplayConfigPathDescriptor> QueryActiveDisplayPaths();
}
