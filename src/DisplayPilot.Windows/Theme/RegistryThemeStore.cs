// SPDX-License-Identifier: Unlicense
using Microsoft.Win32;
namespace DisplayPilot.Windows.Theme;

public sealed class RegistryThemeStore : IThemeStore
{
    private const string Path = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public int Read(string valueName, int defaultValue)
    {
        using var key = Registry.CurrentUser.OpenSubKey(Path);
        return key?.GetValue(valueName) is int number ? number : defaultValue;
    }

    public void Write(string valueName, int value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Path)
            ?? throw new IOException("Cannot access Windows theme preferences.");
        key.SetValue(valueName, value, RegistryValueKind.DWord);
    }
}
