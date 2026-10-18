# DisplayPilot native architecture

The native application uses only original C++17 under `src/native`, built directly with `DisplayPilot.sln` and `DisplayPilot.vcxproj` through Visual Studio/MSBuild. No CMake, .NET or third-party runtime libraries.

## Process and UI

`main.cpp` owns the sole process and Win32 message loop, hidden notification-area owner, Shell_NotifyIcon, recovery after Explorer restarts, a single-instance mutex, and timers. The main flyout is a separate **modeless, nonactivating** Win32 `WS_POPUP` window using Direct2D/DirectWrite; it is not a dialog. The right-click menu is a Win32 HMENU. `advanced.cpp` uses a normal resizeable top-level window and common controls; no other executable or process is started.

Flyout controls change device values only through the shared monitor backend and receive refreshes on the UI message loop. Persistent preferences use HKCU Registry values; no JSON or other loose resource files are read.

## Monitor backend

`display.cpp` uses EnumDisplayMonitors, physical monitor handles, and DXVA2 DDC/CI. It determines brightness, contrast, and color-control availability from the connected hardware. Color preset choices are constrained to those reported in the monitor's VCP capabilities string. Monitor handles are destroyed after each operation. Rotation uses EnumDisplaySettingsEx and ChangeDisplaySettingsEx with CDS_TEST before updates, with immediate mode read-back.

Laptop/internal brightness uses Windows-provided WMI COM interfaces in ROOT\\WMI; no System.Management dependency or third-party library. WmiMonitorBrightnessEvent notifications are drained nonblockingly by the existing UI message loop, without spawning a worker thread or helper process. WMI monitor instance mapping is only attached to a display where its hardware identity can be matched; otherwise it appears as a separate WMI endpoint rather than silently controlling another panel.

## Scheduling and settings

`settings.cpp` reads/writes HKCU\\Software\\DisplayPilot\\Settings, calculates fixed-time or sunrise/sunset boundaries (using manual latitude/longitude), and applies Windows 10+ system theme changes. Brightness reduction can run independently on Windows 7. Previously selected levels are stored in HKCU\\Software\\DisplayPilot\\Restoration and restored on schedule exit or app termination. Windows 7 appearance controls are hidden.

## Deployment

Visual Studio 2026 uses MSVC v145, C++17, and static CRT linking (`/MT` for Release), linking against Windows-supplied DLLs only. The Windows 7 SP1 imported-API baseline and subsystem version are set in the project. Higher-version DPI functions are resolved dynamically. Output is exactly `dist/32/DisplayPilot.exe` or `dist/64/DisplayPilot.exe`. PDBs and obj files are not installed.

The existing icons and license are the only reused resources. See [feature parity](feature-parity.md) and [verification](verification.md); the native rebuild is not feature-complete.
