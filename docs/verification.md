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

## Settings compatibility

The native application uses the same `%LOCALAPPDATA%\DisplayPilot\settings.json` schema (versions 1–4) as the WinUI application. The CI compatibility test loads saved fixed and solar schedules, UTF-8 location labels, automation and brightness flags, and monitor restoration values; verifies case-insensitive monitor keys; and checks that saving preserves restoration state and unknown JSON properties. Malformed files must remain untouched.

## Installer

Verify separate architecture installers, application launch, registration at sign-in, upgrades, and uninstall.

## Native parity regression scenarios

- On raw HID Consumer Control brightness-up/down input (usages `0x006f` and
  `0x0070`), the monitor under the cursor changes in steps of ten and the
  Windows power-plan brightness state is synchronized without repeated writes.
- WMI brightness events update the integrated panel and avoid echoing recent
  raw-input adjustments. Session lock/disconnect suspends background monitor
  notifications; unlock and reconnect refresh monitor inventory and scheduling.
- Rapid trackbar dragging coalesces monitor writes on a serialized background
  worker; late results from superseded writes do not overwrite newer slider
  values, and a failed read-back does not overwrite the last verified value.
- An unverified night-brightness write must retain its original restoration
  value, including across a restart or monitor disconnect. Manual adjustments
  must also preserve the pre-schedule baseline until restoration.
- Invalid JSON settings or failed persistence must be reported, without
  overwriting or removing the original file. Equal schedule transition times
  must be rejected before saving.
- The flyout's expandable schedule editor can save fixed schedules, manually
  entered solar coordinates, and optional brightness reduction. Denied Windows
  location never prevents manual schedule operation.
- Display rescan and refresh actions, rotation, theme, scheduled preview,
  second-instance activation, and no-tray fallback are exercised on real
  hardware; normal CI smoke tests cannot substitute for interaction testing.
