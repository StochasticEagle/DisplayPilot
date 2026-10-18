// SPDX-License-Identifier: Unlicense
using DisplayPilot.Core.Theme;

namespace DisplayPilot.Windows.Settings;

public sealed record ThemeScheduleSettingsLoadResult(
    CustomThemeSchedule Schedule,
    bool WasLoadedFromDisk,
    bool AutomationEnabled,
    bool ReduceBrightness,
    bool BrightnessReductionActive,
    IReadOnlyDictionary<string, int> BrightnessRestoreValues,
    ThemeScheduleMode ScheduleMode,
    SolarLocation? SolarLocation);
