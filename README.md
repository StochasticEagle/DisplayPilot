# DisplayPilot
An open-source dark/light theme and monitor brightness tool for Windows 10/11 desktops.

The current development build includes a read-only WinUI 3 display inventory with
DDC/CI brightness control for external monitors and WMI brightness control for
internal panels, with an explicit write and immediate read-back verification. See
[Display-path verification](docs/display-path-verification.md)
for build, run, and test instructions. Manual Windows light/dark theme detection and
switching are described in [Theme verification](docs/theme-verification.md).
The read-only fixed-hours evaluator is covered by
[Custom theme schedule verification](docs/custom-theme-schedule-verification.md).
Per-user schedule saving is covered by
[Theme schedule persistence verification](docs/theme-schedule-persistence-verification.md).
Opt-in switching while the app is open is covered by
[Fixed theme automation verification](docs/fixed-theme-automation-verification.md).
