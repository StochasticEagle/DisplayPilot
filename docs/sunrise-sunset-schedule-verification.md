# Sunrise/sunset schedule verification

DisplayPilot can calculate sunrise and sunset locally from a latitude, longitude,
date, and the current Windows time zone. It does not send coordinates to an online
service. Coordinates may be entered manually with an optional descriptive label,
or requested once through the Windows location service.

## Manual-location checks

1. Enable theme scheduling and select **Sunrise and sunset**.
2. Enter a familiar location using decimal coordinates. Confirm north/east values
   are positive and south/west values are negative. Add an optional label.
3. Save the schedule and confirm the preview shows plausible local sunrise and
   sunset times for the current date and Windows time zone.
4. Restart DisplayPilot and confirm the schedule type, coordinates, and label are
   restored. The fixed-time values should remain available when switching back to
   **Fixed times**.
5. Enter a missing or out-of-range coordinate and confirm the schedule is not saved.

## Windows-location checks

1. Select **Use Windows location** while DisplayPilot is in the foreground.
2. Grant location access if Windows asks. Confirm both coordinate fields are filled,
   the optional label becomes `Windows location`, and an accuracy estimate appears.
3. Deny or disable Windows location access and confirm DisplayPilot remains open and
   directs the user to enter coordinates manually.
4. Confirm no network access is required by DisplayPilot for either calculation path.

## Automation and boundary checks

1. Temporarily use coordinates and a Windows time zone where a solar boundary is
   near the current time. Confirm the theme changes at the computed boundary.
2. If **Reduce screen brightness** is enabled, confirm sunset records and reduces
   each controllable display's brightness, and sunrise restores the exact recorded
   values.
3. Change the Windows time zone and reopen the flyout. Confirm the displayed solar
   times and next boundary use the new time zone.
4. Test a high-latitude location on dates with polar day and polar night. Confirm
   DisplayPilot holds Light or Dark mode respectively and recalculates after local
   midnight.
5. Copy the diagnostic report and confirm it contains the schedule mode, Windows
   time zone, solar condition, and calculated times. The report also contains the
   saved coordinates and should be reviewed or redacted before sharing.

Run the applicable checks on Windows 10 and Windows 11 before merging this checkpoint.
