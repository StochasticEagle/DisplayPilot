# DisplayPilot

DisplayPilot is a native C++ Windows notification-area application for monitor controls and appearance scheduling.

## Build

Before first opening the solution, register its Win64 C++ platform with Visual Studio. Run **Command Prompt as administrator** in the repository root:

```cmd
.\tools\Register-Win64Platform.bat
```

The registration uses the installed 64-bit MSVC platform files without changing them. It is idempotent and does not modify the Win32 or Win64 project configurations. Restart Visual Studio if the solution was already open.

Open `DisplayPilot.sln` in Visual Studio 2026 with MSVC v145 and the Windows SDK installed. Select Debug or Release and Win32 or Win64, then select **Build Solution**.

| Platform | Release output |
| --- | --- |
| Win32 | `dist/32/DisplayPilot.exe` |
| Win64 | `dist/64/DisplayPilot.exe` |

Command-line builds from a Visual Studio Developer Command Prompt:

```cmd
msbuild DisplayPilot.sln /m /p:Configuration=Release /p:Platform=Win32
msbuild DisplayPilot.sln /m /p:Configuration=Release /p:Platform=Win64
```

Release executables use the static MSVC runtime and embed application resources. Separate installers are built with `installer/DisplayPilot.iss`.

## Features

The notification-area icon opens a modeless native Win32 monitor-control flyout. A native context menu provides quick commands and access to the separate Advanced window. Monitor controls use DDC/CI, WMI, and Windows display APIs. Fixed-time and solar scheduling are available with manually entered coordinates or optional Windows location reports. Automatic location subscriptions are active only while solar scheduling and automatic location are enabled; Update location now performs a user-requested one-time lookup. Location access is never required for startup or manual settings. The native and WinUI applications read and write the same `%LOCALAPPDATA%\DisplayPilot\settings.json` format.

## Supported Windows versions

Windows 7 SP1, Windows 10, and Windows 11 are covered by the supported platform matrix. See [platforms](docs/platforms.md).

## Visual Studio dialog designer

The **Advanced** window and notification-area **flyout** are defined in
`src/native/app.rc` as native dialog resources. In Visual Studio, open
**View → Other Windows → Resource View**, expand `app.rc`, then **Dialog**,
and open `IDD_DISPLAYPILOT_ADVANCED` or `IDD_DISPLAYPILOT_FLYOUT` to edit
the fixed labels, buttons, edits, and combo boxes.

Monitor-specific rows are generated at runtime because their count and supported
controls depend on the connected hardware. The native layout code also
repositions resource controls for DPI, window resizing, and scrolling; changes
to resource control positions or sizes must be reflected in the corresponding
layout routines to affect those runtime positions. Resource text, control types,
styles, and tab order are editable directly in the designer.

## Documentation

[Architecture](docs/architecture.md) · [Features](docs/features.md) · [Verification](docs/verification.md)

## License

[The Unlicense](LICENSE).
