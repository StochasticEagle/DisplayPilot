// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using System.Runtime.InteropServices;

namespace DisplayPilot.Display.Interop;

#pragma warning disable CA1815

[StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode, Size = 220)]
internal unsafe struct DevMode
{
    [FieldOffset(0)]
    public fixed char DeviceName[32];

    [FieldOffset(64)]
    public ushort SpecVersion;

    [FieldOffset(66)]
    public ushort DriverVersion;

    [FieldOffset(68)]
    public ushort Size;

    [FieldOffset(70)]
    public ushort DriverExtra;

    [FieldOffset(72)]
    public uint Fields;

    [FieldOffset(76)]
    public int PositionX;

    [FieldOffset(80)]
    public int PositionY;

    [FieldOffset(84)]
    public uint DisplayOrientation;

    [FieldOffset(88)]
    public uint DisplayFixedOutput;

    [FieldOffset(92)]
    public short Color;

    [FieldOffset(94)]
    public short Duplex;

    [FieldOffset(96)]
    public short YResolution;

    [FieldOffset(98)]
    public short TtOption;

    [FieldOffset(100)]
    public short Collate;

    [FieldOffset(102)]
    public fixed char FormName[32];

    [FieldOffset(166)]
    public ushort LogPixels;

    [FieldOffset(168)]
    public uint BitsPerPel;

    [FieldOffset(172)]
    public uint PelsWidth;

    [FieldOffset(176)]
    public uint PelsHeight;

    [FieldOffset(180)]
    public uint DisplayFlags;

    [FieldOffset(184)]
    public uint DisplayFrequency;

    [FieldOffset(188)]
    public uint IcmMethod;

    [FieldOffset(192)]
    public uint IcmIntent;

    [FieldOffset(196)]
    public uint MediaType;

    [FieldOffset(200)]
    public uint DitherType;

    [FieldOffset(204)]
    public uint Reserved1;

    [FieldOffset(208)]
    public uint Reserved2;

    [FieldOffset(212)]
    public uint PanningWidth;

    [FieldOffset(216)]
    public uint PanningHeight;
}

#pragma warning restore CA1815
