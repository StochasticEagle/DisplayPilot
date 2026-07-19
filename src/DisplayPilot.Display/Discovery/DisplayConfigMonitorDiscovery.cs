// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using DisplayPilot.Display.Interop;

namespace DisplayPilot.Display.Discovery;

/// <summary>
/// Builds a stable, read-only monitor inventory from active Windows display paths.
/// </summary>
public sealed class DisplayConfigMonitorDiscovery : IMonitorDiscoveryService
{
    private readonly IDisplayConfigApi _displayConfigApi;

    public DisplayConfigMonitorDiscovery()
        : this(new WindowsDisplayConfigApi())
    {
    }

    public DisplayConfigMonitorDiscovery(IDisplayConfigApi displayConfigApi)
    {
        ArgumentNullException.ThrowIfNull(displayConfigApi);
        _displayConfigApi = displayConfigApi;
    }

    public IReadOnlyList<MonitorDisplayInfo> DiscoverActiveMonitors()
    {
        var byDevicePath = new Dictionary<string, MonitorDisplayInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in _displayConfigApi.QueryActiveDisplayPaths().OrderBy(path => path.MonitorNumber))
        {
            var devicePath = path.DevicePath?.Trim();
            var gdiDeviceName = path.GdiDeviceName?.Trim();

            if (string.IsNullOrWhiteSpace(devicePath) || string.IsNullOrWhiteSpace(gdiDeviceName))
            {
                continue;
            }

            var monitorNumber = path.MonitorNumber > 0 ? path.MonitorNumber : byDevicePath.Count + 1;
            var friendlyName = string.IsNullOrWhiteSpace(path.FriendlyName)
                ? $"Display {monitorNumber}"
                : path.FriendlyName.Trim();

            if (!byDevicePath.TryAdd(
                    devicePath,
                    new MonitorDisplayInfo(devicePath, gdiDeviceName, friendlyName, monitorNumber)))
            {
                continue;
            }
        }

        return byDevicePath.Values.OrderBy(monitor => monitor.MonitorNumber).ToArray();
    }
}
