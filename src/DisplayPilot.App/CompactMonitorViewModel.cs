// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DisplayPilot.App;

public sealed class CompactMonitorViewModel : INotifyPropertyChanged
{
    private double _brightnessPercent;

    public CompactMonitorViewModel(
        string devicePath,
        string friendlyName,
        bool isBrightnessAvailable,
        double brightnessPercent,
        string status)
    {
        DevicePath = devicePath;
        FriendlyName = friendlyName;
        IsBrightnessAvailable = isBrightnessAvailable;
        _brightnessPercent = brightnessPercent;
        Status = status;
    }

    public string DevicePath { get; }

    public string FriendlyName { get; }

    public bool IsBrightnessAvailable { get; }

    public double BrightnessPercent
    {
        get => _brightnessPercent;
        set
        {
            if (Math.Abs(_brightnessPercent - value) < double.Epsilon)
            {
                return;
            }

            _brightnessPercent = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(BrightnessText));
        }
    }

    public string BrightnessText => $"{BrightnessPercent:F0}%";

    public string Status { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
