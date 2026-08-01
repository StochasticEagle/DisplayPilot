// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using DisplayPilot.Display.Discovery;
using DisplayPilot.Display.Interop;
using DisplayPilot.Display.Mccs;
using DisplayPilot.Display.Models;

namespace DisplayPilot.Display.Ddc;

/// <summary>
/// Reads and writes one standard monitor VCP feature at a time. Every write is
/// followed by an immediate read-back through the active physical-monitor handle.
/// </summary>
public static class WindowsDdcVcpFeatureService
{
    private const uint MaximumPhysicalMonitorsPerDisplay = 64;
    private const int ErrorInvalidData = 13;
    private const int MaximumHandleAttempts = 3;
    private const int MaximumReadAttempts = 3;
    private const int HandleRetryDelayMilliseconds = 200;
    private const int ReadRetryDelayMilliseconds = 75;

    public static IReadOnlyList<MonitorDdcVcpFeatureInfo> ReadFeature(
        IReadOnlyList<MonitorDisplayInfo> activeDisplays,
        byte vcpCode)
    {
        ArgumentNullException.ThrowIfNull(activeDisplays);
        var results = ProbeFeature(vcpCode)
            .GroupBy(result => result.GdiDeviceName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<DdcVcpFeatureResult>)group.ToArray(),
                StringComparer.OrdinalIgnoreCase);

