// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using DisplayPilot.Core.Theme;

namespace DisplayPilot.Windows.Settings;

public interface IThemeScheduleSettingsStore
{
    ThemeScheduleSettingsLoadResult Load();

    void Save(CustomThemeSchedule schedule, bool automationEnabled);
}
