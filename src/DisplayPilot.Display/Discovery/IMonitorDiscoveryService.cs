// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace DisplayPilot.Display.Discovery;

/// <summary>
/// Discovers active monitor identities without issuing DDC/CI commands.
/// </summary>
public interface IMonitorDiscoveryService
{
    IReadOnlyList<MonitorDisplayInfo> DiscoverActiveMonitors();
}
