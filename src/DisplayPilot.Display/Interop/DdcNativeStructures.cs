// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
