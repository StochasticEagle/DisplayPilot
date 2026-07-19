// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using DisplayPilot.Core.Theme;

namespace DisplayPilot.Windows.Theme;

public sealed record ThemeApplyResult(
    ThemeMode RequestedMode,
    ThemeState Before,
    ThemeState After,
    bool AppsChanged,
    bool SystemChanged)
{
    public bool Succeeded => RequestedMode == ThemeMode.Light
        ? After.AppsUseLightTheme && After.SystemUsesLightTheme
        : !After.AppsUseLightTheme && !After.SystemUsesLightTheme;
}
