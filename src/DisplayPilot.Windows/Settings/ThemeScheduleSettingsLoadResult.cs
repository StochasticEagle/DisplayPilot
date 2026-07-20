// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using DisplayPilot.Core.Theme;

namespace DisplayPilot.Windows.Settings;

public sealed record ThemeScheduleSettingsLoadResult(
    CustomThemeSchedule Schedule,
    bool WasLoadedFromDisk,
    bool AutomationEnabled);
