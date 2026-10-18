// SPDX-License-Identifier: Unlicense
using DisplayPilot.Display.Discovery;
using DisplayPilot.Display.Interop;
using DisplayPilot.Display.Models;

namespace DisplayPilot.Display.Wmi;

/// <summary>
/// Correlates active display paths with read-only WMI brightness instances.
/// </summary>
public sealed class WmiBrightnessProbeService
{
    private readonly IWmiBrightnessApi _api;

    public WmiBrightnessProbeService()
        : this(new WindowsWmiBrightnessApi())
    {
    }

    public WmiBrightnessProbeService(IWmiBrightnessApi api)
    {
        _api = api;
    }

    public IReadOnlyList<WmiBrightnessProbeResult> ProbeBrightness(
        IReadOnlyList<MonitorDisplayInfo> displays)
    {
        var query = _api.QueryBrightness();
        if (!query.Succeeded)
        {
            return displays.Select(display => new WmiBrightnessProbeResult(
                display,
                WmiBrightnessProbeStatus.QueryFailed,
                string.Empty,
                0,
                0,
                query.ErrorCode,
                query.ErrorMessage)).ToArray();
        }

        return displays.Select(display => Correlate(display, query.Instances)).ToArray();
    }

    private static WmiBrightnessProbeResult Correlate(
        MonitorDisplayInfo display,
        IReadOnlyList<WmiBrightnessInstance> instances)
    {
        var displayId = DisplayPathKey.FromDevicePath(display.DevicePath);
        var matches = instances.Where(instance => string.Equals(
            DisplayPathKey.FromWmiInstance(instance.InstanceName),
            displayId,
            StringComparison.OrdinalIgnoreCase)).ToArray();
        var match = matches.FirstOrDefault(instance => instance.Active) ?? matches.FirstOrDefault();

        if (match is null)
        {
            return new WmiBrightnessProbeResult(
                display,
                WmiBrightnessProbeStatus.NotAvailable,
                string.Empty,
                0,
                0);
        }

        return new WmiBrightnessProbeResult(
            display,
            match.Active ? WmiBrightnessProbeStatus.ReadSucceeded : WmiBrightnessProbeStatus.Inactive,
            match.InstanceName,
            match.CurrentBrightness,
            match.Levels.Count);
    }
}
