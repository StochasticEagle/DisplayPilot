// SPDX-License-Identifier: Unlicense
// DisplayPilot's display-control labels; numeric codes are defined by VESA MCCS.
namespace DisplayPilot.Display.Mccs;

public static class VcpNames
{
    public static string GetCodeName(byte code) => code switch
    {
        0x10 => "Brightness",
        0x12 => "Contrast",
        0x14 => "Select Color Preset",
        0x60 => "Input Source",
        0x62 => "Audio Speaker Volume",
        0xD6 => "Power Mode",
        _ => $"Unknown (0x{code:X2})"
    };

    public static string? GetValueName(byte code, int value) => (code, value) switch
    {
        (0x14, 0x01) => "sRGB",
        (0x14, 0x02) => "Display Native",
        (0x14, 0x03) => "4000K",
        (0x14, 0x04) => "5000K",
        (0x14, 0x05) => "6500K",
        (0x14, 0x06) => "7500K",
        (0x14, 0x07) => "8200K",
        (0x14, 0x08) => "9300K",
        (0x14, 0x09) => "10000K",
        (0x14, 0x0A) => "11500K",
        (0x14, 0x0B) => "User 1",
        (0x14, 0x0C) => "User 2",
        (0x14, 0x0D) => "User 3",
        (0x60, 0x0F) => "DisplayPort-1",
        (0x60, 0x10) => "DisplayPort-2",
        (0x60, 0x11) => "HDMI-1",
        (0x60, 0x12) => "HDMI-2",
        (0x60, 0x1B) => "USB-C",
        (0xD6, 0x01) => "On",
        (0xD6, 0x02) => "Standby",
        (0xD6, 0x03) => "Suspend",
        (0xD6, 0x04) => "Off (DPM)",
        (0xD6, 0x05) => "Off (Hard)",
        _ => null
    };

    public static string GetFormattedValueName(byte code, int value)
    {
        var name = GetValueName(code, value);
        return name is null ? $"0x{value:X2}" : $"{name} (0x{value:X2})";
    }
}
