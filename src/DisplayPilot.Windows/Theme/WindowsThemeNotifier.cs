// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace DisplayPilot.Windows.Theme;

/// <summary>
/// C# adaptation of the PowerToys Light Switch theme-change broadcasts.
/// </summary>
public sealed partial class WindowsThemeNotifier : IThemeNotifier
{
    private static readonly nint BroadcastWindow = 0xffff;
    private const uint SettingChange = 0x001A;
    private const uint ThemeChanged = 0x031A;
    private const uint DwmColorizationColorChanged = 0x0320;
    private const uint AbortIfHung = 0x0002;
    private const uint TimeoutMilliseconds = 5000;

    public void NotifyThemeChanged()
    {
        _ = SendMessageTimeoutString(
            BroadcastWindow,
            SettingChange,
            0,
            "ImmersiveColorSet",
            AbortIfHung,
            TimeoutMilliseconds,
            out _);
        _ = SendMessageTimeout(
            BroadcastWindow,
            ThemeChanged,
            0,
            0,
            AbortIfHung,
            TimeoutMilliseconds,
            out _);
    }

    public void NotifyColorPrevalenceChanged()
    {
        NotifyThemeChanged();
        _ = SendMessageTimeout(
            BroadcastWindow,
            DwmColorizationColorChanged,
            0,
            0,
            AbortIfHung,
            TimeoutMilliseconds,
            out _);
    }

    [LibraryImport("user32.dll", EntryPoint = "SendMessageTimeoutW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint SendMessageTimeoutString(
        nint window,
        uint message,
        nuint wParam,
        string lParam,
        uint flags,
        uint timeout,
        out nuint result);

    [LibraryImport("user32.dll", EntryPoint = "SendMessageTimeoutW")]
    private static partial nint SendMessageTimeout(
        nint window,
        uint message,
        nuint wParam,
        nint lParam,
        uint flags,
        uint timeout,
        out nuint result);
}
