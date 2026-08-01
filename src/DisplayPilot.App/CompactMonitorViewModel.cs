// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using DisplayPilot.Display.Rotation;

namespace DisplayPilot.App;

public sealed class CompactMonitorViewModel : INotifyPropertyChanged
{
    private double _brightnessPercent;
    private double _contrastPercent;
    private ColorTemperaturePresetViewModel? _selectedColorTemperaturePreset;
    private RotationOptionViewModel? _selectedRotation;

    public CompactMonitorViewModel(
        string devicePath,
        string friendlyName,
        bool isBrightnessAvailable,
        double brightnessPercent,
        bool isContrastAvailable,
        double contrastPercent,
        IReadOnlyList<ColorTemperaturePresetViewModel> colorTemperaturePresets,
        int? currentColorTemperatureValue,
        bool isRotationAvailable,
        DisplayRotation? currentRotation,
        string status)
    {
        DevicePath = devicePath;
        FriendlyName = friendlyName;
        IsBrightnessAvailable = isBrightnessAvailable;
        _brightnessPercent = brightnessPercent;
        IsContrastAvailable = isContrastAvailable;
        _contrastPercent = contrastPercent;
        ColorTemperaturePresets = colorTemperaturePresets;
        _selectedColorTemperaturePreset = colorTemperaturePresets.FirstOrDefault(preset =>
            preset.RawValue == currentColorTemperatureValue);
        IsRotationAvailable = isRotationAvailable;
        RotationOptions =
        [
            new(DisplayRotation.Landscape, "Landscape (0°)"),
            new(DisplayRotation.Portrait, "Portrait (90°)"),
            new(DisplayRotation.LandscapeFlipped, "Landscape flipped (180°)"),
            new(DisplayRotation.PortraitFlipped, "Portrait flipped (270°)"),
        ];
        _selectedRotation = RotationOptions.FirstOrDefault(option =>
            option.Rotation == currentRotation);
        Status = status;
    }

    public string DevicePath { get; }

    public string FriendlyName { get; }

    public bool IsBrightnessAvailable { get; }

    public bool IsContrastAvailable { get; }

    public bool IsColorTemperatureAvailable => ColorTemperaturePresets.Count > 0;

    public IReadOnlyList<ColorTemperaturePresetViewModel> ColorTemperaturePresets { get; }

    public bool IsRotationAvailable { get; }

    public IReadOnlyList<RotationOptionViewModel> RotationOptions { get; }

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

    public double ContrastPercent
    {
        get => _contrastPercent;
        set
        {
            if (Math.Abs(_contrastPercent - value) < double.Epsilon)
            {
                return;
            }

            _contrastPercent = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ContrastText));
        }
    }

    public string ContrastText => $"{ContrastPercent:F0}%";

    public ColorTemperaturePresetViewModel? SelectedColorTemperaturePreset
    {
        get => _selectedColorTemperaturePreset;
        set
        {
            if (Equals(_selectedColorTemperaturePreset, value))
            {
                return;
            }

            _selectedColorTemperaturePreset = value;
            OnPropertyChanged();
        }
    }

    public RotationOptionViewModel? SelectedRotation
    {
        get => _selectedRotation;
        set
        {
            if (Equals(_selectedRotation, value))
            {
                return;
            }

            _selectedRotation = value;
            OnPropertyChanged();
        }
    }

    public string Status { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record ColorTemperaturePresetViewModel(int RawValue, string DisplayName);

public sealed record RotationOptionViewModel(DisplayRotation Rotation, string DisplayName);
