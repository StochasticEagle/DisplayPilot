// SPDX-License-Identifier: Unlicense
using DisplayPilot.Display.Ddc;

namespace DisplayPilot.Display.Interop;

/// <summary>
/// Enumerates physical monitor handles and performs only a VCP 0x10 read.
/// </summary>
public sealed class WindowsDdcProbeApi : IDdcProbeApi
{
    private const int ErrorInvalidData = 13;

    public IReadOnlyList<DdcBrightnessProbeResult> ProbeBrightness()
    {
        var logicalMonitors = DisplayNative.EnumerateLogicalMonitors();
        var results = new List<DdcBrightnessProbeResult>();

        foreach (var monitor in logicalMonitors)
        {
            ProbeLogicalMonitor(monitor, results);
        }

        return results;
    }

    private static void ProbeLogicalMonitor(
        LogicalDisplayMonitor logicalMonitor,
        List<DdcBrightnessProbeResult> results)
    {
        DdcBrightnessProbeResult? lastFailure = null;
        for (int attempt = 1; attempt <= DisplayNative.HandleAttempts; attempt++)
        {
            if (attempt > 1) Thread.Sleep(DisplayNative.HandleRetryMilliseconds);
            var handles = DisplayNative.AcquirePhysicalMonitors(
                logicalMonitor.Handle, out var count, out var error);
            if (handles is null)
            {
                lastFailure = count == 0 && error == 0
                    ? NoPhysicalMonitor(logicalMonitor.DeviceName, attempt)
                    : FailedEnumeration(logicalMonitor.DeviceName, error, attempt);
                if (count > DisplayNative.PhysicalMonitorLimit) break;
                continue;
            }
            try
            {
                if (handles.Any(monitor => monitor.Handle == 0) &&
                    attempt < DisplayNative.HandleAttempts)
                    continue;

                foreach (var handle in handles)
                {
                    results.Add(handle.Handle == 0
                        ? HandleUnavailable(logicalMonitor.DeviceName, handle, attempt)
                        : ProbePhysicalMonitor(logicalMonitor.DeviceName, handle, attempt));
                }
                return;
            }
            finally
            {
                DisplayNative.ReleasePhysicalMonitors(handles);
            }
        }
        results.Add(lastFailure ?? FailedEnumeration(
            logicalMonitor.DeviceName, DisplayNative.InvalidDataError, DisplayNative.HandleAttempts));
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

        var success = DisplayNative.TryReadVcp(
            physicalMonitor.Handle, DisplayInteropConstants.VcpCodeBrightness,
            out var currentValue, out var maximumValue, out var error, out var readAttempts);
        return new DdcBrightnessProbeResult(
            gdiDeviceName, description,
            success ? DdcBrightnessProbeStatus.ReadSucceeded : DdcBrightnessProbeStatus.ReadFailed,
            currentValue, maximumValue, error, readAttempts, handleAcquisitionAttempts);
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

}
