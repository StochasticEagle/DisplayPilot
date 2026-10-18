# Verification

## Build matrix

Build Debug and Release from `DisplayPilot.sln` using Visual Studio 2026 and MSVC v145 for Win32 and Win64. Release executables appear in `dist/32` and `dist/64`. The CI workflow also compiles the two Inno Setup installers.

## Windows platforms

Test Windows 7 SP1 in 32-bit and 64-bit configurations, Windows 10 in both configurations, and Windows 11 in 64-bit configuration. Windows 8/8.1 and running the Win32 build on Windows 11 are not in the standard test matrix.

## Monitor tests

Exercise WMI brightness on internal panels, external DDC/CI brightness and contrast, monitor-specific VCP presets, mixed displays, disconnect/reconnect, rotation rejection, sleep/resume, brightness-key events, and read-back verification.

## User interface

Test tray hover and dismissal, native right-click menu, flyout focus behavior, sliders, Advanced window resizing, keyboard interaction, multiple taskbar positions, and 4K displays.

## Scheduling

Test fixed hours, solar calculations at several latitudes, midnight crossings, daylight-saving transitions, manual overrides, restart recovery, and brightness restoration.

## Installer

Verify separate architecture installers, application launch, registration at sign-in, upgrades, and uninstall.
