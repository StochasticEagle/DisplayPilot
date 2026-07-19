// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace DisplayPilot.Display.Interop;

internal static partial class DdcPInvoke
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate bool MonitorEnumProc(nint monitor, nint monitorDc, nint monitorRect, nint data);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EnumDisplayMonitors(
        nint deviceContext,
        nint clipRect,
        MonitorEnumProc callback,
        nint data);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static unsafe partial bool GetMonitorInfo(nint monitor, MonitorInfoEx* monitorInfo);

    [LibraryImport("Dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetNumberOfPhysicalMonitorsFromHMONITOR(
        nint monitor,
        out uint physicalMonitorCount);

    [LibraryImport("Dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static unsafe partial bool GetPhysicalMonitorsFromHMONITOR(
        nint monitor,
        uint physicalMonitorArraySize,
        PhysicalMonitor* physicalMonitors);

    [LibraryImport("Dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DestroyPhysicalMonitor(nint physicalMonitor);

    [LibraryImport("Dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetVCPFeatureAndVCPFeatureReply(
        nint physicalMonitor,
        byte vcpCode,
        nint vcpCodeType,
        out uint currentValue,
        out uint maximumValue);

    [LibraryImport("Dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetVCPFeature(
        nint physicalMonitor,
        byte vcpCode,
        uint newValue);
}
