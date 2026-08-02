# Brightness-key verification

DisplayPilot 0.9.1 observes the Windows `WmiMonitorBrightnessEvent` generated
when a supported laptop or tablet changes its integrated-panel brightness. It
mirrors the resulting absolute percentage to each external display with a
validated DDC/CI brightness path. Windows remains responsible for the integrated
panel, so DisplayPilot does not install a keyboard hook or adjust that panel a
second time.

## Expected behavior

- Brightness Up and Brightness Down work while DisplayPilot is running in the
  notification area; its flyout does not need to be open.
- The integrated panel changes once, using the level selected by Windows.
- Each readable external DDC/CI monitor follows the integrated panel's new
  percentage.
- Holding a key does not create a delayed backlog of monitor writes.
- DisplayPilot's per-monitor slider remains per-monitor; an application-originated
  WMI write is suppressed from keyboard synchronization.
- Unsupported displays and virtual display paths are not written.

## HP hardware test

1. Start DisplayPilot and confirm its notification-area icon is present.
2. Open the flyout once and confirm the internal panel and any attached external
   DDC/CI monitor have readable brightness controls.
3. Close the flyout, press Brightness Down once, then reopen the flyout.
4. Confirm the internal panel changed once and each external display reports the
   same percentage.
5. Repeat with Brightness Up.
6. Hold each brightness key long enough to generate repeated input. Confirm the
   values continue in the correct direction and settle promptly after release.
7. Use DisplayPilot's slider for only the internal panel. Confirm external monitor
   values do not follow that application-originated change.
8. Copy the diagnostic report and confirm:
   - `Brightness-key watcher active` is `True`.
   - `Brightness-key events` increases after hardware-key use.
   - The last percentage, DDC target count, and DDC success count match the test.

On a VM or desktop with no WMI-controlled integrated panel, the watcher may be
unavailable or receive no events. That is expected and does not affect ordinary
DDC/CI slider control.
