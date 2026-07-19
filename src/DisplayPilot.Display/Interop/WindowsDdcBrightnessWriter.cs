// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using System.ComponentModel;
using System.Runtime.InteropServices;
using DisplayPilot.Display.Brightness;
using DisplayPilot.Display.Discovery;
using DisplayPilot.Display.Models;

namespace DisplayPilot.Display.Interop;

/// <summary>
/// Sets only brightness VCP 0x10 and verifies the result with a read-back.
/// </summary>
public sealed class WindowsDdcBrightnessWriter : IBrightnessWriter
{
    private const int MaximumHandleAttempts = 3;
    private const int MaximumReadAttempts = 3;
    private const int HandleRetryDelayMilliseconds = 200;
    private const int ReadRetryDelayMilliseconds = 75;
    private const int ErrorInvalidData = 13;

    public BrightnessWriteResult WriteBrightness(MonitorDisplayInfo display, int requestedPercent)
    {
        ArgumentNullException.ThrowIfNull(display);
        requestedPercent = Math.Clamp(requestedPercent, 0, 100);

        try
        {
            var logicalMonitor = FindLogicalMonitor(display.GdiDeviceName);
            return logicalMonitor is null
                ? Failed(requestedPercent, ErrorInvalidData, "The active logical monitor was not found.")
                : WriteLogicalMonitor(logicalMonitor, requestedPercent);
        }
        catch (Win32Exception exception)
        {
            return Failed(requestedPercent, exception.NativeErrorCode, exception.Message);
        }
        catch (OverflowException exception)
        {
            return Failed(requestedPercent, ErrorInvalidData, exception.Message);
        }
    }

    private static LogicalMonitor? FindLogicalMonitor(string gdiDeviceName)
    {
        LogicalMonitor? match = null;
        DdcPInvoke.MonitorEnumProc callback = (monitor, _, _, _) =>
        {
            unsafe
            {
                var info = new MonitorInfoEx { Size = (uint)sizeof(MonitorInfoEx) };
                if (DdcPInvoke.GetMonitorInfo(monitor, &info)
                    && string.Equals(info.GetDeviceName(), gdiDeviceName, StringComparison.OrdinalIgnoreCase))
                {
                    match = new LogicalMonitor(monitor);
                    return false;
                }
            }

            return true;
        };

        var enumerationSucceeded = DdcPInvoke.EnumDisplayMonitors(0, 0, callback, 0);
        if (!enumerationSucceeded && match is null)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        return match;
    }

    private static unsafe BrightnessWriteResult WriteLogicalMonitor(
        LogicalMonitor logicalMonitor,
        int requestedPercent)
    {
        var lastError = ErrorInvalidData;
        for (var attempt = 1; attempt <= MaximumHandleAttempts; attempt++)
        {
            if (attempt > 1)
            {
                Thread.Sleep(HandleRetryDelayMilliseconds);
            }

            if (!DdcPInvoke.GetNumberOfPhysicalMonitorsFromHMONITOR(logicalMonitor.Handle, out var count))
            {
                lastError = Marshal.GetLastPInvokeError();
                continue;
            }

            if (count is 0 or > 64)
            {
                lastError = ErrorInvalidData;
                continue;
            }

            var physicalMonitors = new PhysicalMonitor[count];
            var acquired = false;
            fixed (PhysicalMonitor* pointer = physicalMonitors)
            {
                acquired = DdcPInvoke.GetPhysicalMonitorsFromHMONITOR(logicalMonitor.Handle, count, pointer);
                if (!acquired)
                {
                    lastError = Marshal.GetLastPInvokeError();
                }
            }

            if (!acquired)
            {
                continue;
            }

            try
            {
                if (physicalMonitors.Any(monitor => monitor.Handle == 0))
                {
                    lastError = ErrorInvalidData;
                    continue;
                }

                var verifiedPercent = -1;
                foreach (var physicalMonitor in physicalMonitors)
                {
                    var result = WritePhysicalMonitor(physicalMonitor, requestedPercent);
                    if (!result.Succeeded)
                    {
                        return result;
                    }

                    verifiedPercent = result.VerifiedPercent;
                }

                return new BrightnessWriteResult(
                    BrightnessWriteProvider.DdcCi,
                    BrightnessWriteStatus.WriteSucceeded,
                    requestedPercent,
                    requestedPercent,
                    verifiedPercent,
                    Message: $"Set and verified {physicalMonitors.Length} physical monitor handle(s).");
            }
            finally
            {
                foreach (var physicalMonitor in physicalMonitors)
                {
                    if (physicalMonitor.Handle != 0)
                    {
                        _ = DdcPInvoke.DestroyPhysicalMonitor(physicalMonitor.Handle);
                    }
                }
            }
        }

        return Failed(requestedPercent, lastError, "Could not acquire a physical monitor handle.");
    }

