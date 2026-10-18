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
    private const int ErrorInvalidData = 13;

    public BrightnessWriteResult WriteBrightness(MonitorDisplayInfo display, int requestedPercent)
    {
        ArgumentNullException.ThrowIfNull(display);
        requestedPercent = Math.Clamp(requestedPercent, 0, 100);

        try
        {
            var logicalMonitor = DisplayNative.FindLogicalMonitor(display.GdiDeviceName);
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

    private static BrightnessWriteResult WriteLogicalMonitor(
        LogicalDisplayMonitor logicalMonitor, int requestedPercent)
    {
        var lastError = DisplayNative.InvalidDataError;
        for (int attempt = 1; attempt <= DisplayNative.HandleAttempts; attempt++)
        {
            if (attempt > 1) Thread.Sleep(DisplayNative.HandleRetryMilliseconds);
            var physicalMonitors = DisplayNative.AcquirePhysicalMonitors(
                logicalMonitor.Handle, out _, out lastError);
            if (physicalMonitors is null) continue;

            try
            {
                if (physicalMonitors.Any(monitor => monitor.Handle == 0))
                {
                    lastError = DisplayNative.InvalidDataError;
                    continue;
                }

                var verifiedPercent = -1;
                foreach (var monitor in physicalMonitors)
                {
                    var result = WritePhysicalMonitor(monitor, requestedPercent);
                    if (!result.Succeeded) return result;
                    verifiedPercent = result.VerifiedPercent;
                }

                return new BrightnessWriteResult(
                    BrightnessWriteProvider.DdcCi,
                    BrightnessWriteStatus.WriteSucceeded,
                    requestedPercent, requestedPercent, verifiedPercent,
                    Message: $"Set and verified {physicalMonitors.Length} physical monitor handle(s).");
            }
            finally
            {
                DisplayNative.ReleasePhysicalMonitors(physicalMonitors);
            }
        }

        return Failed(requestedPercent, lastError, "Could not acquire a physical monitor handle.");
    }

    private static BrightnessWriteResult WritePhysicalMonitor(
        DdcMonitorHandle physicalMonitor,
        int requestedPercent)
    {
        if (!DisplayNative.TryReadVcp(physicalMonitor.Handle, DisplayInteropConstants.VcpCodeBrightness, out _, out var maximum, out var readError)
            || maximum == 0)
        {
            return Failed(requestedPercent, readError, "Could not read the monitor brightness range before writing.");
        }

        var rawValue = (uint)DisplayLevelScale.FromPercent(requestedPercent, checked((int)maximum));
        if (!DisplayNative.SetVCPFeature(physicalMonitor.Handle, DisplayInteropConstants.VcpCodeBrightness, rawValue))
        {
            return Failed(requestedPercent, Marshal.GetLastPInvokeError(), "SetVCPFeature rejected brightness VCP 0x10.");
        }

        Thread.Sleep(DisplayNative.ReadRetryMilliseconds);
        if (!DisplayNative.TryReadVcp(physicalMonitor.Handle, DisplayInteropConstants.VcpCodeBrightness, out var current, out maximum, out readError)
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

        var verifiedPercent = DisplayLevelScale.ToPercent(checked((int)current), checked((int)maximum));
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

    private static BrightnessWriteResult Failed(int requestedPercent, int error, string message) =>
        new(
            BrightnessWriteProvider.DdcCi,
            BrightnessWriteStatus.WriteFailed,
            requestedPercent,
            -1,
            -1,
            error,
            message);

}
