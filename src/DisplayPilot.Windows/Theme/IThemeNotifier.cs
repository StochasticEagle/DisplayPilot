// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

namespace DisplayPilot.Windows.Theme;

public interface IThemeNotifier
{
    void NotifyThemeChanged();

    void NotifyColorPrevalenceChanged();
}
