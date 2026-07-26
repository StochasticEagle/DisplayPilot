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
        var commonApplicationDataDirectory = Environment.GetFolderPath(
            Environment.SpecialFolder.CommonApplicationData);
        if (string.IsNullOrWhiteSpace(commonApplicationDataDirectory))
        {
            return new StartupRegistration(
                StartupRegistrationStatus.Unavailable,
                null);
        }

        var shortcutPath = BuildCommonStartupShortcutPath(
            commonApplicationDataDirectory);
        return new StartupRegistration(
            File.Exists(shortcutPath)
                ? StartupRegistrationStatus.AllUsersRegistered
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

    public static string BuildCommonStartupShortcutPath(
        string commonApplicationDataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commonApplicationDataDirectory);
        return Path.Combine(
            Path.GetFullPath(commonApplicationDataDirectory),
            "Microsoft",
            "Windows",
            "Start Menu",
            "Programs",
            "Startup",
            ShortcutFileName);
    }
}

public enum StartupRegistrationStatus
{
    Disabled,
    AllUsersRegistered,
    Unavailable,
}

public readonly record struct StartupRegistration(
    StartupRegistrationStatus Status,
    string? RegisteredCommand);
