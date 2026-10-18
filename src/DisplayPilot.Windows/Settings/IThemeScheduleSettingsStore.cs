// SPDX-License-Identifier: Unlicense
using DisplayPilot.Core.Theme;

namespace DisplayPilot.Windows.Settings;

public interface IThemeScheduleSettingsStore
{
    ThemeScheduleSettingsLoadResult Load();

    void Save(
        CustomThemeSchedule schedule,
        bool automationEnabled,
        bool reduceBrightness,
        bool brightnessReductionActive,
        IReadOnlyDictionary<string, int> brightnessRestoreValues,
        ThemeScheduleMode scheduleMode,
        SolarLocation? solarLocation);
}
