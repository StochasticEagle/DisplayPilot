// SPDX-License-Identifier: Unlicense
namespace DisplayPilot.Windows.Theme;

public sealed record ThemeState(bool AppsUseLightTheme, bool SystemUsesLightTheme)
{
    public bool IsUnified => AppsUseLightTheme == SystemUsesLightTheme;
}
