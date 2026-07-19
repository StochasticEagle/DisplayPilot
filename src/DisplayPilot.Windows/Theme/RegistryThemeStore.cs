// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Win32;

namespace DisplayPilot.Windows.Theme;

/// <summary>
/// C# adaptation of PowerToys Light Switch ThemeHelper registry access.
/// </summary>
public sealed class RegistryThemeStore : IThemeStore
{
    private const string PersonalizePath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public int Read(string valueName, int defaultValue)
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizePath, writable: false);
        return key?.GetValue(valueName) is int value ? value : defaultValue;
    }

    public void Write(string valueName, int value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(PersonalizePath, writable: true)
            ?? throw new IOException("Could not open the Windows Personalize registry key.");
        key.SetValue(valueName, value, RegistryValueKind.DWord);
    }
}
