// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using System.Management;
using System.Runtime.InteropServices;
using DisplayPilot.Display.Brightness;
using DisplayPilot.Display.Discovery;
using DisplayPilot.Display.Models;

namespace DisplayPilot.Display.Interop;

/// <summary>
/// Invokes only <c>WmiSetBrightness</c> for the WMI instance matching one display,
/// then verifies the value through <c>WmiMonitorBrightness</c>.
/// </summary>
public sealed class WindowsWmiBrightnessWriter : IBrightnessWriter
{
    private const string Scope = @"root\WMI";
    private const string Query = "SELECT InstanceName FROM WmiMonitorBrightnessMethods";
    private const int MaximumVerificationAttempts = 3;
    private const int VerificationDelayMilliseconds = 100;
    private const int ErrorNotFound = unchecked((int)0x80041002);
    private readonly WindowsWmiBrightnessApi _brightnessApi = new();

    public BrightnessWriteResult WriteBrightness(MonitorDisplayInfo display, int requestedPercent)
    {
        ArgumentNullException.ThrowIfNull(display);
        requestedPercent = Math.Clamp(requestedPercent, 0, 100);

        var displayId = MonitorIdentity.FromDevicePath(display.DevicePath);
        var brightnessQuery = _brightnessApi.QueryBrightness();
        if (!brightnessQuery.Succeeded)
        {
            return Failed(requestedPercent, brightnessQuery.ErrorCode, brightnessQuery.ErrorMessage);
        }

        var brightnessInstance = brightnessQuery.Instances.FirstOrDefault(instance =>
            instance.Active
            && string.Equals(
                MonitorIdentity.FromInstanceName(instance.InstanceName),
                displayId,
                StringComparison.OrdinalIgnoreCase));
        if (brightnessInstance is null)
        {
            return Failed(requestedPercent, ErrorNotFound, "No active WMI brightness instance matched this display.");
        }

        var appliedPercent = NearestSupportedLevel(requestedPercent, brightnessInstance.Levels);
        try
        {
            using var searcher = new ManagementObjectSearcher(Scope, Query);
            using var methods = searcher.Get();
            foreach (ManagementObject method in methods)
            {
                using (method)
                {
                    if (!string.Equals(
                        MonitorIdentity.FromInstanceName(method["InstanceName"] as string),
                        displayId,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    using var parameters = method.GetMethodParameters("WmiSetBrightness");
                    parameters["Timeout"] = 0u;
                    parameters["Brightness"] = checked((byte)appliedPercent);
                    using var output = method.InvokeMethod("WmiSetBrightness", parameters, null);
                    var returnValue = output?["ReturnValue"] is uint value ? value : 0u;
                    if (returnValue != 0)
                    {
                        return Failed(
                            requestedPercent,
                            unchecked((int)returnValue),
                            "WmiSetBrightness returned a failure code.",
                            appliedPercent);
                    }

                    return Verify(displayId, requestedPercent, appliedPercent);
                }
            }

            return Failed(requestedPercent, ErrorNotFound, "No WMI brightness-method instance matched this display.");
        }
        catch (ManagementException exception)
        {
            return Failed(requestedPercent, exception.HResult, exception.Message, appliedPercent);
        }
        catch (COMException exception)
        {
            return Failed(requestedPercent, exception.HResult, exception.Message, appliedPercent);
        }
        catch (UnauthorizedAccessException exception)
        {
            return Failed(requestedPercent, exception.HResult, exception.Message, appliedPercent);
        }
    }

    private BrightnessWriteResult Verify(string displayId, int requestedPercent, int appliedPercent)
    {
        var lastError = 0;
        for (var attempt = 1; attempt <= MaximumVerificationAttempts; attempt++)
        {
            Thread.Sleep(VerificationDelayMilliseconds);
            var query = _brightnessApi.QueryBrightness();
            if (!query.Succeeded)
            {
                lastError = query.ErrorCode;
                continue;
            }

            var instance = query.Instances.FirstOrDefault(candidate =>
                candidate.Active
                && string.Equals(
                    MonitorIdentity.FromInstanceName(candidate.InstanceName),
                    displayId,
                    StringComparison.OrdinalIgnoreCase));
            if (instance?.CurrentBrightness == appliedPercent)
            {
                return new BrightnessWriteResult(
                    BrightnessWriteProvider.Wmi,
                    BrightnessWriteStatus.WriteSucceeded,
                    requestedPercent,
                    appliedPercent,
                    instance.CurrentBrightness,
                    Message: "WmiSetBrightness succeeded and read-back matched.");
            }
        }

        return new BrightnessWriteResult(
            BrightnessWriteProvider.Wmi,
            BrightnessWriteStatus.VerificationFailed,
            requestedPercent,
            appliedPercent,
            -1,
            lastError,
            "WmiSetBrightness returned success, but read-back did not match.");
    }

    private static int NearestSupportedLevel(int requestedPercent, IReadOnlyList<byte> levels)
    {
        return levels.Count == 0
            ? requestedPercent
            : levels.MinBy(level => Math.Abs(level - requestedPercent));
    }

    private static BrightnessWriteResult Failed(
        int requestedPercent,
        int error,
        string message,
        int appliedPercent = -1) =>
        new(
            BrightnessWriteProvider.Wmi,
            BrightnessWriteStatus.WriteFailed,
            requestedPercent,
            appliedPercent,
            -1,
            error,
            message);
}
