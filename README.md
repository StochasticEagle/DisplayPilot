# DisplayPilot

[![Build](https://github.com/StochasticEagle/DisplayPilot/actions/workflows/build.yml/badge.svg?branch=master)](https://github.com/StochasticEagle/DisplayPilot/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D4.svg)](#system-requirements)
[![Release](https://img.shields.io/badge/release-0.9.1%20beta-orange.svg)](https://github.com/StochasticEagle/DisplayPilot/releases)

DisplayPilot is an open-source notification-area application for Windows 10 and
Windows 11 that combines monitor controls with automatic Light and Dark theme
scheduling. It provides a compact flyout for routine adjustments and a detailed
Advanced view for device information, configuration, and diagnostics.

DisplayPilot is a standalone application inspired by the Microsoft PowerToys
Light Switch and Power Display utilities. It does not require PowerToys and is
not affiliated with or supported by Microsoft.

## Release status

DisplayPilot 0.9.1 is a beta release. The final installer regression suite has
passed on Windows 11. Windows 10 installer regression testing remains outstanding,
and broader mixed-monitor testing is still recommended before a 1.0 release.

## Features

### Display control

- Adjust external-monitor brightness and contrast through DDC/CI.
- Adjust supported internal-panel brightness through Windows WMI.
- Select supported DDC/CI color-temperature presets.
- Change display orientation independently for each active monitor.
- Detect multiple active display paths and expose controls according to each
  monitor's reported capabilities.
- Apply brightness and contrast changes dynamically in 1% increments.
- Route supported brightness-key changes to the display containing the mouse
  pointer and synchronize the Windows brightness state to its actual value.
- Refresh monitor state whenever the notification-area flyout opens.

Unsupported controls are hidden. DisplayPilot verifies monitor writes by reading
the resulting value back whenever the underlying interface permits it.

### Theme scheduling

- Switch Windows and application themes together between Light and Dark modes.
- Use fixed Light and Dark transition times with one-minute precision.
- Calculate sunrise and sunset locally from latitude, longitude, date, and the
  current Windows time zone.
- Enter coordinates manually with an optional descriptive label, or request a
  one-time position through the Windows location service.
- Handle polar day and polar night without inventing transition times.
- Optionally reduce controllable displays during Dark hours and restore each
  display's exact recorded brightness at the Light boundary.
- Preserve schedules and brightness-restoration state in per-user settings.

Solar calculations do not use third-party web services. Schedule boundaries are
implemented with a Windows timer rather than periodic polling.

### Windows integration

- Notification-area icon with a compact flyout.
- Right-click menu for the Advanced view and application exit.
- Optional start-at-sign-in registration, configurable per Windows user.
- Single-instance operation.
- Inno Setup installer with clean replacement of an existing installation.
- Diagnostic reporting for display discovery, control paths, scheduling, and
  notification-area behavior.

## System requirements

| Requirement | Supported configuration |
| --- | --- |
| Operating system | Windows 10 version 1809 or later; Windows 11 |
| Architecture | x64 |
| Installation | Administrator approval is required for Program Files and shared runtime installation |
| External monitor control | Monitor and connection must support DDC/CI |
| Internal-panel brightness | Hardware and driver must expose Windows WMI brightness control |

ARM64 is not currently supported.

## Installation

1. Open the [DisplayPilot releases page](https://github.com/StochasticEagle/DisplayPilot/releases).
2. Select the most recent beta release and download `DisplayPilot-Setup-win-x64.exe`.
3. Optionally verify the installer with the accompanying `.sha256` checksum.
4. Run the installer and choose whether DisplayPilot should start when users sign in.

The installer places DisplayPilot under Program Files and installs the required
.NET Desktop, Windows App SDK, and Microsoft Visual C++ runtimes system-wide.
Per-user settings remain under the user's local application-data directory.

The installer and executable are not digitally signed. Windows may therefore
display an unrecognized-publisher or SmartScreen warning. Review the source and
build workflow before proceeding if this is unsuitable for your environment.

## Basic use

- Left-click the DisplayPilot notification-area icon to open the compact flyout.
- Right-click the icon to open **Advanced** or exit DisplayPilot.
- Move an available slider to change that monitor immediately.
- Enable theme scheduling and select either **Fixed times** or
  **Sunrise and sunset**, then save the schedule.
- Use **Advanced** for detailed display paths, startup settings, schedule status,
  and the diagnostic report.

DDC/CI support varies by monitor, cable, adapter, dock, graphics driver, and input.
If a control is absent, first confirm that DDC/CI is enabled in the monitor's
on-screen menu and test a direct display connection where practical.

## Privacy and network behavior

- Display and theme control is performed locally.
- Manual solar scheduling stores coordinates only in the per-user settings file.
- **Use Windows location** requests a position from the Windows location service
  only when selected by the user.
- Copied diagnostics omit the saved location label and coordinates.
- Diagnostics do contain stable display paths and WMI instance names that may
  identify a local hardware instance; review reports before sharing them.
- DisplayPilot contains no application telemetry and does not use third-party
  services for solar calculations.

## Current scope and limitations

- DDC/CI behavior is hardware-dependent and some monitors require retries.
- Virtual displays generally do not expose DDC/CI or WMI brightness control.
- Multi-monitor logic is implemented, but broader testing across mixed monitor,
  adapter, dock, and DPI configurations is still desirable.
- The complete installer regression suite has passed on Windows 11; the equivalent
  Windows 10 regression run is pending.
- Theme automation runs while DisplayPilot is active; start-at-sign-in keeps it
  available across normal desktop sessions.
- Volume, monitor input switching, display power control, and saved monitor
  profiles are intentionally outside the current scope.
- Changes made by directly editing the JSON settings file are loaded on the next
  application start; live file watching is not supported.

## Build from source

Building requires Windows x64 and the .NET 10 SDK.

```powershell
git clone https://github.com/StochasticEagle/DisplayPilot.git
cd DisplayPilot
dotnet restore DisplayPilot.slnx
dotnet build DisplayPilot.slnx --configuration Release --no-restore
dotnet test DisplayPilot.slnx --configuration Release --no-build
```

To create the framework-dependent application directory:

```powershell
dotnet publish src/DisplayPilot.App/DisplayPilot.App.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained false `
  --output artifacts/DisplayPilot-win-x64
```

The complete CI packaging process, including runtime verification and Inno Setup
installer generation, is defined in [`.github/workflows/build.yml`](.github/workflows/build.yml).

## Project structure

| Project | Responsibility |
| --- | --- |
| `DisplayPilot.App` | WinUI 3 flyout, Advanced interface, and application lifecycle |
| `DisplayPilot.Core` | Theme schedules, solar calculations, and domain logic |
| `DisplayPilot.Display` | Display discovery, DDC/CI, WMI, MCCS, and rotation |
| `DisplayPilot.Windows` | Windows themes, timers, settings, shell integration, and startup |
| `tests` | Unit and platform-focused automated tests |

See [Architecture](docs/architecture.md) for component boundaries and source
extraction policy.

## Verification documentation

- [Display discovery and control](docs/display-path-verification.md)
- [Brightness keyboard buttons](docs/brightness-key-verification.md)
- [Notification-area interface](docs/notification-area-ui-verification.md)
- [Display rotation](docs/display-rotation-verification.md)
- [Theme switching](docs/theme-verification.md)
- [Fixed-time scheduling](docs/fixed-theme-automation-verification.md)
- [Sunrise and sunset scheduling](docs/sunrise-sunset-schedule-verification.md)
- [Schedule persistence](docs/theme-schedule-persistence-verification.md)
- [Installer and startup](docs/installer-verification.md)

## License and attribution

DisplayPilot is distributed under the [MIT License](LICENSE). Portions are based
on Microsoft PowerToys source licensed under MIT. See [Notices](NOTICE.md) and
[Third-party notices](THIRD-PARTY-NOTICES.md) for attribution.

Security reports and contribution requirements are documented in
[Security policy](SECURITY.md) and [Contributing](CONTRIBUTING.md).
