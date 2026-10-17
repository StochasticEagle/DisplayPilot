namespace DisplayPilot.Display.Models;

public sealed class DisplayIdentity
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string GdiDeviceName { get; set; } = "";
    public string InstanceName { get; set; } = "";
    public int MonitorNumber { get; set; }
}

public sealed class DisplayHardware
{
    public nint Handle { get; set; }
    public string CommunicationMethod { get; set; } = "";
    public MonitorCapabilities Capabilities { get; set; }
    public MonitorReadFlags ReadValues { get; set; }
    public string? CapabilitiesRaw { get; set; }
    public VcpCapabilities? VcpCapabilitiesInfo { get; set; }
    public int BrightnessVcpMax { get; set; } = 100;
    public int ContrastVcpMax { get; set; } = 100;
    public int VolumeVcpMax { get; set; } = 100;
    public bool SupportsColorTemperature { get; set; }
}

public sealed class DisplayValues
{
    internal int Brightness;
    internal int Contrast = 50;
    internal int Volume = 50;
    internal int ColorTemperature = 0x05;
    internal int InputSource;
    internal int PowerState = 0x01;
    internal int Orientation;
}
