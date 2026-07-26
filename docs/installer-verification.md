# Installer and startup verification

Test the Release installer artifact on Windows 10 and Windows 11 x64.

## Install

1. Download `DisplayPilot-Setup-win-x64.exe` from the workflow artifacts.
2. Run the installer and approve the prerequisite elevation request.
3. Confirm that DisplayPilot is installed under Program Files.
4. Confirm that the .NET 10 Desktop, Windows App SDK 2.3, and Microsoft Visual
   C++ runtimes are installed system-wide rather than under AppData.
5. Confirm that the Start menu shortcut uses the DisplayPilot icon.
6. Launch DisplayPilot and verify the notification-area icon, flyout, and
   Advanced interface.

## Start at sign-in

1. Leave **Start DisplayPilot for all users when they sign in** selected in
   the installer.
2. Complete installation, close DisplayPilot, and sign out or reboot.
3. Sign into each test account and confirm that DisplayPilot starts in the
   notification area without opening a window.
4. Open **Advanced** and select **Open Windows Startup settings**.
5. Confirm that DisplayPilot appears in **Settings > Apps > Startup**.
6. Confirm that changing the setting does not request administrator elevation.
7. Turn DisplayPilot off in Windows Settings and confirm that it does not
   start after the next sign-in for that account.
8. Confirm that DisplayPilot remains enabled for another account.
9. Turn it on again and confirm that startup resumes for the first account.

Windows may label DisplayPilot as a **High impact** startup application. This
classification is based on Windows startup-impact measurements and is accepted;
DisplayPilot does not attempt to override it.

Repeat the installation with the installer option cleared:

1. Confirm that each account's DisplayPilot startup shortcut is removed.
2. Sign out and sign in.
3. Confirm that DisplayPilot does not start automatically.

## Brightness response

1. Open the notification-area flyout on hardware with validated DDC/CI or WMI
   brightness control.
2. Move the brightness slider through several values.
3. Confirm that changes begin after approximately 30 milliseconds and that
   rapid slider movement does not leave stale writes queued.

## Uninstall

1. Enable DisplayPilot in Windows Startup settings and leave DisplayPilot running.
2. Uninstall DisplayPilot from Windows Settings.
3. Confirm that DisplayPilot closes during uninstall.
4. Confirm that the application directory, Start menu shortcut, and per-user
   DisplayPilot startup shortcuts are removed.
5. Confirm that the shared system runtimes remain available for other
   applications.
