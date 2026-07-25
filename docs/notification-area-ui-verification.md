# Notification-area UI verification

DisplayPilot now runs as a Windows notification-area application. Its primary
surface is a compact brightness, theme, and schedule flyout. The previous full
window remains available as the **Advanced** interface.

## Behavior

- DisplayPilot starts with no visible window and adds an icon to the taskbar
  notification area.
- Selecting the icon opens or hides the compact flyout next to that icon.
- The flyout is kept inside the notification icon's monitor work area and is
  scaled for that window's DPI.
- The first flyout opening reads DDC/CI and WMI brightness if it has not already
  been read.
- Brightness changes remain explicit: move a slider, then select **Set**.
- Theme and saved-schedule toggles use the same services and settings as
  **Advanced**.
- Selecting **Advanced** opens the existing full interface.
- Closing the Advanced window hides DisplayPilot; it does not end automation.
- The icon context menu provides **Open DisplayPilot**, **Advanced**, and
  **Exit**. Only **Exit** terminates the process.
- The icon is restored if Windows Explorer restarts.

## Windows test

1. Launch DisplayPilot and confirm no window appears and one DisplayPilot icon
   is present in the notification area.
2. Select the icon and confirm the compact flyout opens next to it without a
   taskbar or Alt+Tab entry and is brought to the foreground.
3. Select the icon again, then open the flyout and click elsewhere; confirm both
   actions hide the flyout while the process remains active.
4. Confirm each readable display shows its current brightness. Move a slider,
   select **Set**, and verify the reported value and monitor on-screen menu agree.
5. Switch Light/Dark mode and schedule automation from the compact UI; confirm
   the Advanced UI reflects the same state.
6. Select **Advanced**, verify the existing display, schedule, brightness, and
   diagnostic controls, then close the window and confirm the icon remains.
7. Use the icon's **Open DisplayPilot** and **Advanced** commands and confirm
   the compact and full interfaces return respectively.
8. Restart Windows Explorer and confirm the DisplayPilot icon returns without
   creating a duplicate.
9. Use the icon's **Exit** command and confirm the icon and process both close.
10. Repeat on Windows 10 and Windows 11, including a non-100% display scale if
    available.
