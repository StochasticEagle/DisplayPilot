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

        using var localMachine = RegistryKey.OpenBaseKey(
            RegistryHive.LocalMachine,
            RegistryView.Registry64);
        using var key = localMachine.OpenSubKey(RunKeyPath);
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
                ? StartupRegistrationStatus.MachineRegistered
                : StartupRegistrationStatus.DifferentExecutable,
            registeredCommand);
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
    MachineRegistered,
    DifferentExecutable,
    Unavailable,
}

public readonly record struct StartupRegistration(
    StartupRegistrationStatus Status,
    string? RegisteredCommand);
