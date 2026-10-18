using System.ComponentModel;
using System.Runtime.InteropServices;
using DisplayPilot.Display.Ddc;

namespace DisplayPilot.Display.Interop;

/// <summary>
/// Enumerates physical monitor handles and performs only a VCP 0x10 read.
/// </summary>
public sealed class WindowsDdcProbeApi : IDdcProbeApi
{
    private const uint MaximumPhysicalMonitorsPerDisplay = 64;
    private const int ErrorInvalidData = 13;
    private const int MaximumHandleAcquisitionAttempts = 3;
    private const int HandleAcquisitionRetryDelayMilliseconds = 200;
    private const int MaximumVcpReadAttempts = 3;
    private const int VcpReadRetryDelayMilliseconds = 75;

    public IReadOnlyList<DdcBrightnessProbeResult> ProbeBrightness()
    {
        var logicalMonitors = EnumerateLogicalMonitors();
        var results = new List<DdcBrightnessProbeResult>();

        foreach (var monitor in logicalMonitors)
        {
            ProbeLogicalMonitor(monitor, results);
        }

        return results;
    }

    private static List<LogicalMonitor> EnumerateLogicalMonitors()
    {
        var monitors = new List<LogicalMonitor>();

        DisplayNative.MonitorCallback callback = (monitor, _, _, _) =>
        {
            unsafe
            {
                var info = new DisplayMonitorInfo
                {
                    Size = (uint)sizeof(DisplayMonitorInfo),
                };

                if (!DisplayNative.GetMonitorInfo(monitor, &info))
                {
                    return true;
                }

                monitors.Add(new LogicalMonitor(monitor, info.GetDeviceName()));
                return true;
            }
        };

        if (!DisplayNative.EnumDisplayMonitors(0, 0, callback, 0))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        return monitors;
    }

    private static unsafe void ProbeLogicalMonitor(
        LogicalMonitor logicalMonitor,
        List<DdcBrightnessProbeResult> results)
    {
        DdcBrightnessProbeResult? lastFailure = null;

        for (var acquisitionAttempt = 1; acquisitionAttempt <= MaximumHandleAcquisitionAttempts; acquisitionAttempt++)
        {
            if (acquisitionAttempt > 1)
            {
                Thread.Sleep(HandleAcquisitionRetryDelayMilliseconds);
            }

            if (!DisplayNative.GetNumberOfPhysicalMonitorsFromHMONITOR(logicalMonitor.Handle, out var count))
            {
                lastFailure = FailedEnumeration(
                    logicalMonitor.DeviceName,
                    Marshal.GetLastPInvokeError(),
                    acquisitionAttempt);
                continue;
            }

            if (count == 0)
            {
                lastFailure = NoPhysicalMonitor(logicalMonitor.DeviceName, acquisitionAttempt);
                continue;
            }

            if (count > MaximumPhysicalMonitorsPerDisplay)
            {
                results.Add(FailedEnumeration(logicalMonitor.DeviceName, ErrorInvalidData, acquisitionAttempt));
                return;
            }

            var physicalMonitors = new DdcMonitorHandle[count];
            var enumerationSucceeded = false;
            var enumerationError = 0;
            fixed (DdcMonitorHandle* physicalMonitorPointer = physicalMonitors)
            {
                enumerationSucceeded = DisplayNative.GetPhysicalMonitorsFromHMONITOR(
                    logicalMonitor.Handle,
                    count,
                    physicalMonitorPointer);
                if (!enumerationSucceeded)
                {
                    enumerationError = Marshal.GetLastPInvokeError();
                }
            }

            if (!enumerationSucceeded)
            {
                lastFailure = FailedEnumeration(
                    logicalMonitor.DeviceName,
                    enumerationError,
                    acquisitionAttempt);
                continue;
            }

            try
            {
                var hasNullHandles = physicalMonitors.Any(monitor => monitor.Handle == 0);
                if (hasNullHandles && acquisitionAttempt < MaximumHandleAcquisitionAttempts)
                {
                    continue;
                }

                foreach (var physicalMonitor in physicalMonitors)
                {
                    results.Add(physicalMonitor.Handle == 0
                        ? HandleUnavailable(logicalMonitor.DeviceName, physicalMonitor, acquisitionAttempt)
                        : ProbePhysicalMonitor(logicalMonitor.DeviceName, physicalMonitor, acquisitionAttempt));
                }

                return;
            }
            finally
            {
                foreach (var physicalMonitor in physicalMonitors)
                {
                    if (physicalMonitor.Handle != 0)
                    {
                        _ = DisplayNative.DestroyPhysicalMonitor(physicalMonitor.Handle);
                    }
                }
            }
        }

        results.Add(lastFailure ?? FailedEnumeration(
            logicalMonitor.DeviceName,
            ErrorInvalidData,
            MaximumHandleAcquisitionAttempts));
    }

