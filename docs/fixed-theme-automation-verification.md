# Fixed theme automation verification

This checkpoint adds opt-in automatic Light and Dark theme switching while
DisplayPilot is running. It uses the last successfully saved fixed-hours schedule
and arms one Windows thread-pool timer for the next schedule boundary.

## Scope and safety

- Automatic switching is disabled by default, including migrated version-1 settings.
- Enabling or disabling automation takes effect only after **Save schedule** succeeds.
- Unsaved picker edits affect Preview only and never affect the running scheduler.
- When enabled, the saved schedule is evaluated immediately and a one-shot timer is
  armed for the exact next boundary. After it fires, the following boundary is armed.
- Returning to the app re-evaluates the schedule and re-arms the boundary timer.
- A boundary crossed during sleep is handled when Windows resumes the timer callback.
- Manual **Light** or **Dark** actions override the schedule until its next boundary.
- The setting persists, but automation runs only while DisplayPilot is open.
- No startup registration, tray/background lifetime, or Windows service is included.
- DST or time-zone changes exactly on a boundary remain intentionally out of scope.
- Scheduled theme changes do not issue DDC/CI or WMI monitor commands.

## Windows test

1. Start with automatic switching off and confirm no theme change occurs.
2. Set Light one minute before the current time and Dark two minutes after it,
   enable automatic switching, and choose **Save schedule**.
3. Confirm the current scheduled theme is applied and the next boundary changes it
   when the selected minute begins.
4. While automation is enabled, change a picker without saving and confirm it does
   not affect the automatic transition.
5. During a scheduled interval, use the opposite manual theme button and confirm the
   manual choice remains until the next saved schedule boundary.
6. Restart DisplayPilot and confirm the enabled state and saved times return and are
   evaluated immediately.
7. Disable automatic switching, save, and confirm no later transition occurs.
8. Confirm display discovery, brightness control, and **Copy diagnostic report**
   continue to work normally.
9. Copy the diagnostic report and confirm **Schedule timer active** is `True` and
   **Schedule timer due** matches the next saved boundary while automation is enabled.

Run the sequence on both Windows 10 and Windows 11 before merging this checkpoint.
