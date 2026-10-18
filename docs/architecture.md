# Architecture

DisplayPilot is a native C++17 desktop program built from `DisplayPilot.sln` and `DisplayPilot.vcxproj`.

## Components

| Source | Responsibilities |
| --- | --- |
| `src/native/main.cpp` | Tray icon, hidden owner window, timers, window messages, scheduling |
| `src/native/flyout.cpp` | Modeless flyout with native Win32 controls |
| `src/native/advanced.cpp` | Advanced window and monitor controls |
| `src/native/display.cpp` | Display enumeration, DDC/CI, WMI brightness, rotation |
| `src/native/wmi_events.cpp` | Brightness event polling |
| `src/native/settings.cpp` | Registry settings, time calculations, brightness restoration |
| `src/native/app.rc` | Embedded icon, version information, manifest |

A single Win32 message loop handles the tray, native popup menu, flyout, and Advanced window. The flyout uses native trackbars, combo boxes, radio buttons, and checkboxes, with system fonts and Windows-version-specific window chrome. WMI brightness notifications are polled without blocking the message loop.

External monitors are controlled using the DXVA2 physical-monitor APIs. Internal-panel brightness uses COM and `ROOT\WMI`. Display orientation is applied after validating the proposed mode with Windows display configuration functions.

Persistent preferences and scheduled-brightness restoration state share the original WinUI versioned JSON file at `%LOCALAPPDATA%\DisplayPilot\settings.json`. Existing native Registry values are used only when that file is absent. Scheduling supports fixed times and sunrise/sunset using configured coordinates.

## Compilation

Visual Studio 2026 builds Win32 and Win64 configurations using MSVC v145 and the Windows SDK. The minimum application subsystem baseline is Windows 7 SP1. Release builds use static CRT linking and write executables to `dist/32` and `dist/64`.
