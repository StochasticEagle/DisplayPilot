// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using System.Runtime.InteropServices;
using Microsoft.Win32;
using Windows.ApplicationModel;

namespace DisplayPilot.Windows.Startup;

public static class WindowsStartupService
{
    private const string PackagedStartupTaskId = "DisplayPilotStartup";
    private const string RunKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DisplayPilot";

    public static async Task<StartupRegistration> ReadRegistrationAsync()
    {
        if (!IsPackaged())
        {
            return ReadRegistryRegistration();
        }

        var startupTask = await StartupTask.GetAsync(PackagedStartupTaskId);
        return new StartupRegistration(
            startupTask.State switch
            {
                StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy =>
                    StartupRegistrationStatus.Enabled,
                StartupTaskState.DisabledByUser =>
                    StartupRegistrationStatus.DisabledByUser,
                StartupTaskState.DisabledByPolicy =>
                    StartupRegistrationStatus.DisabledByPolicy,
                _ => StartupRegistrationStatus.Disabled,
            },
            null);
    }

    public static async Task SetEnabledAsync(bool enabled)
    {
        if (!IsPackaged())
        {
            SetRegistryEnabled(enabled);
            return;
        }

        var startupTask = await StartupTask.GetAsync(PackagedStartupTaskId);
        if (enabled)
        {
            await startupTask.RequestEnableAsync();
        }
        else
        {
            startupTask.Disable();
        }
    }

    private static StartupRegistration ReadRegistryRegistration()
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

    private static void SetRegistryEnabled(bool enabled)
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

    private static bool IsPackaged()
    {
        try
        {
            _ = Package.Current.Id.FullName;
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (COMException exception)
            when (unchecked((uint)exception.HResult) == 0x80073D54)
        {
            return false;
        }
    }
}

public enum StartupRegistrationStatus
{
    Disabled,
    Enabled,
    DifferentExecutable,
    DisabledByUser,
    DisabledByPolicy,
    Unavailable,
}

public readonly record struct StartupRegistration(
    StartupRegistrationStatus Status,
    string? RegisteredCommand);
