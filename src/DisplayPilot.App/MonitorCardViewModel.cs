using System.Globalization;
using DisplayPilot.Display.Ddc;
using DisplayPilot.Display.Discovery;

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
            "DDC/CI: not probed",
            "Select Read brightness (VCP 0x10) to perform an explicit read-only hardware query.");
    }

    public static MonitorCardViewModel FromProbe(MonitorDdcProbeInfo probe)
    {
        var successfulReads = probe.PhysicalMonitors
            .Where(result => result.Status == DdcBrightnessProbeStatus.ReadSucceeded)
            .ToArray();

        if (successfulReads.Length > 0)
        {
            return new MonitorCardViewModel(
                probe.Display.DevicePath,
                probe.Display.GdiDeviceName,
                probe.Display.FriendlyName,
                "DDC/CI: brightness is readable",
                string.Join(Environment.NewLine, successfulReads.Select(FormatSuccessfulRead)));
        }

        var details = string.Join(Environment.NewLine, probe.PhysicalMonitors.Select(FormatFailure));
        return new MonitorCardViewModel(
            probe.Display.DevicePath,
            probe.Display.GdiDeviceName,
            probe.Display.FriendlyName,
            "DDC/CI: brightness read unavailable",
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
}
