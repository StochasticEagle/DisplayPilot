// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using DisplayPilot.Display.Ddc;
using DisplayPilot.Display.Discovery;
using DisplayPilot.Display.Interop;
using DisplayPilot.Display.Wmi;

namespace DisplayPilot.Display.Brightness;

/// <summary>
/// Selects exactly one validated brightness path for a display. WMI wins for an
/// internal panel; otherwise a successfully read DDC/CI path is used.
/// </summary>
public sealed class BrightnessControlService
{
    private readonly IBrightnessWriter _ddcWriter;
    private readonly IBrightnessWriter _wmiWriter;

    public BrightnessControlService()
        : this(new WindowsDdcBrightnessWriter(), new WindowsWmiBrightnessWriter())
    {
    }

    public BrightnessControlService(IBrightnessWriter ddcWriter, IBrightnessWriter wmiWriter)
    {
        ArgumentNullException.ThrowIfNull(ddcWriter);
        ArgumentNullException.ThrowIfNull(wmiWriter);
        _ddcWriter = ddcWriter;
        _wmiWriter = wmiWriter;
    }

    public BrightnessWriteResult SetBrightness(
        MonitorDisplayInfo display,
        MonitorDdcProbeInfo ddcProbe,
        WmiBrightnessProbeResult wmiProbe,
        int requestedPercent)
    {
        ArgumentNullException.ThrowIfNull(display);
        ArgumentNullException.ThrowIfNull(ddcProbe);
        ArgumentNullException.ThrowIfNull(wmiProbe);

        requestedPercent = Math.Clamp(requestedPercent, 0, 100);
        if (wmiProbe.Status == WmiBrightnessProbeStatus.ReadSucceeded)
        {
            return _wmiWriter.WriteBrightness(display, requestedPercent);
        }

        if (ddcProbe.PhysicalMonitors.Any(result =>
            result.Status == DdcBrightnessProbeStatus.ReadSucceeded))
        {
            return _ddcWriter.WriteBrightness(display, requestedPercent);
        }

        return BrightnessWriteResult.Unsupported(requestedPercent);
    }
}
