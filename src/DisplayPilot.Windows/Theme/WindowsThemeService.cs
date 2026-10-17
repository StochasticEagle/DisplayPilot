// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using DisplayPilot.Core.Theme;

namespace DisplayPilot.Windows.Theme;

/// <summary>
/// Applies per-user Windows application and system theme preferences.
/// </summary>
public sealed class WindowsThemeService
{
    public const string AppsUseLightTheme = "AppsUseLightTheme";
    public const string SystemUsesLightTheme = "SystemUsesLightTheme";
    public const string ColorPrevalence = "ColorPrevalence";

    private readonly IThemeStore _store;
    private readonly IThemeNotifier _notifier;

    public WindowsThemeService()
        : this(new RegistryThemeStore(), new WindowsThemeNotifier())
    {
    }

    public WindowsThemeService(IThemeStore store, IThemeNotifier notifier)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(notifier);
        _store = store;
        _notifier = notifier;
    }

    public ThemeState ReadState() =>
        new(
            _store.Read(AppsUseLightTheme, 1) == 1,
            _store.Read(SystemUsesLightTheme, 1) == 1);

    public ThemeApplyResult Apply(ThemeMode mode)
    {
        var before = ReadState();
        var shouldBeLight = mode == ThemeMode.Light;
        var appsChanged = before.AppsUseLightTheme != shouldBeLight;
        var systemChanged = before.SystemUsesLightTheme != shouldBeLight;

        if (appsChanged)
            _store.Write(AppsUseLightTheme, shouldBeLight ? 1 : 0);

        if (systemChanged)
        {
            _store.Write(SystemUsesLightTheme, shouldBeLight ? 1 : 0);
            if (shouldBeLight)
                _store.Write(ColorPrevalence, 0);
        }

        if (systemChanged && shouldBeLight)
            _notifier.NotifyColorPrevalenceChanged();
        else if (appsChanged || systemChanged)
            _notifier.NotifyThemeChanged();

        return new ThemeApplyResult(mode, before, ReadState(), appsChanged, systemChanged);
    }
}
