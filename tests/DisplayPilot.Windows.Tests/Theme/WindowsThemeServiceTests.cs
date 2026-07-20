// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using DisplayPilot.Core.Theme;
using DisplayPilot.Windows.Theme;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DisplayPilot.Windows.Tests.Theme;

[TestClass]
public sealed class WindowsThemeServiceTests
{
    [TestMethod]
    public void ReadStatePreservesMixedWindowsTheme()
    {
        var store = new FakeStore
        {
            [WindowsThemeService.AppsUseLightTheme] = 1,
            [WindowsThemeService.SystemUsesLightTheme] = 0,
        };

        var state = new WindowsThemeService(store, new FakeNotifier()).ReadState();

        Assert.IsTrue(state.AppsUseLightTheme);
        Assert.IsFalse(state.SystemUsesLightTheme);
        Assert.IsFalse(state.IsUnified);
    }

    [TestMethod]
    public void ApplyDarkChangesBothValuesAndNotifies()
    {
        var store = LightStore();
        var notifier = new FakeNotifier();

        var result = new WindowsThemeService(store, notifier).Apply(ThemeMode.Dark);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(0, store[WindowsThemeService.AppsUseLightTheme]);
        Assert.AreEqual(0, store[WindowsThemeService.SystemUsesLightTheme]);
        Assert.AreEqual(2, notifier.ThemeChangeCount);
        Assert.AreEqual(0, notifier.ColorChangeCount);
    }

    [TestMethod]
    public void ApplyCurrentThemePerformsNoWritesOrNotifications()
    {
        var store = LightStore();
        var notifier = new FakeNotifier();

        var result = new WindowsThemeService(store, notifier).Apply(ThemeMode.Light);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(0, store.WriteCount);
        Assert.AreEqual(0, notifier.ThemeChangeCount);
    }

    [TestMethod]
    public void ApplyLightResetsColorPrevalenceLikePowerToys()
    {
        var store = new FakeStore
        {
            [WindowsThemeService.AppsUseLightTheme] = 0,
            [WindowsThemeService.SystemUsesLightTheme] = 0,
            [WindowsThemeService.ColorPrevalence] = 1,
        };
        var notifier = new FakeNotifier();

        var result = new WindowsThemeService(store, notifier).Apply(ThemeMode.Light);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(0, store[WindowsThemeService.ColorPrevalence]);
        Assert.AreEqual(2, notifier.ThemeChangeCount);
        Assert.AreEqual(1, notifier.ColorChangeCount);
    }

    private static FakeStore LightStore() => new()
    {
        [WindowsThemeService.AppsUseLightTheme] = 1,
        [WindowsThemeService.SystemUsesLightTheme] = 1,
    };

    private sealed class FakeStore : IThemeStore
    {
        private readonly Dictionary<string, int> _values = [];

        public int WriteCount { get; private set; }

        public int this[string name]
        {
            get => _values[name];
            set => _values[name] = value;
        }

        public int Read(string valueName, int defaultValue) =>
            _values.TryGetValue(valueName, out var value) ? value : defaultValue;

        public void Write(string valueName, int value)
        {
            _values[valueName] = value;
            WriteCount++;
        }
    }

    private sealed class FakeNotifier : IThemeNotifier
    {
        public int ThemeChangeCount { get; private set; }

        public int ColorChangeCount { get; private set; }

        public void NotifyThemeChanged() => ThemeChangeCount++;

        public void NotifyColorPrevalenceChanged() => ColorChangeCount++;
    }
}
