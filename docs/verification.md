# Native DisplayPilot validation

## Build matrix

Build directly from DisplayPilot.sln using Visual Studio 2026 on Windows. Test Release/Debug for Win32 and x64; only `DisplayPilot.exe` is distributed from `dist/32` and `dist/64`. The generated executable should import Windows system DLLs only; no .NET, third-party DLLs or runtime asset directories.

## OS acceptance

- Windows 7 SP1 **32-bit**: required minimum acceptance system.
- Windows 7 SP1 **64-bit**: required.
- Windows 10 **32-bit and 64-bit**: required.
- Windows 11 **64-bit**: required. Running the 32-bit executable is untested and unblocked.
- Windows 8 and 8.1: untested and unblocked.

Windows 7 appearance controls are hidden, but display and brightness schedule controls remain usable. No modal dialog or separate service may appear as part of normal operation.

## Hardware matrix

Exercise built-in laptops (WMI only), one external DDC/CI monitor, several external monitors with different supported VCP lists, and mixed internal/external setups. Test with DDC/CI disabled, unreachable monitors, stale handles, unplugging while a slider is dragged, rotation rejection, mirrored displays and suspended/resumed hardware.

Ensure write operations reflect monitor capability and that control failure never changes another monitor. Verify DDC/CI read-back, WMI read-back, and the ability to revert an automated brightness reduction after an unexpected termination.

## Flyout and Advanced UX

Verify tray icon hover and delayed dismissal; right click opens a standard context menu; flyout never activates or blocks other windows; Advanced is an independently movable top-level window in the same process. Test scroll lists, keyboard/mouse input, multiple monitor DPI and 4K work areas. Test Explorer restart and startup at Windows sign-in.

## Automation

Test time-change notifications, midnight crossings, spring/fall daylight saving transitions, manual coordinates, sunrise/sunset including high latitudes, and an unavailable location provider. Manual overrides must not be lost to the automation timer. A restart must not leave a reduced display permanently dimmed.

## Installer

Build separate 32 and 64 Inno Setup installers, installing exactly DisplayPilot.exe. Validate cleanup on uninstall, no leftover services, no added runtime folders and no spurious external helper executable.
