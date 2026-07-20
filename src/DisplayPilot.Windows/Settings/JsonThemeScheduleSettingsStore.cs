// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using System.Text;
using System.Text.Json;
using DisplayPilot.Core.Theme;

namespace DisplayPilot.Windows.Settings;

public sealed class JsonThemeScheduleSettingsStore : IThemeScheduleSettingsStore
{
    private const int CurrentVersion = 1;
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
            return new ThemeScheduleSettingsLoadResult(CreateDefault(), WasLoadedFromDisk: false);
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

        if (stored is null || stored.Version != CurrentVersion)
        {
            throw new InvalidDataException("The theme schedule settings version is not supported.");
        }

        if (!IsMinuteOfDay(stored.LightMinutes) || !IsMinuteOfDay(stored.DarkMinutes))
        {
            throw new InvalidDataException("Theme schedule times must be between 00:00 and 23:59.");
        }

        try
        {
            return new ThemeScheduleSettingsLoadResult(
                new CustomThemeSchedule(
                    TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(stored.LightMinutes)),
                    TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(stored.DarkMinutes))),
                WasLoadedFromDisk: true);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("The saved Light and Dark transition times must be different.", exception);
        }
    }

    public void Save(CustomThemeSchedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException("The theme schedule settings path has no directory.");
        Directory.CreateDirectory(directory);

        var stored = new StoredSettings
        {
            Version = CurrentVersion,
            LightMinutes = ToMinuteOfDay(schedule.LightTime),
            DarkMinutes = ToMinuteOfDay(schedule.DarkTime),
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

    private sealed class StoredSettings
    {
        public int Version { get; init; }

        public int LightMinutes { get; init; }

        public int DarkMinutes { get; init; }
    }
}
