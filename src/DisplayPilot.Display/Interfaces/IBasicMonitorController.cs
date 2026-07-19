// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using DisplayPilot.Display.Models;

namespace DisplayPilot.Display.Interfaces;

/// <summary>
/// Operations supported by both DDC/CI external monitors and WMI-controlled internal panels.
/// Brightness setters accept a normalized percentage from 0 through 100.
/// </summary>
public interface IBasicMonitorController
{
    string Name { get; }

    Task<VcpFeatureValue> GetBrightnessAsync(
        Monitor monitor,
        CancellationToken cancellationToken = default);

    Task<MonitorOperationResult> SetBrightnessAsync(
        Monitor monitor,
        int brightness,
        CancellationToken cancellationToken = default);
}
