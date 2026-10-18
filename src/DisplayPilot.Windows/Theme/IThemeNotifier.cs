// SPDX-License-Identifier: Unlicense
namespace DisplayPilot.Windows.Theme;

public interface IThemeNotifier
{
    void NotifyThemeChanged();

    void NotifyColorPrevalenceChanged();
}
