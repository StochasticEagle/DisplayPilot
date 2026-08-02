// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using System.Globalization;
using System.Text;
using DisplayPilot.Core.Theme;
using DisplayPilot.Windows.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DisplayPilot.Windows.Tests.Settings;

[TestClass]
public sealed class JsonThemeScheduleSettingsStoreTests
{
    private string _testDirectory = null!;
    private string _settingsPath = null!;

    [TestInitialize]
    public void Initialize()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            "DisplayPilot.Tests",
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        _settingsPath = Path.Combine(_testDirectory, "settings.json");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void MissingFileReturnsDefaultsWithoutWriting()
    {
        var result = new JsonThemeScheduleSettingsStore(_settingsPath).Load();

        Assert.IsFalse(result.WasLoadedFromDisk);
        Assert.AreEqual(new TimeOnly(7, 0), result.Schedule.LightTime);
        Assert.AreEqual(new TimeOnly(19, 0), result.Schedule.DarkTime);
        Assert.IsFalse(result.AutomationEnabled);
        Assert.IsFalse(result.ReduceBrightness);
        Assert.IsFalse(result.BrightnessReductionActive);
        Assert.AreEqual(0, result.BrightnessRestoreValues.Count);
        Assert.AreEqual(ThemeScheduleMode.FixedTimes, result.ScheduleMode);
        Assert.IsNull(result.SolarLocation);
        Assert.IsFalse(File.Exists(_settingsPath));
    }

    [TestMethod]
    public void SavedScheduleRoundTrips()
    {
        var store = new JsonThemeScheduleSettingsStore(_settingsPath);
        var expected = new CustomThemeSchedule(new TimeOnly(6, 45), new TimeOnly(22, 15));

        store.Save(
            expected,
            automationEnabled: true,
            reduceBrightness: true,
            brightnessReductionActive: true,
            brightnessRestoreValues: new Dictionary<string, int> { [@"\\.\DISPLAY1"] = 67 },
            scheduleMode: ThemeScheduleMode.SunriseSunset,
            solarLocation: new SolarLocation(40.7128, -74.0060, "New York"));
        var result = store.Load();

        Assert.IsTrue(result.WasLoadedFromDisk);
        Assert.AreEqual(expected, result.Schedule);
        Assert.IsTrue(result.AutomationEnabled);
        Assert.IsTrue(result.ReduceBrightness);
        Assert.IsTrue(result.BrightnessReductionActive);
        Assert.AreEqual(67, result.BrightnessRestoreValues[@"\\.\DISPLAY1"]);
        Assert.AreEqual(ThemeScheduleMode.SunriseSunset, result.ScheduleMode);
        Assert.AreEqual(40.7128, result.SolarLocation?.Latitude);
        Assert.AreEqual(-74.0060, result.SolarLocation?.Longitude);
        Assert.AreEqual("New York", result.SolarLocation?.Label);
        StringAssert.Contains(File.ReadAllText(_settingsPath, Encoding.UTF8), "\"version\": 4");
    }

    [TestMethod]
    public void VersionOneScheduleMigratesWithAutomationDisabled()
    {
        Directory.CreateDirectory(_testDirectory);
        File.WriteAllText(
            _settingsPath,
            "{\"version\":1,\"lightMinutes\":405,\"darkMinutes\":1335}",
            Encoding.UTF8);

        var result = new JsonThemeScheduleSettingsStore(_settingsPath).Load();

        Assert.IsTrue(result.WasLoadedFromDisk);
        Assert.AreEqual(new TimeOnly(6, 45), result.Schedule.LightTime);
        Assert.AreEqual(new TimeOnly(22, 15), result.Schedule.DarkTime);
        Assert.IsFalse(result.AutomationEnabled);
        Assert.IsFalse(result.ReduceBrightness);
        Assert.IsFalse(result.BrightnessReductionActive);
        Assert.AreEqual(ThemeScheduleMode.FixedTimes, result.ScheduleMode);
    }

    [TestMethod]
    public void VersionTwoScheduleMigratesWithoutBrightnessReduction()
    {
        Directory.CreateDirectory(_testDirectory);
        File.WriteAllText(
            _settingsPath,
            "{\"version\":2,\"lightMinutes\":405,\"darkMinutes\":1335,\"automationEnabled\":true}",
            Encoding.UTF8);

        var result = new JsonThemeScheduleSettingsStore(_settingsPath).Load();

        Assert.IsTrue(result.AutomationEnabled);
        Assert.IsFalse(result.ReduceBrightness);
        Assert.IsFalse(result.BrightnessReductionActive);
        Assert.AreEqual(0, result.BrightnessRestoreValues.Count);
        Assert.AreEqual(ThemeScheduleMode.FixedTimes, result.ScheduleMode);
    }

    [TestMethod]
    public void InvalidBrightnessRestoreValueIsRejected()
    {
        Directory.CreateDirectory(_testDirectory);
        File.WriteAllText(
            _settingsPath,
            "{\"version\":3,\"lightMinutes\":405,\"darkMinutes\":1335," +
            "\"automationEnabled\":true,\"reduceBrightness\":true," +
            "\"brightnessReductionActive\":true," +
            "\"brightnessRestoreValues\":{\"display\":101}}",
            Encoding.UTF8);

        var store = new JsonThemeScheduleSettingsStore(_settingsPath);

        Assert.ThrowsExactly<InvalidDataException>(() => store.Load());
    }

    [TestMethod]
    public void InvalidJsonIsRejected()
    {
        Directory.CreateDirectory(_testDirectory);
        File.WriteAllText(_settingsPath, "not-json", Encoding.UTF8);
        var store = new JsonThemeScheduleSettingsStore(_settingsPath);

        Assert.ThrowsExactly<InvalidDataException>(() => store.Load());
    }

    [TestMethod]
    public void EqualSavedTimesAreRejected()
    {
        Directory.CreateDirectory(_testDirectory);
        File.WriteAllText(
            _settingsPath,
            "{\"version\":1,\"lightMinutes\":420,\"darkMinutes\":420}",
            Encoding.UTF8);
        var store = new JsonThemeScheduleSettingsStore(_settingsPath);

        Assert.ThrowsExactly<InvalidDataException>(() => store.Load());
    }

    [TestMethod]
    public void SolarScheduleWithoutCoordinatesIsRejected()
    {
        Directory.CreateDirectory(_testDirectory);
        File.WriteAllText(
            _settingsPath,
            "{\"version\":4,\"lightMinutes\":420,\"darkMinutes\":1140,\"scheduleMode\":1}",
            Encoding.UTF8);
        var store = new JsonThemeScheduleSettingsStore(_settingsPath);

        Assert.ThrowsExactly<InvalidDataException>(() => store.Load());
    }

    [TestMethod]
    public void UnsupportedScheduleModeIsRejected()
    {
        Directory.CreateDirectory(_testDirectory);
        File.WriteAllText(
            _settingsPath,
            "{\"version\":4,\"lightMinutes\":420,\"darkMinutes\":1140,\"scheduleMode\":2}",
            Encoding.UTF8);
        var store = new JsonThemeScheduleSettingsStore(_settingsPath);

        Assert.ThrowsExactly<InvalidDataException>(() => store.Load());
    }
}
