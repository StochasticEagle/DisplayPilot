// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

        DdcPInvoke.MonitorEnumProc callback = (monitor, _, _, _) =>
        {
            unsafe
            {
                var info = new MonitorInfoEx
                {
                    Size = (uint)sizeof(MonitorInfoEx),
                };

                if (!DdcPInvoke.GetMonitorInfo(monitor, &info))
                {
                    return true;
                }

                monitors.Add(new LogicalMonitor(monitor, info.GetDeviceName()));
                return true;
            }
        };

        if (!DdcPInvoke.EnumDisplayMonitors(0, 0, callback, 0))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        return monitors;
    }

    private static unsafe void ProbeLogicalMonitor(
        LogicalMonitor logicalMonitor,
        List<DdcBrightnessProbeResult> results)
    {
        if (!DdcPInvoke.GetNumberOfPhysicalMonitorsFromHMONITOR(logicalMonitor.Handle, out var count))
        {
            results.Add(FailedEnumeration(logicalMonitor.DeviceName, Marshal.GetLastPInvokeError()));
            return;
        }

        if (count == 0)
        {
            results.Add(NoPhysicalMonitor(logicalMonitor.DeviceName));
            return;
        }

        if (count > MaximumPhysicalMonitorsPerDisplay)
        {
            results.Add(FailedEnumeration(logicalMonitor.DeviceName, ErrorInvalidData));
            return;
        }

        var physicalMonitors = new PhysicalMonitor[count];
        fixed (PhysicalMonitor* physicalMonitorPointer = physicalMonitors)
        {
            if (!DdcPInvoke.GetPhysicalMonitorsFromHMONITOR(
                    logicalMonitor.Handle,
                    count,
                    physicalMonitorPointer))
            {
                results.Add(FailedEnumeration(logicalMonitor.DeviceName, Marshal.GetLastPInvokeError()));
                return;
            }

            try
            {
                foreach (var physicalMonitor in physicalMonitors)
                {
                    results.Add(ProbePhysicalMonitor(logicalMonitor.DeviceName, physicalMonitor));
                }
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
    }

    private static DdcBrightnessProbeResult ProbePhysicalMonitor(
        string gdiDeviceName,
        PhysicalMonitor physicalMonitor)
    {
        var description = physicalMonitor.GetDescription();
        if (physicalMonitor.Handle != 0
            && DdcPInvoke.GetVCPFeatureAndVCPFeatureReply(
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
                0);
        }

        return new DdcBrightnessProbeResult(
            gdiDeviceName,
            description,
            DdcBrightnessProbeStatus.ReadFailed,
            0,
            0,
            physicalMonitor.Handle == 0 ? ErrorInvalidData : Marshal.GetLastPInvokeError());
    }

    private static DdcBrightnessProbeResult NoPhysicalMonitor(string gdiDeviceName)
    {
        return new DdcBrightnessProbeResult(
            gdiDeviceName,
            string.Empty,
            DdcBrightnessProbeStatus.NoPhysicalMonitor,
            0,
            0,
            0);
    }

    private static DdcBrightnessProbeResult FailedEnumeration(string gdiDeviceName, int error)
    {
        return new DdcBrightnessProbeResult(
            gdiDeviceName,
            string.Empty,
            DdcBrightnessProbeStatus.PhysicalMonitorEnumerationFailed,
            0,
            0,
            error);
    }

    private sealed record LogicalMonitor(nint Handle, string DeviceName);
}
