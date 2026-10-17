// DisplayPilot monitor view model.
// Microsoft-derived historical source remains attributed in repository notices.
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DisplayPilot.Display.Interfaces;
using DisplayPilot.Display.Mccs;

namespace DisplayPilot.Display.Models;

public sealed class Monitor : INotifyPropertyChanged, IMonitorData
{
    public DisplayIdentity Identity { get; } = new();
    public DisplayHardware Hardware { get; } = new();
    public DisplayValues Values { get; } = new();

    public string Id { get => Identity.Id; set => Identity.Id = value; }
    public string Name { get => Identity.Name; set => Identity.Name = value; }
    public string GdiDeviceName { get => Identity.GdiDeviceName; set => Identity.GdiDeviceName = value; }
    public string InstanceName { get => Identity.InstanceName; set => Identity.InstanceName = value; }
    public int MonitorNumber { get => Identity.MonitorNumber; set => Identity.MonitorNumber = value; }

    public nint Handle { get => Hardware.Handle; set => Hardware.Handle = value; }
    public string CommunicationMethod { get => Hardware.CommunicationMethod; set => Hardware.CommunicationMethod = value; }
    public MonitorCapabilities Capabilities { get => Hardware.Capabilities; set => Hardware.Capabilities = value; }
    public MonitorReadFlags ReadValues { get => Hardware.ReadValues; set => Hardware.ReadValues = value; }
    public string? CapabilitiesRaw { get => Hardware.CapabilitiesRaw; set => Hardware.CapabilitiesRaw = value; }
    public VcpCapabilities? VcpCapabilitiesInfo { get => Hardware.VcpCapabilitiesInfo; set => Hardware.VcpCapabilitiesInfo = value; }
    public int BrightnessVcpMax { get => Hardware.BrightnessVcpMax; set => Hardware.BrightnessVcpMax = value; }
    public int ContrastVcpMax { get => Hardware.ContrastVcpMax; set => Hardware.ContrastVcpMax = value; }
    public int VolumeVcpMax { get => Hardware.VolumeVcpMax; set => Hardware.VolumeVcpMax = value; }
    public bool SupportsColorTemperature { get => Hardware.SupportsColorTemperature; set => Hardware.SupportsColorTemperature = value; }
    public bool SupportsInputSource => VcpCapabilitiesInfo?.SupportsVcpCode(0x60) == true;
    public IReadOnlyList<int>? SupportedInputSources => VcpCapabilitiesInfo?.GetSupportedValues(0x60);
    public bool SupportsPowerState => VcpCapabilitiesInfo?.SupportsVcpCode(0xD6) == true;
    public IReadOnlyList<int>? SupportedPowerStates => VcpCapabilitiesInfo?.GetSupportedValues(0xD6);
    public bool SupportsBrightness => Capabilities.HasFlag(MonitorCapabilities.Brightness);
    public bool SupportsContrast => Capabilities.HasFlag(MonitorCapabilities.Contrast);
    public bool SupportsVolume => Capabilities.HasFlag(MonitorCapabilities.Volume);

    public int CurrentBrightness
    {
        get => Values.Brightness;
        set => Change(ref Values.Brightness, Math.Clamp(value, 0, 100));
    }
    public int CurrentContrast
    {
        get => Values.Contrast;
        set => Change(ref Values.Contrast, Math.Clamp(value, 0, 100));
    }
    public int CurrentVolume
    {
        get => Values.Volume;
        set => Change(ref Values.Volume, Math.Clamp(value, 0, 100));
    }
    public int CurrentColorTemperature
    {
        get => Values.ColorTemperature;
        set
        {
            if (Change(ref Values.ColorTemperature, value))
                Notify(nameof(ColorTemperaturePresetName));
        }
    }
    public int CurrentInputSource
    {
        get => Values.InputSource;
        set
        {
            if (Change(ref Values.InputSource, value))
                Notify(nameof(InputSourceName));
        }
    }
    public int CurrentPowerState
    {
        get => Values.PowerState;
        set => Change(ref Values.PowerState, value);
    }
    public int Orientation
    {
        get => Values.Orientation;
        set => Change(ref Values.Orientation, value);
    }

    public string ColorTemperaturePresetName => VcpNames.GetFormattedValueName(0x14, CurrentColorTemperature);
    public string InputSourceName => VcpNames.GetValueName(0x60, CurrentInputSource) ?? $"Source 0x{CurrentInputSource:X2}";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    private void Notify(string propertyName) => OnPropertyChanged(propertyName);
    private bool Change(ref int field, int value, [CallerMemberName] string? name = null)
    {
        if (field == value) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    int IMonitorData.Brightness { get => CurrentBrightness; set => CurrentBrightness = value; }
    int IMonitorData.Contrast { get => CurrentContrast; set => CurrentContrast = value; }
    int IMonitorData.Volume { get => CurrentVolume; set => CurrentVolume = value; }
    int IMonitorData.ColorTemperatureVcp { get => CurrentColorTemperature; set => CurrentColorTemperature = value; }
    public override string ToString() => $"{Name} ({CommunicationMethod}) - {CurrentBrightness}%";
}
