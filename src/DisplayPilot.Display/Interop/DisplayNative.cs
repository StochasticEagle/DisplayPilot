using System.Runtime.InteropServices;

namespace DisplayPilot.Display.Interop;

// Only the Windows ABI fields read by DisplayPilot are represented explicitly.
// The reserved regions preserve the documented native structure sizes.
#pragma warning disable CA1815

[StructLayout(LayoutKind.Sequential)]
internal struct DisplayAdapterId
{
    public uint Low;
    public int High;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ActivePathRecord
{
    public DisplayAdapterId SourceAdapter;
    public uint SourceId;
    private fixed byte SourceMetadata[8];
    public DisplayAdapterId TargetAdapter;
    public uint TargetId;
    private fixed byte TargetMetadata[36];
    private uint Flags;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ModeRecord
{
    public uint Kind;
    public uint Id;
    public DisplayAdapterId Adapter;
    private fixed byte Details[48];
}

[StructLayout(LayoutKind.Sequential)]
internal struct DisplayNameHeader
{
    public uint Query;
    public uint Size;
    public DisplayAdapterId Adapter;
    public uint Id;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct SourceDisplayName
{
    public DisplayNameHeader Header;
    private fixed ushort Name[32];

    public readonly string GetName()
    {
        fixed (ushort* name = Name)
            return new string((char*)name);
    }
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct TargetDisplayName
{
    public DisplayNameHeader Header;
    private fixed byte TargetMetadata[16];
    private fixed ushort FriendlyName[64];
    private fixed ushort Path[128];

    public readonly string GetFriendlyName()
    {
        fixed (ushort* name = FriendlyName)
            return new string((char*)name);
    }

    public readonly string GetPath()
    {
        fixed (ushort* path = Path)
            return new string((char*)path);
    }
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct DisplayMonitorInfo
{
    public uint Size;
    private fixed byte GeometryAndFlags[36];
    private fixed ushort Device[32];

    public readonly string GetDeviceName()
    {
        fixed (ushort* device = Device)
            return new string((char*)device);
    }
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct DdcMonitorHandle
{
    public nint Handle;
    private fixed ushort Description[128];

    public readonly string GetDescription()
    {
        fixed (ushort* description = Description)
            return new string((char*)description);
    }
}

#pragma warning restore CA1815

// The OS entry point names and native calling conventions are fixed by Win32.
// No background worker or stateful abstraction is required.
internal static partial class DisplayNative
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate bool MonitorCallback(nint monitor, nint dc, nint rect, nint context);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EnumDisplayMonitors(
        nint dc, nint rect, MonitorCallback callback, nint context);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static unsafe partial bool GetMonitorInfo(nint monitor, DisplayMonitorInfo* data);

    [LibraryImport("user32.dll")]
    internal static partial int GetDisplayConfigBufferSizes(
        uint flags, out uint pathCount, out uint modeCount);

    [LibraryImport("user32.dll")]
    internal static unsafe partial int QueryDisplayConfig(
        uint flags, ref uint pathCount, ActivePathRecord* paths,
        ref uint modeCount, ModeRecord* modes, nint topology);

    [LibraryImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")]
    internal static unsafe partial int ReadDisplayName(SourceDisplayName* name);

    [LibraryImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")]
    internal static unsafe partial int ReadDisplayName(TargetDisplayName* name);

    [LibraryImport("Dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetNumberOfPhysicalMonitorsFromHMONITOR(nint monitor, out uint count);

    [LibraryImport("Dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static unsafe partial bool GetPhysicalMonitorsFromHMONITOR(
        nint monitor, uint count, DdcMonitorHandle* handles);

    [LibraryImport("Dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DestroyPhysicalMonitor(nint handle);

    [LibraryImport("Dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetVCPFeatureAndVCPFeatureReply(
        nint handle, byte code, nint type, out uint current, out uint maximum);

    [LibraryImport("Dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetVCPFeature(nint handle, byte code, uint value);

    [LibraryImport("Dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetCapabilitiesStringLength(nint handle, out uint length);

    [LibraryImport("Dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static unsafe partial bool CapabilitiesRequestAndCapabilitiesReply(
        nint handle, byte* buffer, uint length);
}
