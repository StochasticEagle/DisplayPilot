# DisplayPilot Architecture

DisplayPilot is a standalone Windows 10 and Windows 11 tray application combining
theme automation with monitor management.

## Boundaries

- `DisplayPilot.Core`: scheduling, profiles, settings models, and domain logic.
- `DisplayPilot.Display`: monitor discovery and control through DDC/CI and WMI.
- `DisplayPilot.Windows`: Windows theme, notification-area shell integration,
  registry, scheduling, and session integration.
- `DisplayPilot.App`: compact WinUI 3 notification-area flyout and Advanced
  verification/settings interface.
- `DisplayPilot.Core.Tests`: platform-independent unit tests.

## Constraints

- x64 only.
- Windows 10 version 1809 or newer.
- Windows 11 supported.
- Per-user operation without a required Windows service.
- No dependency on the PowerToys runner or PowerToys Settings application.
- Hardware operations remain capability-driven because DDC/CI implementations vary.
- Notification-area callbacks use a dedicated hidden Win32 window. Native
  callbacks queue commands onto the WinUI dispatcher and never mutate the
  flyout directly from the shell callback.

## Extraction policy

PowerToys source is imported selectively. Each imported component must:

1. Retain its original Microsoft copyright header.
2. Be placed behind a DisplayPilot-owned interface.
3. Remove dependencies on PowerToys runner IPC, telemetry, GPO, and settings infrastructure.
4. Receive focused tests before functional changes are introduced.
