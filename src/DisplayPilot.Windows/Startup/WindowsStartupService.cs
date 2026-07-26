// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using Microsoft.Win32;

namespace DisplayPilot.Windows.Startup;

public static class WindowsStartupService
{
    private const string RunKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DisplayPilot";
    private const string ShortcutFileName = "DisplayPilot.lnk";

    public static StartupRegistration ReadRegistration()
    {
        var startupDirectory = Environment.GetFolderPath(
            Environment.SpecialFolder.Startup);
        if (string.IsNullOrWhiteSpace(startupDirectory))
        {
            return new StartupRegistration(
                StartupRegistrationStatus.Unavailable,
                null);
        }

        var shortcutPath = BuildStartupShortcutPath(startupDirectory);
        return new StartupRegistration(
            File.Exists(shortcutPath)
                ? StartupRegistrationStatus.PerUserRegistered
                : StartupRegistrationStatus.Disabled,
            shortcutPath);
    }

    public static void RemoveLegacyPerUserRegistration()
    {
        using var currentUser = RegistryKey.OpenBaseKey(
            RegistryHive.CurrentUser,
            RegistryView.Default);
        using var key = currentUser.CreateSubKey(RunKeyPath, writable: true);
        key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    public static string BuildStartupShortcutPath(string startupDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startupDirectory);
        return Path.Combine(
            Path.GetFullPath(startupDirectory),
            ShortcutFileName);
    }
}

public enum StartupRegistrationStatus
{
    Disabled,
    PerUserRegistered,
    Unavailable,
}

public readonly record struct StartupRegistration(
    StartupRegistrationStatus Status,
    string? RegisteredCommand);
