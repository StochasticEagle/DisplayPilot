// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using DisplayPilot.Display.Ddc;
using DisplayPilot.Display.Interop;
using DisplayPilot.Display.Wmi;

namespace DisplayPilot.Display.Brightness;

/// <summary>
/// Mirrors an integrated-panel brightness-key result to validated external
/// DDC/CI displays. The integrated panel remains under Windows control.
/// </summary>
public sealed class KeyboardBrightnessSyncService
{
    private readonly IBrightnessWriter _ddcWriter;

    public KeyboardBrightnessSyncService()
        : this(new WindowsDdcBrightnessWriter())
    {
    }

    public KeyboardBrightnessSyncService(IBrightnessWriter ddcWriter)
    {
        ArgumentNullException.ThrowIfNull(ddcWriter);
        _ddcWriter = ddcWriter;
    }

    public IReadOnlyList<BrightnessWriteResult> Synchronize(
        int brightnessPercent,
        IReadOnlyList<MonitorDdcProbeInfo> ddcProbes,
        IReadOnlyList<WmiBrightnessProbeResult> wmiProbes)
    {
        brightnessPercent = Math.Clamp(brightnessPercent, 0, 100);
        var results = new List<BrightnessWriteResult>();
        foreach (var ddcProbe in ddcProbes)
        {
            var isIntegratedPanel = wmiProbes.Any(probe =>
                string.Equals(
                    probe.Display.DevicePath,
                    ddcProbe.Display.DevicePath,
                    StringComparison.OrdinalIgnoreCase)
                && probe.Status == WmiBrightnessProbeStatus.ReadSucceeded);
            if (isIntegratedPanel || !ddcProbe.PhysicalMonitors.Any(result =>
                    result.Status == DdcBrightnessProbeStatus.ReadSucceeded))
            {
                continue;
            }

            results.Add(_ddcWriter.WriteBrightness(ddcProbe.Display, brightnessPercent));
        }

        return results;
    }
}
