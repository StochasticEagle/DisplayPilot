# Supported platforms

| Windows version | Win32 application | Win64 application |
| --- | :---: | :---: |
| Windows 7 SP1 | 🟢 | 🟢 |
| Windows 8 / 8.1 | ⚠️ | ⚠️ |
| Windows 10 | 🟢 | 🟢 |
| Windows 11 | ❌ | 🟢 |

Windows 7 uses system DPI scaling. Later Windows versions provide optional per-monitor DPI APIs. Windows theme integration is available on Windows 10 and later.

## Build outputs

| Build | Executable |
| --- | --- |
| Release / Win32 | `dist/32/DisplayPilot.exe` |
| Release / Win64 | `dist/64/DisplayPilot.exe` |

Object files and other build intermediates are placed under `build/32` and `build/64`. Installers are compiled from `installer/DisplayPilot.iss` into `installers/32` and `installers/64`.

## Dependencies

DisplayPilot uses the Windows Shell, User32, GDI, common controls, COM/WMI, DXVA2 monitor configuration, and Registry APIs. Release builds statically link the MSVC runtime. Application icons and the manifest are embedded in the executable.
