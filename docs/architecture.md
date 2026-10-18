# DisplayPilot Architecture

DisplayPilot is a standalone Windows 10 and Windows 11 tray application combining
theme automation with monitor management.

## Boundaries

- `DisplayPilot.Core`: scheduling, settings models, and domain logic.
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
- Hardware operations remain capability-driven because DDC/CI implementations vary.
- Notification-area callbacks use a dedicated hidden Win32 window. Native
  callbacks queue commands onto the WinUI dispatcher and never mutate the
  flyout directly from the shell callback.

## Runtime design

- One application process; no independent background services.
- Event-driven display and session updates, without periodic hardware polling.
- One-shot theme scheduling at the next required transition.
- Open DDC/CI monitor handles only for bounded, requested work.
- Reuse cached monitor state and apply targeted read-back verification after writes.
- Avoid unnecessary allocations, wakeups, and full UI rebuilds while idle.
