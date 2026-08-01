// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using System.Runtime.InteropServices;

namespace DisplayPilot.Display.Interop;

internal static partial class DisplaySettingsPInvoke
{
    [LibraryImport("user32.dll", EntryPoint = "EnumDisplaySettingsW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EnumDisplaySettings(
        string deviceName,
        int modeNumber,
        ref DevMode deviceMode);

    [LibraryImport("user32.dll", EntryPoint = "ChangeDisplaySettingsExW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    internal static partial int ChangeDisplaySettingsEx(
        string deviceName,
        ref DevMode deviceMode,
        nint window,
        uint flags,
        nint parameter);
}
