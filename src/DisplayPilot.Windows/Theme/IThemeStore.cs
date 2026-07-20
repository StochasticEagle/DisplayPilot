// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

namespace DisplayPilot.Windows.Theme;

public interface IThemeStore
{
    int Read(string valueName, int defaultValue);

    void Write(string valueName, int value);
}
