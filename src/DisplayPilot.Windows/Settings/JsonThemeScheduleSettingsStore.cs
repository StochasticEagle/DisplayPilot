// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using System.Text;
using System.Text.Json;
using DisplayPilot.Core.Theme;

namespace DisplayPilot.Windows.Settings;

public sealed class JsonThemeScheduleSettingsStore : IThemeScheduleSettingsStore
{
    private const int CurrentVersion = 4;
    private const int FirstSupportedVersion = 1;
    private const int MinutesPerDay = 24 * 60;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string _filePath;

    public JsonThemeScheduleSettingsStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DisplayPilot",
            "settings.json"))
    {
    }

    public JsonThemeScheduleSettingsStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = Path.GetFullPath(filePath);
    }

    public ThemeScheduleSettingsLoadResult Load()
    {
        if (!File.Exists(_filePath))
        {
            return new ThemeScheduleSettingsLoadResult(
                CreateDefault(),
                WasLoadedFromDisk: false,
                AutomationEnabled: false,
                ReduceBrightness: false,
                BrightnessReductionActive: false,
                BrightnessRestoreValues: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                ScheduleMode: ThemeScheduleMode.FixedTimes,
                SolarLocation: null);
        }

        var json = File.ReadAllText(_filePath, Encoding.UTF8);
        StoredSettings? stored;
        try
        {
            stored = JsonSerializer.Deserialize<StoredSettings>(json, SerializerOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The theme schedule settings file is not valid JSON.", exception);
        }

        if (stored is null || stored.Version is < FirstSupportedVersion or > CurrentVersion)
        {
            throw new InvalidDataException("The theme schedule settings version is not supported.");
        }

        if (!IsMinuteOfDay(stored.LightMinutes) || !IsMinuteOfDay(stored.DarkMinutes))
        {
            throw new InvalidDataException("Theme schedule times must be between 00:00 and 23:59.");
        }

        try
        {
            var restoreValues = ValidateRestoreValues(stored);
            var scheduleMode = ReadScheduleMode(stored);
            var solarLocation = ReadSolarLocation(stored, scheduleMode);
            return new ThemeScheduleSettingsLoadResult(
                new CustomThemeSchedule(
                    TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(stored.LightMinutes)),
                    TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(stored.DarkMinutes))),
                WasLoadedFromDisk: true,
                AutomationEnabled: stored.Version >= 2 && stored.AutomationEnabled,
                ReduceBrightness: stored.Version >= 3 && stored.ReduceBrightness,
                BrightnessReductionActive: stored.Version >= 3 && restoreValues.Count > 0,
                BrightnessRestoreValues: restoreValues,
                ScheduleMode: scheduleMode,
                SolarLocation: solarLocation);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("The saved Light and Dark transition times must be different.", exception);
        }
    }

    public void Save(
        CustomThemeSchedule schedule,
        bool automationEnabled,
        bool reduceBrightness,
        bool brightnessReductionActive,
        IReadOnlyDictionary<string, int> brightnessRestoreValues,
        ThemeScheduleMode scheduleMode,
        SolarLocation? solarLocation)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        if (!Enum.IsDefined(scheduleMode))
        {
            throw new ArgumentOutOfRangeException(nameof(scheduleMode));
        }

        if (scheduleMode == ThemeScheduleMode.SunriseSunset && solarLocation is null)
        {
            throw new ArgumentNullException(nameof(solarLocation));
        }

        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException("The theme schedule settings path has no directory.");
        Directory.CreateDirectory(directory);

        var stored = new StoredSettings
        {
            Version = CurrentVersion,
            LightMinutes = ToMinuteOfDay(schedule.LightTime),
            DarkMinutes = ToMinuteOfDay(schedule.DarkTime),
            AutomationEnabled = automationEnabled,
            ReduceBrightness = reduceBrightness,
            BrightnessReductionActive = brightnessReductionActive,
            BrightnessRestoreValues = brightnessRestoreValues.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase),
            ScheduleMode = (int)scheduleMode,
            Latitude = solarLocation?.Latitude,
            Longitude = solarLocation?.Longitude,
            LocationLabel = solarLocation?.Label,
        };
        var json = JsonSerializer.Serialize(stored, SerializerOptions);
        var temporaryPath = _filePath + ".tmp";
        File.WriteAllText(temporaryPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(temporaryPath, _filePath, overwrite: true);
    }

    public static CustomThemeSchedule CreateDefault() =>
        new(new TimeOnly(7, 0), new TimeOnly(19, 0));

    private static bool IsMinuteOfDay(int value) => value is >= 0 and < MinutesPerDay;

    private static int ToMinuteOfDay(TimeOnly value) => (value.Hour * 60) + value.Minute;

    private static Dictionary<string, int> ValidateRestoreValues(StoredSettings stored)
    {
        if (stored.Version < 3 || stored.BrightnessRestoreValues is null)
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        if (stored.BrightnessRestoreValues.Any(pair =>
                string.IsNullOrWhiteSpace(pair.Key) || pair.Value is < 0 or > 100))
        {
            throw new InvalidDataException("Saved brightness restoration values must be between 0 and 100 percent.");
        }

        return new Dictionary<string, int>(
            stored.BrightnessRestoreValues,
            StringComparer.OrdinalIgnoreCase);
    }

    private static ThemeScheduleMode ReadScheduleMode(StoredSettings stored)
    {
        if (stored.Version < 4)
        {
            return ThemeScheduleMode.FixedTimes;
        }

        if (!Enum.IsDefined(typeof(ThemeScheduleMode), stored.ScheduleMode))
        {
            throw new InvalidDataException("The saved theme schedule mode is not supported.");
        }

        return (ThemeScheduleMode)stored.ScheduleMode;
    }

    private static SolarLocation? ReadSolarLocation(
        StoredSettings stored,
        ThemeScheduleMode scheduleMode)
    {
        if (scheduleMode == ThemeScheduleMode.FixedTimes)
        {
            return null;
        }

        if (stored.Latitude is null || stored.Longitude is null)
        {
            throw new InvalidDataException("Sunrise/sunset scheduling requires a saved location.");
        }

        try
        {
            return new SolarLocation(
                stored.Latitude.Value,
                stored.Longitude.Value,
                stored.LocationLabel);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidDataException("The saved solar location is outside the valid coordinate range.", exception);
        }
    }

    private sealed class StoredSettings
    {
        public int Version { get; init; }

        public int LightMinutes { get; init; }

        public int DarkMinutes { get; init; }

        public bool AutomationEnabled { get; init; }

        public bool ReduceBrightness { get; init; }

        public bool BrightnessReductionActive { get; init; }

        public Dictionary<string, int>? BrightnessRestoreValues { get; init; }

        public int ScheduleMode { get; init; }

        public double? Latitude { get; init; }

        public double? Longitude { get; init; }

        public string? LocationLabel { get; init; }
    }
}
