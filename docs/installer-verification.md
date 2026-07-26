# Installer and startup verification

Test the Release installer artifact on Windows 10 and Windows 11 x64.

## Install

1. Download `DisplayPilot-Setup-win-x64.exe` from the workflow artifacts.
2. Run the installer as a standard user.
3. Confirm that installation does not require elevation.
4. Confirm that the Start menu shortcut uses the DisplayPilot icon.
5. Launch DisplayPilot and verify the notification-area icon, flyout, and
   Advanced interface.

## Start at sign-in

1. Open **Advanced**.
2. Turn on **Start DisplayPilot when I sign in**.
3. Close DisplayPilot and sign out or reboot.
4. Sign in and confirm that DisplayPilot starts in the notification area
   without opening a window.
5. Open **Advanced**, turn the option off, and confirm that DisplayPilot does
   not start after the next sign-in.

## Brightness response

1. Open the notification-area flyout on hardware with validated DDC/CI or WMI
   brightness control.
2. Move the brightness slider through several values.
3. Confirm that changes begin after approximately 30 milliseconds and that
   rapid slider movement does not leave stale writes queued.

## Uninstall

1. Enable **Start DisplayPilot when I sign in**.
2. Uninstall DisplayPilot from Windows Settings.
3. Confirm that the application files, Start menu shortcut, and DisplayPilot
   startup registration are removed.
