// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using Microsoft.Win32;

namespace DisplayPilot.Windows.Startup;

public static class WindowsStartupService
{
    private const string RunKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DisplayPilot";

    public static StartupRegistration ReadRegistration()
    {
        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return new StartupRegistration(
                StartupRegistrationStatus.Unavailable,
                null);
        }

        using var currentUser = RegistryKey.OpenBaseKey(
            RegistryHive.CurrentUser,
            RegistryView.Default);
        using var key = currentUser.OpenSubKey(RunKeyPath);
        var registeredCommand = key?.GetValue(
            ValueName,
            defaultValue: null,
            RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
        if (string.IsNullOrWhiteSpace(registeredCommand))
        {
            return new StartupRegistration(
                StartupRegistrationStatus.Disabled,
                null);
        }

        var expectedCommand = BuildCommandLine(executablePath);
        return new StartupRegistration(
            string.Equals(
                registeredCommand,
                expectedCommand,
                StringComparison.OrdinalIgnoreCase)
                ? StartupRegistrationStatus.PerUserRegistered
                : StartupRegistrationStatus.DifferentExecutable,
            registeredCommand);
    }

    public static void SetRegistration(bool enabled)
    {
        using var currentUser = RegistryKey.OpenBaseKey(
            RegistryHive.CurrentUser,
            RegistryView.Default);
        using var key = currentUser.CreateSubKey(RunKeyPath, writable: true);
        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new InvalidOperationException(
                "The DisplayPilot executable path is unavailable.");
        }

        key.SetValue(
            ValueName,
            BuildCommandLine(executablePath),
            RegistryValueKind.String);
    }

    public static string BuildCommandLine(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        return $"\"{Path.GetFullPath(executablePath)}\" --startup";
    }
}

public enum StartupRegistrationStatus
{
    Disabled,
    PerUserRegistered,
    DifferentExecutable,
    Unavailable,
}

public readonly record struct StartupRegistration(
    StartupRegistrationStatus Status,
    string? RegisteredCommand);
