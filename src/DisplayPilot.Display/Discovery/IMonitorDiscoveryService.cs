namespace DisplayPilot.Display.Discovery;

/// <summary>
/// Discovers active monitor identities without issuing DDC/CI commands.
/// </summary>
public interface IMonitorDiscoveryService
{
    IReadOnlyList<MonitorDisplayInfo> DiscoverActiveMonitors();
}