    private static DdcBrightnessProbeResult ProbePhysicalMonitor(
        string gdiDeviceName,
        DdcMonitorHandle physicalMonitor,
        int handleAcquisitionAttempts)
    {
        var description = physicalMonitor.GetDescription();
        if (physicalMonitor.Handle == 0)
        {
            return new DdcBrightnessProbeResult(
                gdiDeviceName,
                description,
                DdcBrightnessProbeStatus.ReadFailed,
                0,
                0,
                ErrorInvalidData,
                0,
                handleAcquisitionAttempts);
        }

        var lastError = 0;
        for (var attempt = 1; attempt <= MaximumVcpReadAttempts; attempt++)
        {
            if (DisplayNative.GetVCPFeatureAndVCPFeatureReply(
                physicalMonitor.Handle,
                NativeConstants.VcpCodeBrightness,
                0,
                out var currentValue,
                out var maximumValue))
            {
                return new DdcBrightnessProbeResult(
                    gdiDeviceName,
                    description,
                    DdcBrightnessProbeStatus.ReadSucceeded,
                    currentValue,
                    maximumValue,
                    0,
                    attempt,
                    handleAcquisitionAttempts);
            }

            lastError = Marshal.GetLastPInvokeError();
            if (attempt < MaximumVcpReadAttempts)
            {
                Thread.Sleep(VcpReadRetryDelayMilliseconds);
            }
        }

        return new DdcBrightnessProbeResult(
            gdiDeviceName,
            description,
            DdcBrightnessProbeStatus.ReadFailed,
            0,
            0,
            lastError,
            MaximumVcpReadAttempts,
            handleAcquisitionAttempts);
    }

    private static DdcBrightnessProbeResult NoPhysicalMonitor(
        string gdiDeviceName,
        int handleAcquisitionAttempts)
    {
        return new DdcBrightnessProbeResult(
            gdiDeviceName,
            string.Empty,
            DdcBrightnessProbeStatus.NoPhysicalMonitor,
            0,
            0,
            0,
            0,
            handleAcquisitionAttempts);
    }

    private static DdcBrightnessProbeResult FailedEnumeration(
        string gdiDeviceName,
        int error,
        int handleAcquisitionAttempts)
    {
        return new DdcBrightnessProbeResult(
            gdiDeviceName,
            string.Empty,
            DdcBrightnessProbeStatus.PhysicalMonitorEnumerationFailed,
            0,
            0,
            error,
            0,
            handleAcquisitionAttempts);
    }

    private static DdcBrightnessProbeResult HandleUnavailable(
        string gdiDeviceName,
        DdcMonitorHandle physicalMonitor,
        int handleAcquisitionAttempts)
    {
        return new DdcBrightnessProbeResult(
            gdiDeviceName,
            physicalMonitor.GetDescription(),
            DdcBrightnessProbeStatus.PhysicalMonitorHandleUnavailable,
            0,
            0,
            ErrorInvalidData,
            0,
            handleAcquisitionAttempts);
    }

    private sealed record LogicalMonitor(nint Handle, string DeviceName);
}
