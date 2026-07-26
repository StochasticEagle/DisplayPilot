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

        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
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
                ? StartupRegistrationStatus.Enabled
                : StartupRegistrationStatus.DifferentExecutable,
            registeredCommand);
    }

    public static void SetEnabled(bool enabled)
    {
        if (!enabled)
        {
            using var existingKey = Registry.CurrentUser.OpenSubKey(
                RunKeyPath,
                writable: true);
            existingKey?.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new InvalidOperationException(
                "The current DisplayPilot executable path is unavailable.");
        }

        using var key = Registry.CurrentUser.CreateSubKey(
            RunKeyPath,
            writable: true);
        if (key is null)
        {
            throw new InvalidOperationException(
                "The current-user startup registry key could not be opened.");
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
    Enabled,
    DifferentExecutable,
    Unavailable,
}

public readonly record struct StartupRegistration(
    StartupRegistrationStatus Status,
    string? RegisteredCommand);
