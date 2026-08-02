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
    private readonly IBrightnessWriter _wmiWriter;

    public KeyboardBrightnessSyncService()
        : this(new WindowsDdcBrightnessWriter(), new WindowsWmiBrightnessWriter())
    {
    }

    public KeyboardBrightnessSyncService(
        IBrightnessWriter ddcWriter,
        IBrightnessWriter? wmiWriter = null)
    {
        ArgumentNullException.ThrowIfNull(ddcWriter);
        _ddcWriter = ddcWriter;
        _wmiWriter = wmiWriter ?? ddcWriter;
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

    public IReadOnlyList<BrightnessWriteResult> AdjustBy(
        int deltaPercent,
        IReadOnlyList<MonitorDdcProbeInfo> ddcProbes,
        IReadOnlyList<WmiBrightnessProbeResult> wmiProbes)
    {
        var results = new List<BrightnessWriteResult>();
        foreach (var ddcProbe in ddcProbes)
        {
            var wmiProbe = wmiProbes.FirstOrDefault(probe => string.Equals(
                probe.Display.DevicePath,
                ddcProbe.Display.DevicePath,
                StringComparison.OrdinalIgnoreCase));
            if (wmiProbe?.Status == WmiBrightnessProbeStatus.ReadSucceeded)
            {
                var requested = Math.Clamp(wmiProbe.CurrentBrightness + deltaPercent, 0, 100);
                results.Add(_wmiWriter.WriteBrightness(ddcProbe.Display, requested));
                continue;
            }

            var readableDdc = ddcProbe.PhysicalMonitors.FirstOrDefault(result =>
                result.Status == DdcBrightnessProbeStatus.ReadSucceeded &&
                result.MaximumValue > 0);
            if (readableDdc is null)
            {
                continue;
            }

            var currentPercent = (int)Math.Round(
                readableDdc.CurrentValue * 100d / readableDdc.MaximumValue,
                MidpointRounding.AwayFromZero);
            results.Add(_ddcWriter.WriteBrightness(
                ddcProbe.Display,
                Math.Clamp(currentPercent + deltaPercent, 0, 100)));
        }

        return results;
    }
}
