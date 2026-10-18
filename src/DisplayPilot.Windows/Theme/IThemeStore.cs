// SPDX-License-Identifier: Unlicense
namespace DisplayPilot.Windows.Theme;

public interface IThemeStore
{
    int Read(string valueName, int defaultValue);

    void Write(string valueName, int value);
}