        return activeDisplays.Select(display => new MonitorDdcVcpFeatureInfo(
            display,
            results.TryGetValue(display.GdiDeviceName, out var physicalMonitors)
                ? physicalMonitors
                : [Failure(
                    display.GdiDeviceName,
                    string.Empty,
                    vcpCode,
                    DdcVcpFeatureStatus.NoPhysicalMonitor,
                    0,
                    0)])).ToArray();
    }

    public static DdcVcpWriteResult WriteContinuousPercent(
        MonitorDisplayInfo display,
        byte vcpCode,
        int requestedPercent)
    {
        ArgumentNullException.ThrowIfNull(display);
        requestedPercent = Math.Clamp(requestedPercent, 0, 100);
        return WriteFeature(display, vcpCode, checked((uint)requestedPercent), continuous: true);
    }

    public static DdcVcpWriteResult WriteDiscreteValue(
        MonitorDisplayInfo display,
        byte vcpCode,
        uint requestedValue)
    {
        ArgumentNullException.ThrowIfNull(display);
        return WriteFeature(display, vcpCode, requestedValue, continuous: false);
    }

    public static IReadOnlyList<MonitorDdcCapabilitiesInfo> ReadCapabilities(
        IReadOnlyList<MonitorDisplayInfo> activeDisplays)
    {
        ArgumentNullException.ThrowIfNull(activeDisplays);
        var logicalMonitors = EnumerateLogicalMonitors();
        return activeDisplays.Select(display =>
        {
            var logicalMonitor = logicalMonitors.FirstOrDefault(candidate =>
                string.Equals(candidate.DeviceName, display.GdiDeviceName, StringComparison.OrdinalIgnoreCase));
            return logicalMonitor is null
                ? new MonitorDdcCapabilitiesInfo(display, false, string.Empty, VcpCapabilities.Empty, ErrorInvalidData)
                : ReadCapabilities(display, logicalMonitor);
        }).ToArray();
    }

    private static DdcVcpWriteResult WriteFeature(
        MonitorDisplayInfo display,
        byte vcpCode,
        uint requestedValue,
        bool continuous)
    {
        try
        {
            var logicalMonitor = EnumerateLogicalMonitors().FirstOrDefault(monitor =>
                string.Equals(monitor.DeviceName, display.GdiDeviceName, StringComparison.OrdinalIgnoreCase));
            if (logicalMonitor is null)
            {
                return WriteFailure(vcpCode, requestedValue, ErrorInvalidData, "The active logical monitor was not found.");
            }

            var lastError = ErrorInvalidData;
            for (var attempt = 1; attempt <= MaximumHandleAttempts; attempt++)
            {
                if (attempt > 1)
                {
                    Thread.Sleep(HandleRetryDelayMilliseconds);
                }

                var physicalMonitors = AcquirePhysicalMonitors(logicalMonitor.Handle, out lastError);
                if (physicalMonitors is null)
                {
                    continue;
                }

                try
                {
                    uint? verifiedRaw = null;
                    int? verifiedPercent = null;
                    uint appliedRaw = requestedValue;
                    foreach (var physicalMonitor in physicalMonitors)
                    {
                        if (physicalMonitor.Handle == 0)
                        {
                            return WriteFailure(vcpCode, requestedValue, ErrorInvalidData, "A physical monitor handle was unavailable.");
                        }

                        if (!TryReadFeature(physicalMonitor.Handle, vcpCode, out _, out var maximum, out lastError))
                        {
                            return WriteFailure(vcpCode, requestedValue, lastError, $"Could not read VCP 0x{vcpCode:X2} before writing.");
                        }

                        appliedRaw = continuous
                            ? (uint)VcpFeatureValue.FromPercentage(checked((int)requestedValue), checked((int)maximum))
                            : requestedValue;
                        if (!DdcPInvoke.SetVCPFeature(physicalMonitor.Handle, vcpCode, appliedRaw))
                        {
                            return WriteFailure(vcpCode, requestedValue, Marshal.GetLastPInvokeError(), $"SetVCPFeature rejected VCP 0x{vcpCode:X2}.", appliedRaw);
                        }

                        Thread.Sleep(ReadRetryDelayMilliseconds);
                        if (!TryReadFeature(physicalMonitor.Handle, vcpCode, out var current, out maximum, out lastError))
                        {
                            return new DdcVcpWriteResult(
                                vcpCode,
                                DdcVcpWriteStatus.VerificationFailed,
                                requestedValue,
                                appliedRaw,
                                null,
                                continuous ? checked((int)requestedValue) : null,
                                null,
                                lastError,
                                "The write returned success, but read-back failed.");
                        }

                        verifiedRaw = current;
                        if (continuous)
                        {
                            if (maximum == 0)
                            {
                                return WriteFailure(vcpCode, requestedValue, ErrorInvalidData, "The monitor reported a zero feature maximum.", appliedRaw);
                            }

                            verifiedPercent = new VcpFeatureValue(checked((int)current), checked((int)maximum)).ToPercentage();
                            if (Math.Abs(verifiedPercent.Value - checked((int)requestedValue)) > 1)
                            {
                                return new DdcVcpWriteResult(
                                    vcpCode,
                                    DdcVcpWriteStatus.VerificationFailed,
                                    requestedValue,
                                    appliedRaw,
                                    current,
                                    checked((int)requestedValue),
                                    verifiedPercent,
                                    Message: "Read-back did not match the requested percentage.");
                            }
                        }
                        else if (current != requestedValue)
                        {
                            return new DdcVcpWriteResult(
                                vcpCode,
                                DdcVcpWriteStatus.VerificationFailed,
                                requestedValue,
                                appliedRaw,
                                current,
                                Message: "Read-back did not match the requested VCP value.");
                        }
                    }

                    return new DdcVcpWriteResult(
                        vcpCode,
                        DdcVcpWriteStatus.WriteSucceeded,
                        requestedValue,
                        appliedRaw,
                        verifiedRaw,
                        continuous ? checked((int)requestedValue) : null,
                        verifiedPercent,
                        Message: $"Set and verified {physicalMonitors.Length} physical monitor handle(s).");
                }
                finally
                {
                    DestroyPhysicalMonitors(physicalMonitors);
                }
            }

            return WriteFailure(vcpCode, requestedValue, lastError, "Could not acquire a physical monitor handle.");
        }
        catch (Win32Exception exception)
        {
            return WriteFailure(vcpCode, requestedValue, exception.NativeErrorCode, exception.Message);
        }
        catch (OverflowException exception)
        {
            return WriteFailure(vcpCode, requestedValue, ErrorInvalidData, exception.Message);
        }
    }

    private static unsafe MonitorDdcCapabilitiesInfo ReadCapabilities(
        MonitorDisplayInfo display,
        LogicalMonitor logicalMonitor)
    {
        var lastError = ErrorInvalidData;
        for (var attempt = 1; attempt <= MaximumHandleAttempts; attempt++)
        {
            if (attempt > 1)
            {
                Thread.Sleep(HandleRetryDelayMilliseconds);
            }

            var physicalMonitors = AcquirePhysicalMonitors(logicalMonitor.Handle, out lastError);
            if (physicalMonitors is null)
            {
                continue;
            }

            try
            {
                foreach (var physicalMonitor in physicalMonitors)
                {
                    if (physicalMonitor.Handle == 0 ||
                        !DdcPInvoke.GetCapabilitiesStringLength(physicalMonitor.Handle, out var length))
                    {
                        lastError = physicalMonitor.Handle == 0
                            ? ErrorInvalidData
                            : Marshal.GetLastPInvokeError();
                        continue;
                    }

                    if (length is 0 or > 65536)
                    {
                        lastError = ErrorInvalidData;
                        continue;
                    }

                    var buffer = new byte[length];
                    fixed (byte* pointer = buffer)
                    {
                        if (!DdcPInvoke.CapabilitiesRequestAndCapabilitiesReply(
                                physicalMonitor.Handle,
                                pointer,
                                length))
                        {
                            lastError = Marshal.GetLastPInvokeError();
                            continue;
                        }
                    }

                    var raw = Encoding.ASCII.GetString(buffer).TrimEnd('\0');
                    var parsed = MccsCapabilitiesParser.Parse(raw);
                    return new MonitorDdcCapabilitiesInfo(
                        display,
                        true,
                        raw,
                        parsed.Capabilities);
                }
            }
            finally
            {
                DestroyPhysicalMonitors(physicalMonitors);
            }
        }

        return new MonitorDdcCapabilitiesInfo(
            display,
            false,
            string.Empty,
            VcpCapabilities.Empty,
            lastError);
    }

    private static List<DdcVcpFeatureResult> ProbeFeature(byte vcpCode)
    {
        var results = new List<DdcVcpFeatureResult>();
        foreach (var logicalMonitor in EnumerateLogicalMonitors())
        {
            ProbeLogicalMonitor(logicalMonitor, vcpCode, results);
        }

        return results;
    }

    private static void ProbeLogicalMonitor(
        LogicalMonitor logicalMonitor,
        byte vcpCode,
        List<DdcVcpFeatureResult> results)
    {
        DdcVcpFeatureResult? lastFailure = null;
        for (var attempt = 1; attempt <= MaximumHandleAttempts; attempt++)
        {
            if (attempt > 1)
            {
                Thread.Sleep(HandleRetryDelayMilliseconds);
            }

            var physicalMonitors = AcquirePhysicalMonitors(logicalMonitor.Handle, out var error);
            if (physicalMonitors is null)
            {
                lastFailure = Failure(
                    logicalMonitor.DeviceName,
                    string.Empty,
                    vcpCode,
                    DdcVcpFeatureStatus.PhysicalMonitorEnumerationFailed,
                    error,
                    attempt);
                continue;
            }

            try
            {
                foreach (var physicalMonitor in physicalMonitors)
                {
                    var description = physicalMonitor.GetDescription();
                    if (physicalMonitor.Handle == 0)
                    {
                        results.Add(Failure(
                            logicalMonitor.DeviceName,
                            description,
                            vcpCode,
                            DdcVcpFeatureStatus.PhysicalMonitorHandleUnavailable,
                            ErrorInvalidData,
                            attempt));
                        continue;
                    }

                    if (TryReadFeature(physicalMonitor.Handle, vcpCode, out var current, out var maximum, out error, out var readAttempts))
                    {
                        results.Add(new DdcVcpFeatureResult(
                            logicalMonitor.DeviceName,
                            description,
                            vcpCode,
                            DdcVcpFeatureStatus.ReadSucceeded,
                            current,
                            maximum,
                            0,
                            readAttempts,
                            attempt));
                    }
                    else
                    {
                        results.Add(new DdcVcpFeatureResult(
                            logicalMonitor.DeviceName,
                            description,
                            vcpCode,
                            DdcVcpFeatureStatus.ReadFailed,
                            0,
                            0,
                            error,
                            readAttempts,
                            attempt));
                    }
                }

                return;
            }
            finally
            {
                DestroyPhysicalMonitors(physicalMonitors);
            }
        }

        results.Add(lastFailure ?? Failure(
            logicalMonitor.DeviceName,
            string.Empty,
            vcpCode,
            DdcVcpFeatureStatus.PhysicalMonitorEnumerationFailed,
            ErrorInvalidData,
            MaximumHandleAttempts));
    }

    private static List<LogicalMonitor> EnumerateLogicalMonitors()
    {
        var monitors = new List<LogicalMonitor>();
        DdcPInvoke.MonitorEnumProc callback = (monitor, _, _, _) =>
        {
            unsafe
            {
                var info = new MonitorInfoEx { Size = (uint)sizeof(MonitorInfoEx) };
                if (DdcPInvoke.GetMonitorInfo(monitor, &info))
                {
                    monitors.Add(new LogicalMonitor(monitor, info.GetDeviceName()));
                }
            }

            return true;
        };

        if (!DdcPInvoke.EnumDisplayMonitors(0, 0, callback, 0))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        return monitors;
    }

    private static unsafe PhysicalMonitor[]? AcquirePhysicalMonitors(nint logicalMonitor, out int error)
    {
        error = 0;
        if (!DdcPInvoke.GetNumberOfPhysicalMonitorsFromHMONITOR(logicalMonitor, out var count))
        {
            error = Marshal.GetLastPInvokeError();
            return null;
        }

        if (count is 0 or > MaximumPhysicalMonitorsPerDisplay)
        {
            error = ErrorInvalidData;
            return null;
        }

        var physicalMonitors = new PhysicalMonitor[count];
        fixed (PhysicalMonitor* pointer = physicalMonitors)
        {
            if (!DdcPInvoke.GetPhysicalMonitorsFromHMONITOR(logicalMonitor, count, pointer))
            {
                error = Marshal.GetLastPInvokeError();
                return null;
            }
        }

        return physicalMonitors;
    }

    private static bool TryReadFeature(
        nint handle,
        byte vcpCode,
        out uint current,
        out uint maximum,
        out int error) =>
        TryReadFeature(handle, vcpCode, out current, out maximum, out error, out _);

    private static bool TryReadFeature(
        nint handle,
        byte vcpCode,
        out uint current,
        out uint maximum,
        out int error,
        out int attempts)
    {
        error = 0;
        for (attempts = 1; attempts <= MaximumReadAttempts; attempts++)
        {
            if (DdcPInvoke.GetVCPFeatureAndVCPFeatureReply(handle, vcpCode, 0, out current, out maximum))
            {
                return true;
            }

            error = Marshal.GetLastPInvokeError();
            if (attempts < MaximumReadAttempts)
            {
                Thread.Sleep(ReadRetryDelayMilliseconds);
            }
        }

        attempts = MaximumReadAttempts;
        current = 0;
        maximum = 0;
        return false;
    }

    private static void DestroyPhysicalMonitors(IEnumerable<PhysicalMonitor> monitors)
    {
        foreach (var monitor in monitors)
        {
            if (monitor.Handle != 0)
            {
                _ = DdcPInvoke.DestroyPhysicalMonitor(monitor.Handle);
            }
        }
    }

    private static DdcVcpFeatureResult Failure(
        string gdiDeviceName,
        string description,
        byte vcpCode,
        DdcVcpFeatureStatus status,
        int error,
        int handleAttempts) =>
        new(gdiDeviceName, description, vcpCode, status, 0, 0, error, 0, handleAttempts);

    private static DdcVcpWriteResult WriteFailure(
        byte vcpCode,
        uint requestedValue,
        int error,
        string message,
        uint appliedValue = 0) =>
        new(
            vcpCode,
            DdcVcpWriteStatus.WriteFailed,
            requestedValue,
            appliedValue,
            null,
            ErrorCode: error,
            Message: message);

    private sealed record LogicalMonitor(nint Handle, string DeviceName);
}
