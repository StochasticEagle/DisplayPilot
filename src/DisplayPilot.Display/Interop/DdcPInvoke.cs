// Copyright (c) Microsoft Corporation
// PowerToys-derived interoperability declarations; MIT terms retained.
using System.Runtime.InteropServices;
namespace DisplayPilot.Display.Interop;

#pragma warning disable CA1815 // These structs mirror Win32 ABI layouts and are not domain value types.

[StructLayout(LayoutKind.Sequential)]
internal struct NativeRect
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal unsafe struct MonitorInfoEx
{
    public uint Size;
    public NativeRect MonitorArea;
    public NativeRect WorkArea;
    public uint Flags;
    public fixed ushort DeviceName[32];

    public readonly string GetDeviceName()
    {
        fixed (ushort* value = DeviceName)
        {
            return new string((char*)value);
        }
    }
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal unsafe struct PhysicalMonitor
{
    public nint Handle;
    public fixed ushort Description[128];

    public readonly string GetDescription()
    {
        fixed (ushort* value = Description)
        {
            return new string((char*)value);
        }
    }
}

#pragma warning restore CA1815


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

    [LibraryImport("Dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetCapabilitiesStringLength(
        nint physicalMonitor,
        out uint capabilitiesStringLength);

    [LibraryImport("Dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static unsafe partial bool CapabilitiesRequestAndCapabilitiesReply(
        nint physicalMonitor,
        byte* capabilitiesString,
        uint capabilitiesStringLength);
}
