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

    /// <summary>Read the display's supported controls through one physical-handle acquisition.</summary>
    public static DdcControlSnapshot ReadSnapshot(
        IReadOnlyList<MonitorDisplayInfo> displays,
        IReadOnlyList<MonitorDdcCapabilitiesInfo>? previousCapabilities = null)
    {
        ArgumentNullException.ThrowIfNull(displays);
        var monitors = displays.Count == 0 ? [] : EnumerateLogicalMonitors();
        var brightness = new List<MonitorDdcProbeInfo>(displays.Count);
        var contrast = new List<MonitorDdcVcpFeatureInfo>(displays.Count);
        var temperature = new List<MonitorDdcVcpFeatureInfo>(displays.Count);
        var capabilities = new List<MonitorDdcCapabilitiesInfo>(displays.Count);

        foreach (var display in displays)
        {
            var bright = new List<DdcVcpFeatureResult>();
            var dark = new List<DdcVcpFeatureResult>();
            var colors = new List<DdcVcpFeatureResult>();
            var known = previousCapabilities?.FirstOrDefault(item =>
                string.Equals(item.Display.DevicePath, display.DevicePath, StringComparison.OrdinalIgnoreCase));
            var capability = known;
            var logical = monitors.FirstOrDefault(item =>
                string.Equals(item.DeviceName, display.GdiDeviceName, StringComparison.OrdinalIgnoreCase));
            var lastError = ErrorInvalidData;
            if (logical is not null)
            {
                for (int attempt = 1; attempt <= MaximumHandleAttempts; attempt++)
                {
                    if (attempt > 1) Thread.Sleep(HandleRetryDelayMilliseconds);
                    var physical = AcquirePhysicalMonitors(logical.Handle, out lastError);
                    if (physical is null) continue;
                    try
                    {
                        foreach (var item in physical)
                        {
                            bright.Add(ReadSnapshotFeature(display.GdiDeviceName, item,
                                NativeConstants.VcpCodeBrightness, attempt));
                            dark.Add(ReadSnapshotFeature(display.GdiDeviceName, item,
                                NativeConstants.VcpCodeContrast, attempt));
                            colors.Add(ReadSnapshotFeature(display.GdiDeviceName, item,
                                NativeConstants.VcpCodeSelectColorPreset, attempt));
                            if (capability is null || !capability.Succeeded)
                            {
                                if (TrySnapshotCapabilities(item.Handle, out var raw, out var parsed, out var error))
                                    capability = new(display, true, raw, parsed);
                                else
                                    lastError = error;
                            }
                        }
                    }
                    finally
                    {
                        DestroyPhysicalMonitors(physical);
                    }
                    break;
                }
            }

            if (bright.Count == 0)
            {
                var status = logical is null ? DdcVcpFeatureStatus.NoPhysicalMonitor :
                    DdcVcpFeatureStatus.PhysicalMonitorEnumerationFailed;
                bright.Add(Failure(display.GdiDeviceName, "", NativeConstants.VcpCodeBrightness, status, lastError, 0));
                dark.Add(Failure(display.GdiDeviceName, "", NativeConstants.VcpCodeContrast, status, lastError, 0));
                colors.Add(Failure(display.GdiDeviceName, "", NativeConstants.VcpCodeSelectColorPreset, status, lastError, 0));
            }
            brightness.Add(new(display, bright.Select(item => new DdcBrightnessProbeResult(
                item.GdiDeviceName, item.PhysicalMonitorDescription,
                (DdcBrightnessProbeStatus)(int)item.Status, item.CurrentValue, item.MaximumValue,
                item.Win32Error, item.AttemptCount, item.HandleAcquisitionAttempts)).ToArray()));
            contrast.Add(new(display, dark));
            temperature.Add(new(display, colors));
            capabilities.Add(capability ?? new(display, false, "", new MccsDisplayProfile(), lastError));
        }
        return new(brightness, contrast, temperature, capabilities);
    }

    private static DdcVcpFeatureResult ReadSnapshotFeature(
        string device, DdcMonitorHandle item, byte code, int attempt)
    {
        var description = item.GetDescription();
        if (item.Handle == 0)
            return Failure(device, description, code, DdcVcpFeatureStatus.PhysicalMonitorHandleUnavailable, ErrorInvalidData, attempt);

        var worked = TryReadFeature(item.Handle, code, out var current, out var maximum, out var error, out var reads);
        return new(device, description, code,
            worked ? DdcVcpFeatureStatus.ReadSucceeded : DdcVcpFeatureStatus.ReadFailed,
            current, maximum, error, reads, attempt);
    }

    private static unsafe bool TrySnapshotCapabilities(
        nint handle, out string raw, out MccsDisplayProfile parsed, out int error)
    {
        raw = "";
        parsed = new MccsDisplayProfile();
        error = ErrorInvalidData;
        if (handle == 0) return false;
        if (!DisplayNative.GetCapabilitiesStringLength(handle, out var size))
        {
            error = Marshal.GetLastPInvokeError();
            return false;
        }
        if (size is 0 or > 65536) return false;
        var buffer = new byte[size];
        fixed (byte* address = buffer)
        {
            if (!DisplayNative.CapabilitiesRequestAndCapabilitiesReply(handle, address, size))
            {
                error = Marshal.GetLastPInvokeError();
                return false;
            }
        }
        raw = Encoding.ASCII.GetString(buffer).TrimEnd('\0');
        parsed = MccsCapabilitiesParser.Parse(raw).Capabilities;
        error = 0;
        return true;
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
                            ? (uint)DisplayLevelScale.FromPercent(checked((int)requestedValue), checked((int)maximum))
                            : requestedValue;
                        if (!DisplayNative.SetVCPFeature(physicalMonitor.Handle, vcpCode, appliedRaw))
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

                            verifiedPercent = DisplayLevelScale.ToPercent(checked((int)current), checked((int)maximum));
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

    private static List<LogicalMonitor> EnumerateLogicalMonitors()
    {
        var monitors = new List<LogicalMonitor>();
        DisplayNative.MonitorCallback callback = (monitor, _, _, _) =>
        {
            unsafe
            {
                var info = new DisplayMonitorInfo { Size = (uint)sizeof(DisplayMonitorInfo) };
                if (DisplayNative.GetMonitorInfo(monitor, &info))
                {
                    monitors.Add(new LogicalMonitor(monitor, info.GetDeviceName()));
                }
            }

            return true;
        };

        if (!DisplayNative.EnumDisplayMonitors(0, 0, callback, 0))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        return monitors;
    }

    private static unsafe DdcMonitorHandle[]? AcquirePhysicalMonitors(nint logicalMonitor, out int error)
    {
        error = 0;
        if (!DisplayNative.GetNumberOfPhysicalMonitorsFromHMONITOR(logicalMonitor, out var count))
        {
            error = Marshal.GetLastPInvokeError();
            return null;
        }

        if (count is 0 or > MaximumPhysicalMonitorsPerDisplay)
        {
            error = ErrorInvalidData;
            return null;
        }

        var physicalMonitors = new DdcMonitorHandle[count];
        fixed (DdcMonitorHandle* pointer = physicalMonitors)
        {
            if (!DisplayNative.GetPhysicalMonitorsFromHMONITOR(logicalMonitor, count, pointer))
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
            if (DisplayNative.GetVCPFeatureAndVCPFeatureReply(handle, vcpCode, 0, out current, out maximum))
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

    private static void DestroyPhysicalMonitors(IEnumerable<DdcMonitorHandle> monitors)
    {
        foreach (var monitor in monitors)
        {
            if (monitor.Handle != 0)
            {
                _ = DisplayNative.DestroyPhysicalMonitor(monitor.Handle);
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
