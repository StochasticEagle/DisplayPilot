// Copyright (c) Microsoft Corporation
// Portions derived from PowerToys; MIT terms retained in LICENSE.
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DisplayPilot.Display.Interfaces;
namespace DisplayPilot.Display.Models;
public partial class Monitor : INotifyPropertyChanged, IMonitorData
{
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString()
        {
            return $"{Name} ({CommunicationMethod}) - {CurrentBrightness}%";
        }

        /// <inheritdoc />
        int IMonitorData.Brightness
        {
            get => CurrentBrightness;
            set => CurrentBrightness = value;
        }

        /// <inheritdoc />
        int IMonitorData.Contrast
        {
            get => CurrentContrast;
            set => CurrentContrast = value;
        }

        /// <inheritdoc />
        int IMonitorData.Volume
        {
            get => CurrentVolume;
            set => CurrentVolume = value;
        }

        /// <inheritdoc />
        int IMonitorData.ColorTemperatureVcp
        {
            get => CurrentColorTemperature;
            set => CurrentColorTemperature = value;
        }

        /// <summary>
        /// Gets or sets monitor number (1, 2, 3...)
        /// </summary>
        public int MonitorNumber { get; set; }

        /// <summary>
        /// Gets or sets the GDI device name (e.g., "\\.\DISPLAY1").
        /// This is obtained from QueryDisplayConfig during discovery and should be used
        /// for display settings APIs (EnumDisplaySettings, ChangeDisplaySettingsEx).
        /// </summary>
        public string GdiDeviceName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets monitor orientation (0=0, 1=90, 2=180, 3=270).
        /// Fires PropertyChanged when value changes.
        /// </summary>
        public int Orientation
        {
            get => _orientation;
            set
            {
                if (_orientation != value)
                {
                    _orientation = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <inheritdoc />
        int IMonitorData.MonitorNumber
        {
            get => MonitorNumber;
            set => MonitorNumber = value;
        }

        /// <inheritdoc />
        int IMonitorData.Orientation
        {
            get => Orientation;
            set => Orientation = value;
        }
}