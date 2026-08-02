# Brightness-key verification

DisplayPilot 0.9.1 registers the notification-area window for Raw Input from the
HID Consumer Control collection. It recognizes the standardized Brightness
Increment (`0x006F`) and Brightness Decrement (`0x0070`) usages even when they do
not appear as ordinary keyboard keys. Each command adjusts the validated WMI or
DDC/CI display containing the mouse pointer by 10 percentage points. If Windows successfully changes
the integrated panel and emits `WmiMonitorBrightnessEvent`, that absolute result
is used instead so the panel is not adjusted twice.

## Expected behavior

- Brightness Up and Brightness Down work while DisplayPilot is running in the
  notification area; its flyout does not need to be open.
- Only the display containing the mouse pointer changes by 10 percentage points,
  clamped to 0–100%.
- If Windows changes the integrated panel first, DisplayPilot mirrors that actual
  percentage to external displays without applying another step.
- Holding a key does not create a delayed backlog of monitor writes.
- DisplayPilot's per-monitor slider remains per-monitor; an application-originated
  WMI write is suppressed from keyboard synchronization.
- Unsupported displays and virtual display paths are not written.

## HP hardware test

1. Start DisplayPilot and confirm its notification-area icon is present.
2. Open the flyout once and confirm the internal panel and any attached external
   DDC/CI monitor have readable brightness controls.
3. Close the flyout, press Brightness Down once, then reopen the flyout.
4. Confirm only the display containing the pointer changed.
5. Repeat with Brightness Up.
6. Move the pointer to each available monitor and confirm the keys control only
   that monitor.
7. Confirm the Windows brightness bar is synchronized to the actual value of the
   cursor's monitor after startup and after each key action.
8. Hold each brightness key long enough to generate repeated input. Confirm the
   values continue in the correct direction and settle promptly after release.
9. Use DisplayPilot's slider for only the internal panel. Confirm external monitor
   values do not follow that application-originated change.
10. Copy the diagnostic report and confirm:
   - `Brightness-key watcher active` is `True`.
   - `Raw brightness input registered` is `True`.
   - `Raw brightness input events` increases after hardware-key use.
   - `Last brightness-key target` matches the display containing the pointer.
   - `Last Windows brightness state synchronized` is `True`.
   - `Brightness-key events` increases after hardware-key use.
   - The last percentage, DDC target count, and DDC success count match the test.

On a VM or desktop with no WMI-controlled integrated panel, the watcher may be
unavailable or receive no events. That is expected and does not affect ordinary
DDC/CI slider control.
