# Custom theme schedule verification

This checkpoint adds a platform-independent fixed-hours evaluator based on the
PowerToys Light Switch boundary behavior. The app previews which theme a custom
schedule calls for at the current local time and shows the next transition.

## Scope and safety

- The default preview uses Light at 07:00 and Dark at 19:00.
- The two times can be changed in memory and previewed immediately.
- The evaluator supports schedules that wrap across midnight.
- Equal transition times are rejected as ambiguous.
- Schedule settings are not persisted yet.
- No timer, background task, startup task, or automatic theme write is included.
- Previewing a schedule does not issue theme, DDC/CI, or WMI writes.

## Windows test

1. Start DisplayPilot and confirm the 07:00/19:00 preview matches the current local time.
2. Set Light a few minutes before the current time and Dark a few minutes after it;
   the preview should report Light and identify the Dark transition.
3. Reverse those times to create a midnight-wrapping schedule and verify the result.
4. Set both times equal and verify DisplayPilot reports that they must differ.
5. Select **Light** and **Dark** manually and confirm their existing behavior is unchanged.
6. Confirm brightness controls and **Copy diagnostic report** still work normally.

Run the sequence on both Windows 10 and Windows 11 before merging this checkpoint.
