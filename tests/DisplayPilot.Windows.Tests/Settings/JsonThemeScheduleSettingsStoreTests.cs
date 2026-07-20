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
        Assert.IsFalse(File.Exists(_settingsPath));
    }

    [TestMethod]
    public void SavedScheduleRoundTrips()
    {
        var store = new JsonThemeScheduleSettingsStore(_settingsPath);
        var expected = new CustomThemeSchedule(new TimeOnly(6, 45), new TimeOnly(22, 15));

        store.Save(expected, automationEnabled: true);
        var result = store.Load();

        Assert.IsTrue(result.WasLoadedFromDisk);
        Assert.AreEqual(expected, result.Schedule);
        Assert.IsTrue(result.AutomationEnabled);
        StringAssert.Contains(File.ReadAllText(_settingsPath, Encoding.UTF8), "\"version\": 2");
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
}
