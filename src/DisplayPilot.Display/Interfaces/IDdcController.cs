// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using DisplayPilot.Display.Models;
using Monitor = DisplayPilot.Display.Models.Monitor;

namespace DisplayPilot.Display.Interfaces;

/// <summary>
/// Extended features carried by DDC/CI VCP codes. Default implementations safely
/// report unsupported operations for controllers such as WMI.
/// </summary>
public interface IDdcController
{
    Task<DisplayControlReading> GetContrastAsync(Monitor monitor, CancellationToken cancellationToken = default)
        => Task.FromResult(DisplayControlReading.Invalid);

    Task<MonitorOperationResult> SetContrastAsync(Monitor monitor, int contrast, CancellationToken cancellationToken = default)
        => Unsupported("Contrast");

    Task<DisplayControlReading> GetVolumeAsync(Monitor monitor, CancellationToken cancellationToken = default)
        => Task.FromResult(DisplayControlReading.Invalid);

    Task<MonitorOperationResult> SetVolumeAsync(Monitor monitor, int volume, CancellationToken cancellationToken = default)
        => Unsupported("Volume");

    Task<DisplayControlReading> GetColorTemperatureAsync(Monitor monitor, CancellationToken cancellationToken = default)
        => Task.FromResult(DisplayControlReading.Invalid);

    Task<MonitorOperationResult> SetColorTemperatureAsync(Monitor monitor, int colorTemperature, CancellationToken cancellationToken = default)
        => Unsupported("Color temperature");

    Task<DisplayControlReading> GetInputSourceAsync(Monitor monitor, CancellationToken cancellationToken = default)
        => Task.FromResult(DisplayControlReading.Invalid);

    Task<MonitorOperationResult> SetInputSourceAsync(Monitor monitor, int inputSource, CancellationToken cancellationToken = default)
        => Unsupported("Input source");

    Task<DisplayControlReading> GetPowerStateAsync(Monitor monitor, CancellationToken cancellationToken = default)
        => Task.FromResult(DisplayControlReading.Invalid);

    Task<MonitorOperationResult> SetPowerStateAsync(Monitor monitor, int powerState, CancellationToken cancellationToken = default)
        => Unsupported("Power state");

    private static Task<MonitorOperationResult> Unsupported(string feature)
        => Task.FromResult(MonitorOperationResult.Failure($"{feature} is not supported by this controller"));
}
