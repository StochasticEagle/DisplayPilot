// SPDX-License-Identifier: Unlicense
// Windows and VESA-defined constants used by DisplayPilot.
namespace DisplayPilot.Display.Interop;

public static class DisplayInteropConstants
{
    public const byte VcpCodeBrightness = 0x10;
    public const byte VcpCodeContrast = 0x12;
    public const byte VcpCodeVolume = 0x62;
    public const byte VcpCodeSelectColorPreset = 0x14;
    public const byte VcpCodeInputSource = 0x60;
    public const byte VcpCodePowerMode = 0xD6;
    public const uint QdcOnlyActivePaths = 0x00000002;
    public const uint DisplayconfigDeviceInfoGetSourceName = 1;
    public const uint DisplayconfigDeviceInfoGetTargetName = 2;
    public const int EnumCurrentSettings = -1;
    public const int DmdoDefault = 0;
    public const int Dmdo90 = 1;
    public const int Dmdo180 = 2;
    public const int Dmdo270 = 3;
    public const int DmDisplayOrientation = 0x00000080;
    public const int DmPelsWidth = 0x00080000;
    public const int DmPelsHeight = 0x00100000;
    public const uint CdsTest = 0x00000002;
    public const uint CdsUpdateRegistry = 0x00000001;
    public const int DispChangeSuccessful = 0;
    public const int DispChangeRestart = 1;
    public const int DispChangeFailed = -1;
    public const int DispChangeBadmode = -2;
    public const int DispChangeNotupdated = -3;
    public const int DispChangeBadflags = -4;
    public const int DispChangeBadparam = -5;
}