    private static BrightnessWriteResult WritePhysicalMonitor(
        PhysicalMonitor physicalMonitor,
        int requestedPercent)
    {
        if (!TryReadBrightness(physicalMonitor.Handle, out _, out var maximum, out var readError)
            || maximum == 0)
        {
            return Failed(requestedPercent, readError, "Could not read the monitor brightness range before writing.");
        }

        var rawValue = (uint)VcpFeatureValue.FromPercentage(requestedPercent, checked((int)maximum));
        if (!DdcPInvoke.SetVCPFeature(physicalMonitor.Handle, NativeConstants.VcpCodeBrightness, rawValue))
        {
            return Failed(requestedPercent, Marshal.GetLastPInvokeError(), "SetVCPFeature rejected brightness VCP 0x10.");
        }

        Thread.Sleep(ReadRetryDelayMilliseconds);
        if (!TryReadBrightness(physicalMonitor.Handle, out var current, out maximum, out readError)
            || maximum == 0)
        {
            return new BrightnessWriteResult(
                BrightnessWriteProvider.DdcCi,
                BrightnessWriteStatus.VerificationFailed,
                requestedPercent,
                requestedPercent,
                -1,
                readError,
                "The write returned success, but brightness read-back failed.");
        }

        var verifiedPercent = new VcpFeatureValue(checked((int)current), checked((int)maximum)).ToPercentage();
        return Math.Abs(verifiedPercent - requestedPercent) <= 1
            ? new BrightnessWriteResult(
                BrightnessWriteProvider.DdcCi,
                BrightnessWriteStatus.WriteSucceeded,
                requestedPercent,
                requestedPercent,
                verifiedPercent)
            : new BrightnessWriteResult(
                BrightnessWriteProvider.DdcCi,
                BrightnessWriteStatus.VerificationFailed,
                requestedPercent,
                requestedPercent,
                verifiedPercent,
                Message: "Brightness read-back did not match the requested percentage.");
    }

    private static bool TryReadBrightness(
        nint handle,
        out uint current,
        out uint maximum,
        out int error)
    {
        error = 0;
        for (var attempt = 1; attempt <= MaximumReadAttempts; attempt++)
        {
            if (DdcPInvoke.GetVCPFeatureAndVCPFeatureReply(
                handle,
                NativeConstants.VcpCodeBrightness,
                0,
                out current,
                out maximum))
            {
                return true;
            }

            error = Marshal.GetLastPInvokeError();
            if (attempt < MaximumReadAttempts)
            {
                Thread.Sleep(ReadRetryDelayMilliseconds);
            }
        }

        current = 0;
        maximum = 0;
        return false;
    }

    private static BrightnessWriteResult Failed(int requestedPercent, int error, string message) =>
        new(
            BrightnessWriteProvider.DdcCi,
            BrightnessWriteStatus.WriteFailed,
            requestedPercent,
            -1,
            -1,
            error,
            message);

    private sealed record LogicalMonitor(nint Handle);
}
