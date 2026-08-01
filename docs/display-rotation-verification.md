# Display rotation verification

DisplayPilot exposes an independent Rotation selector on each monitor card in the
notification-area flyout. The available orientations are landscape (0 degrees),
portrait (90 degrees), landscape flipped (180 degrees), and portrait flipped
(270 degrees).

The app asks Windows to validate a requested mode before applying it. Crossing
between landscape and portrait also swaps the mode width and height so the
selected monitor retains the equivalent resolution. A successful change is saved
for the current Windows user.

## Build and run

1. Build the `Release|x64` solution configuration.
2. Start DisplayPilot and left-click its notification-area icon.
3. Confirm every active display has its own Rotation selector and Apply button.

## Single-monitor checks

1. Record the current Windows display resolution and orientation.
2. Apply portrait and confirm the desktop rotates 90 degrees without changing the
   effective resolution.
3. Apply landscape flipped and portrait flipped, confirming each orientation.
4. Return the display to landscape before completing the test.
5. Close and reopen the flyout after each change and confirm the selector reports
   the current Windows orientation.
6. Restart or sign out and confirm the last selected orientation persists.
7. Copy the diagnostic report and confirm it contains the display's Windows name,
   rotation status, current rotation, and the last write result.

## Multi-monitor checks

When two or more monitors are available:

1. Record each monitor's orientation.
2. Change one monitor and confirm no other monitor rotates.
3. Change a second monitor to a different orientation and confirm both selectors
   remain independently synchronized after reopening the flyout.
4. Return all monitors to their original orientations.

If Windows rejects an orientation during validation, DisplayPilot should report
the failure and leave the display unchanged.
