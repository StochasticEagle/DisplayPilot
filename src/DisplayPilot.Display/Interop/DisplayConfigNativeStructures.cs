// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace DisplayPilot.Display.Interop;

#pragma warning disable CA1815 // These structs mirror Win32 ABI layouts and are not domain value types.

[StructLayout(LayoutKind.Sequential)]
internal struct Luid
{
    public uint LowPart;
    public int HighPart;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigRational
{
    public uint Numerator;
    public uint Denominator;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigPathSourceInfo
{
    public Luid AdapterId;
    public uint Id;
    public uint ModeInfoIndex;
    public uint StatusFlags;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigPathTargetInfo
{
    public Luid AdapterId;
    public uint Id;
    public uint ModeInfoIndex;
    public uint OutputTechnology;
    public uint Rotation;
    public uint Scaling;
    public DisplayConfigRational RefreshRate;
    public uint ScanLineOrdering;
    public uint TargetAvailable;
    public uint StatusFlags;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigPathInfo
{
    public DisplayConfigPathSourceInfo SourceInfo;
    public DisplayConfigPathTargetInfo TargetInfo;
    public uint Flags;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfig2DRegion
{
    public uint Width;
    public uint Height;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigPoint
{
    public int X;
    public int Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigSourceMode
{
    public uint Width;
    public uint Height;
    public uint PixelFormat;
    public DisplayConfigPoint Position;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigVideoSignalInfo
{
    public ulong PixelRate;
    public DisplayConfigRational HorizontalSyncFrequency;
    public DisplayConfigRational VerticalSyncFrequency;
    public DisplayConfig2DRegion ActiveSize;
    public DisplayConfig2DRegion TotalSize;
    public uint VideoStandard;
    public uint ScanLineOrdering;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigTargetMode
{
    public DisplayConfigVideoSignalInfo TargetVideoSignalInfo;
}

[StructLayout(LayoutKind.Explicit)]
internal struct DisplayConfigModeInfoUnion
{
    [FieldOffset(0)]
    public DisplayConfigTargetMode TargetMode;

    [FieldOffset(0)]
    public DisplayConfigSourceMode SourceMode;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigModeInfo
{
    public uint InfoType;
    public uint Id;
    public Luid AdapterId;
    public DisplayConfigModeInfoUnion ModeInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigDeviceInfoHeader
{
    public uint Type;
    public uint Size;
    public Luid AdapterId;
    public uint Id;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal unsafe struct DisplayConfigSourceDeviceName
{
    public DisplayConfigDeviceInfoHeader Header;
    public fixed ushort ViewGdiDeviceName[32];

    public readonly string GetViewGdiDeviceName()
    {
        fixed (ushort* value = ViewGdiDeviceName)
        {
            return new string((char*)value);
        }
    }
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal unsafe struct DisplayConfigTargetDeviceName
{
    public DisplayConfigDeviceInfoHeader Header;
    public uint Flags;
    public uint OutputTechnology;
    public ushort EdidManufactureId;
    public ushort EdidProductCodeId;
    public uint ConnectorInstance;
    public fixed ushort MonitorFriendlyDeviceName[64];
    public fixed ushort MonitorDevicePath[128];

    public readonly string GetMonitorFriendlyDeviceName()
    {
        fixed (ushort* value = MonitorFriendlyDeviceName)
        {
            return new string((char*)value);
        }
    }

    public readonly string GetMonitorDevicePath()
    {
        fixed (ushort* value = MonitorDevicePath)
        {
            return new string((char*)value);
        }
    }
}

#pragma warning restore CA1815
