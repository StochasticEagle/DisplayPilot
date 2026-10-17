namespace DisplayPilot.Display.Models;

[Flags]
public enum DisplayControlSupport
{
    None = 0,
    Brightness = 1,
    Contrast = 2,
    DdcCi = 4,
    Wmi = 8,
    Hdr = 16,
    HighLevel = 32,
    Volume = 64
}
