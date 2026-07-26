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

1. Leave **Start DisplayPilot when users sign in** selected in the installer.
2. Complete installation, close DisplayPilot, and sign out or reboot.
3. Confirm that DisplayPilot starts in the notification area without opening
   a window.
4. Open **Advanced** and select **Open Windows Startup settings**.
5. Confirm that DisplayPilot appears in **Settings > Apps > Startup**.
6. Turn DisplayPilot off in Windows Settings and confirm that it does not
   start after the next sign-in.
7. Turn it on again and confirm that startup resumes for this account.

Repeat the installation with the installer option cleared:

1. Confirm that the machine-wide DisplayPilot startup entry is removed.
2. Sign out and sign in.
3. Confirm that DisplayPilot does not start automatically.

## Brightness response

1. Open the notification-area flyout on hardware with validated DDC/CI or WMI
   brightness control.
2. Move the brightness slider through several values.
3. Confirm that changes begin after approximately 30 milliseconds and that
   rapid slider movement does not leave stale writes queued.

## Uninstall

1. Enable DisplayPilot in Windows Startup settings.
2. Uninstall DisplayPilot from Windows Settings.
3. Confirm that the application files, Start menu shortcut, and DisplayPilot
   machine-wide startup entry are removed.
4. Confirm that the shared system runtimes remain available for other
   applications.
