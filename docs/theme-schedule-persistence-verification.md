# Theme schedule persistence verification

This checkpoint persists the custom fixed-hours Light and Dark transition times in
the current user's local application-data folder. The settings file uses a versioned
JSON schema and stores minute-of-day values without device identifiers.

## Scope and safety

- Missing settings use 07:00 for Light and 19:00 for Dark without creating a file.
- **Save schedule** explicitly writes the selected times.
- Saved times are restored the next time DisplayPilot starts.
- Invalid, unsupported, or ambiguous saved settings fall back to safe defaults.
- DST or time-zone changes occurring exactly on a schedule boundary are intentionally
  out of scope; evaluation uses the current local wall-clock time.
- No timer, background task, startup task, or automatic theme write is included.
- Loading, previewing, and saving a schedule do not issue DDC/CI or WMI commands.

## Windows test

1. Start DisplayPilot without an existing settings file and confirm it shows the
   default 07:00 and 19:00 times.
2. Select distinct custom times, choose **Save schedule**, and close DisplayPilot.
   Confirm both selected values are shown with hours and minutes in the preview.
3. Reopen DisplayPilot and confirm both custom times were restored.
4. Change the pickers without saving, restart, and confirm the last saved values return.
5. Set equal times and verify both preview and save reject the schedule.
6. Confirm manual theme switching, display discovery, brightness control, and
   **Copy diagnostic report** still work normally.

Run the sequence on both Windows 10 and Windows 11 before merging this checkpoint.
