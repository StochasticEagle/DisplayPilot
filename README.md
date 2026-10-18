# DisplayPilot

DisplayPilot is a native C++ Windows notification-area application for monitor controls and appearance scheduling.

## Build

Open `DisplayPilot.sln` in Visual Studio 2026 with MSVC v145 and the Windows SDK installed. Select Debug or Release and Win32 or Win64, then select **Build Solution**.

| Platform | Release output |
| --- | --- |
| Win32 | `dist/32/DisplayPilot.exe` |
| Win64 | `dist/64/DisplayPilot.exe` |

Command-line builds from a Visual Studio Developer Command Prompt:

```cmd
msbuild DisplayPilot.sln /m /p:Configuration=Release /p:Platform=Win32
msbuild DisplayPilot.sln /m /p:Configuration=Release /p:Platform=Win64
```

Release executables use the static MSVC runtime and embed application resources. Separate installers are built with `installer/DisplayPilot.iss`.

## Features

The notification-area icon opens a modeless Direct2D monitor-control flyout. A native context menu provides quick commands and access to the separate Advanced window. Monitor controls use DDC/CI, WMI, and Windows display APIs. Fixed-time and solar scheduling are available with user-configured coordinates.

## Supported Windows versions

Windows 7 SP1, Windows 10, and Windows 11 are covered by the supported platform matrix. See [platforms](docs/platforms.md).

## Documentation

[Architecture](docs/architecture.md) · [Features](docs/features.md) · [Verification](docs/verification.md)

## License

[The Unlicense](LICENSE).
