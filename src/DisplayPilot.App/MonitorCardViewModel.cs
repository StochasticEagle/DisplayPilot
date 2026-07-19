using System.Globalization;
using DisplayPilot.Display.Ddc;
using DisplayPilot.Display.Discovery;
using DisplayPilot.Display.Wmi;

namespace DisplayPilot.App;

public sealed record MonitorCardViewModel(
    string DevicePath,
    string GdiDeviceName,
    string FriendlyName,
    string DdcStatus,
    string DdcDetails)
{
    public static MonitorCardViewModel NotProbed(MonitorDisplayInfo display)
    {
        return new MonitorCardViewModel(
            display.DevicePath,
            display.GdiDeviceName,
            display.FriendlyName,
            "Brightness: not probed",
            "Select Read brightness (DDC + WMI) to perform explicit read-only hardware queries.");
    }

    public static MonitorCardViewModel FromProbes(
        MonitorDdcProbeInfo ddcProbe,
        WmiBrightnessProbeResult wmiProbe)
    {
        var successfulReads = ddcProbe.PhysicalMonitors
            .Where(result => result.Status == DdcBrightnessProbeStatus.ReadSucceeded)
            .ToArray();

        if (successfulReads.Length > 0)
        {
            return new MonitorCardViewModel(
                ddcProbe.Display.DevicePath,
                ddcProbe.Display.GdiDeviceName,
                ddcProbe.Display.FriendlyName,
                "Brightness is readable through DDC/CI",
                string.Join(Environment.NewLine, successfulReads.Select(FormatSuccessfulRead)));
        }

        if (wmiProbe.Status == WmiBrightnessProbeStatus.ReadSucceeded)
        {
            return new MonitorCardViewModel(
                ddcProbe.Display.DevicePath,
                ddcProbe.Display.GdiDeviceName,
                ddcProbe.Display.FriendlyName,
                "Brightness is readable through WMI",
                string.Format(
                    CultureInfo.CurrentCulture,
                    "Internal panel: {0}% ({1} advertised brightness levels)",
                    wmiProbe.CurrentBrightness,
                    wmiProbe.LevelCount));
        }

        var ddcDetails = string.Join(
            Environment.NewLine,
            ddcProbe.PhysicalMonitors.Select(FormatFailure));
        var details = string.Join(
            Environment.NewLine,
            new[] { ddcDetails, FormatWmiFailure(wmiProbe) }.Where(value => !string.IsNullOrWhiteSpace(value)));
        return new MonitorCardViewModel(
            ddcProbe.Display.DevicePath,
            ddcProbe.Display.GdiDeviceName,
            ddcProbe.Display.FriendlyName,
            "Brightness read unavailable",
            details);
    }

    private static string FormatSuccessfulRead(DdcBrightnessProbeResult result)
    {
        var description = DescriptionOrDefault(result);
        return string.Format(
            CultureInfo.CurrentCulture,
            "{0}: raw {1} of {2} (VCP 0x10; handle attempt {3}, read attempt {4})",
            description,
            result.CurrentValue,
            result.MaximumValue,
            result.HandleAcquisitionAttempts,
            result.AttemptCount);
    }

    private static string FormatFailure(DdcBrightnessProbeResult result)
    {
        var description = DescriptionOrDefault(result);
        return result.Status switch
        {
            DdcBrightnessProbeStatus.NoPhysicalMonitor =>
                string.Format(
                    CultureInfo.CurrentCulture,
                    "Windows exposed no physical monitor after {0} handle-acquisition attempts. This is expected for most VMs.",
                    result.HandleAcquisitionAttempts),
            DdcBrightnessProbeStatus.PhysicalMonitorEnumerationFailed =>
                string.Format(
                    CultureInfo.CurrentCulture,
                    "Physical monitor enumeration failed after {0} attempts (Win32 error {1}).",
                    result.HandleAcquisitionAttempts,
                    FormatWin32Error(result.Win32Error)),
            DdcBrightnessProbeStatus.PhysicalMonitorHandleUnavailable =>
                string.Format(
                    CultureInfo.CurrentCulture,
                    "Windows returned a null physical-monitor handle after {0} attempts (Win32 error {1}).",
                    result.HandleAcquisitionAttempts,
                    FormatWin32Error(result.Win32Error)),
            DdcBrightnessProbeStatus.ReadFailed =>
                string.Format(
                    CultureInfo.CurrentCulture,
                    "{0}: VCP 0x10 read failed after {1} attempts (Win32 error {2}).",
                    description,
                    result.AttemptCount,
                    FormatWin32Error(result.Win32Error)),
            _ => "Brightness probe returned an unknown result.",
        };
    }

    private static string DescriptionOrDefault(DdcBrightnessProbeResult result)
    {
        return string.IsNullOrWhiteSpace(result.PhysicalMonitorDescription)
            ? "Physical monitor"
            : result.PhysicalMonitorDescription;
    }

    private static string FormatWin32Error(int error)
    {
        return error == 0
            ? "0"
            : string.Format(CultureInfo.InvariantCulture, "{0} / 0x{1:X8}", error, unchecked((uint)error));
    }

    private static string FormatWmiFailure(WmiBrightnessProbeResult result)
    {
        return result.Status switch
        {
            WmiBrightnessProbeStatus.NotAvailable =>
                "WMI: no matching WmiMonitorBrightness instance was exposed for this display.",
            WmiBrightnessProbeStatus.Inactive =>
                "WMI: the matching brightness instance is currently inactive.",
            WmiBrightnessProbeStatus.QueryFailed =>
                string.Format(
                    CultureInfo.CurrentCulture,
                    "WMI brightness query failed (error {0}).",
                    FormatWin32Error(result.ErrorCode)),
            _ => string.Empty,
        };
    }
}
