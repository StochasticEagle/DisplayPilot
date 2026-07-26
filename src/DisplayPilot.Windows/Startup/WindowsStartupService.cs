// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using System.Runtime.InteropServices;

namespace DisplayPilot.Windows.Startup;

public static class WindowsStartupService
{
    private const string ShortcutFileName = "DisplayPilot.lnk";

    public static StartupRegistration ReadRegistration()
    {
        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return new StartupRegistration(
                StartupRegistrationStatus.Unavailable,
                null);
        }

        var shortcutPath = GetShortcutPath();
        if (!File.Exists(shortcutPath))
        {
            return new StartupRegistration(
                StartupRegistrationStatus.Disabled,
                null);
        }

        var shortcut = ReadShortcut(shortcutPath);
        var registeredCommand = BuildCommandLine(
            shortcut.TargetPath,
            shortcut.Arguments);
        var expectedCommand = BuildCommandLine(
            executablePath,
            "--startup");
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
            File.Delete(GetShortcutPath());
            return;
        }

        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new InvalidOperationException(
                "The current DisplayPilot executable path is unavailable.");
        }

        var shortcutPath = GetShortcutPath();
        Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath)!);
        WriteShortcut(shortcutPath, executablePath);
    }

    public static string BuildCommandLine(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        return $"\"{Path.GetFullPath(executablePath)}\" --startup";
    }

    private static string BuildCommandLine(
        string executablePath,
        string arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        return $"\"{Path.GetFullPath(executablePath)}\" {arguments.Trim()}";
    }

    private static string GetShortcutPath()
    {
        var startupDirectory = Environment.GetFolderPath(
            Environment.SpecialFolder.Startup);
        if (string.IsNullOrWhiteSpace(startupDirectory))
        {
            throw new InvalidOperationException(
                "The current-user Startup folder is unavailable.");
        }

        return Path.Combine(startupDirectory, ShortcutFileName);
    }

    private static ShortcutRegistration ReadShortcut(string shortcutPath)
    {
        dynamic? shell = null;
        dynamic? shortcut = null;
        try
        {
            shell = CreateShell();
            shortcut = shell.CreateShortcut(shortcutPath);
            return new ShortcutRegistration(
                (string)shortcut.TargetPath,
                (string)shortcut.Arguments);
        }
        finally
        {
            ReleaseComObject(shortcut);
            ReleaseComObject(shell);
        }
    }

    private static void WriteShortcut(
        string shortcutPath,
        string executablePath)
    {
        dynamic? shell = null;
        dynamic? shortcut = null;
        try
        {
            shell = CreateShell();
            shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = Path.GetFullPath(executablePath);
            shortcut.Arguments = "--startup";
            shortcut.WorkingDirectory =
                Path.GetDirectoryName(Path.GetFullPath(executablePath));
            shortcut.IconLocation = $"{Path.GetFullPath(executablePath)},0";
            shortcut.Save();
        }
        finally
        {
            ReleaseComObject(shortcut);
            ReleaseComObject(shell);
        }
    }

    private static object CreateShell()
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null)
        {
            throw new InvalidOperationException(
                "Windows Script Host is unavailable.");
        }

        return Activator.CreateInstance(shellType)
            ?? throw new InvalidOperationException(
                "Windows Script Host could not be started.");
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }

    private readonly record struct ShortcutRegistration(
        string TargetPath,
        string Arguments);
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
