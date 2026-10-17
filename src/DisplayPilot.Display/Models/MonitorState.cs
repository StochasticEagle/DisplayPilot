// Copyright (c) Microsoft Corporation
// Portions derived from PowerToys; MIT terms retained in LICENSE.
using DisplayPilot.Display.Mccs;
namespace DisplayPilot.Display.Models;
public partial class Monitor
{
    private int _currentBrightness;
    private int _currentColorTemperature = 0x05;
    private int _currentInputSource;
    private int _currentPowerState = 0x01;
    private int _orientation;
    private int _currentContrast = 50;
    private int _currentVolume = 50;
        /// <summary>
        /// Gets or sets current brightness as a percentage (0-100).
        /// </summary>
        public int CurrentBrightness
        {
            get => _currentBrightness;
            set
            {
                var clamped = Math.Clamp(value, 0, 100);
                if (_currentBrightness != clamped)
                {
                    _currentBrightness = clamped;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the device-reported raw VCP maximum for brightness (VCP 0x10).
        /// </summary>
        /// <remarks>
        /// <para>This is the value returned in the "max" out-parameter of <c>GetVCPFeatureAndVCPFeatureReply</c>.
        /// Most monitors report 100, but some (e.g. several Samsung models) report 50 — for those, writing
        /// the slider percentage as the raw VCP value lops off the upper half of the range. Writers must scale
        /// percent → raw using this field; readers use it via <see cref="VcpFeatureValue.ToPercentage"/>.</para>
        /// </remarks>
        public int BrightnessVcpMax { get; set; } = 100;

        /// <summary>
        /// Gets or sets current color temperature VCP preset value (from VCP code 0x14).
        /// This stores the raw VCP value (e.g., 0x05 for 6500K), not Kelvin temperature.
        /// Use ColorTemperaturePresetName to get human-readable name.
        /// </summary>
        public int CurrentColorTemperature
        {
            get => _currentColorTemperature;
            set
            {
                if (_currentColorTemperature != value)
                {
                    _currentColorTemperature = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ColorTemperaturePresetName));
                }
            }
        }

        /// <summary>
        /// Gets human-readable color temperature preset name (e.g., "6500K (0x05)", "sRGB (0x01)")
        /// </summary>
        public string ColorTemperaturePresetName =>
            VcpNames.GetFormattedValueName(0x14, CurrentColorTemperature);

        /// <summary>
        /// Gets or sets a value indicating whether the monitor supports color temperature adjustment via VCP 0x14
        /// </summary>
        public bool SupportsColorTemperature { get; set; }

        /// <summary>
        /// Gets or sets current input source VCP value (from VCP code 0x60).
        /// This stores the raw VCP value (e.g., 0x11 for HDMI-1).
        /// Use InputSourceName to get human-readable name.
        /// </summary>
        public int CurrentInputSource
        {
            get => _currentInputSource;
            set
            {
                if (_currentInputSource != value)
                {
                    _currentInputSource = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(InputSourceName));
                }
            }
        }

        /// <summary>
        /// Gets human-readable input source name (e.g., "HDMI-1", "DisplayPort-1")
        /// Returns just the name without hex value for cleaner UI display.
        /// </summary>
        public string InputSourceName =>
            VcpNames.GetValueName(0x60, CurrentInputSource) ?? $"Source 0x{CurrentInputSource:X2}";

        /// <summary>
        /// Gets a value indicating whether the monitor supports input source switching via VCP 0x60
        /// </summary>
        public bool SupportsInputSource => VcpCapabilitiesInfo?.SupportsVcpCode(0x60) ?? false;

        /// <summary>
        /// Gets get supported input sources from capabilities (as list of VCP values)
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<int>? SupportedInputSources =>
            VcpCapabilitiesInfo?.GetSupportedValues(0x60);

        /// <summary>
        /// Gets a value indicating whether the monitor supports power state control via VCP 0xD6
        /// </summary>
        public bool SupportsPowerState => VcpCapabilitiesInfo?.SupportsVcpCode(0xD6) ?? false;

        /// <summary>
        /// Gets supported power states from capabilities (as list of VCP values)
        /// Values: 0x01=On, 0x02=Standby, 0x03=Suspend, 0x04=Off(DPM), 0x05=Off(Hard)
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<int>? SupportedPowerStates =>
            VcpCapabilitiesInfo?.GetSupportedValues(0xD6);

        /// <summary>
        /// Gets or sets current power state VCP value (from VCP code 0xD6).
        /// Values: 0x01=On, 0x02=Standby, 0x03=Suspend, 0x04=Off(DPM), 0x05=Off(Hard).
        /// </summary>
        public int CurrentPowerState
        {
            get => _currentPowerState;
            set
            {
                if (_currentPowerState != value)
                {
                    _currentPowerState = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the monitor supports brightness adjustment
        /// </summary>
        public bool SupportsBrightness => Capabilities.HasFlag(MonitorCapabilities.Brightness);

        /// <summary>
        /// Gets a value indicating whether the monitor supports contrast adjustment
        /// </summary>
        public bool SupportsContrast => Capabilities.HasFlag(MonitorCapabilities.Contrast);

        /// <summary>
        /// Gets a value indicating whether the monitor supports volume adjustment (for audio-capable monitors)
        /// </summary>
        public bool SupportsVolume => Capabilities.HasFlag(MonitorCapabilities.Volume);



        /// <summary>
        /// Gets or sets current contrast as a percentage (0-100).
        /// </summary>
        public int CurrentContrast
        {
            get => _currentContrast;
            set
            {
                var clamped = Math.Clamp(value, 0, 100);
                if (_currentContrast != clamped)
                {
                    _currentContrast = clamped;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the device-reported raw VCP maximum for contrast (VCP 0x12).
        /// See <see cref="BrightnessVcpMax"/> for semantics — same story applies to contrast.
        /// </summary>
        public int ContrastVcpMax { get; set; } = 100;

        /// <summary>
        /// Gets or sets current volume as a percentage (0-100).
        /// </summary>
        public int CurrentVolume
        {
            get => _currentVolume;
            set
            {
                var clamped = Math.Clamp(value, 0, 100);
                if (_currentVolume != clamped)
                {
                    _currentVolume = clamped;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the device-reported raw VCP maximum for volume (VCP 0x62).
        /// See <see cref="BrightnessVcpMax"/> for semantics — same story applies to volume.
        /// </summary>
        public int VolumeVcpMax { get; set; } = 100;


}
