# Theme verification

This checkpoint validates DisplayPilot's first extracted PowerToys Light Switch
behavior on Windows 10 and Windows 11. It reads and writes these per-user values:

- `AppsUseLightTheme`
- `SystemUsesLightTheme`
- `ColorPrevalence` (reset to the Windows default only when changing the system to light)

The values are stored under
`HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize`. After a
change, DisplayPilot broadcasts the same `WM_SETTINGCHANGE`, `WM_THEMECHANGED`, and
colorization notifications used by PowerToys Light Switch.

## Scope

- Theme state is read at startup and when **Refresh** is selected.
- **Light** explicitly applies the light theme to both Windows and applications.
- **Dark** explicitly applies the dark theme to both Windows and applications.
- Registry values are read back immediately and the result is shown in the app.
- No schedule, sunrise/sunset lookup, Night Light integration, hotkey, startup task,
  background service, or automatic theme write is included yet.
- Theme operations do not issue monitor DDC/CI or WMI brightness commands.

## Windows test

1. Record the starting theme shown by DisplayPilot and Windows Settings.
2. Select **Dark** and verify apps, taskbar/Start, Windows Settings, and DisplayPilot
   report dark for both application and system theme.
3. Close and reopen DisplayPilot; it must still detect dark without writing anything.
4. Select **Light** and verify both values and visible Windows surfaces return to light.
5. Change one theme value through Windows Settings if the OS exposes separate choices,
   then select **Refresh**. DisplayPilot should show the mixed state accurately.
6. Confirm monitor brightness and all other monitor settings remain unchanged.

Run the sequence on both Windows 10 and Windows 11 before merging this checkpoint.
