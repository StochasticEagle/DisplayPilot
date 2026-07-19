# Display-path verification

This checkpoint provides a read-only WinUI 3 screen for verifying active Windows
display-path discovery on a VM and on physical Windows 10/11 systems. It does not
open physical monitor handles, query MCCS capabilities, send DDC/CI commands, or
change display settings.

## Build and run

Requirements:

- Windows 10 version 1809 or newer, or Windows 11.
- An x64 Windows installation.
- Visual Studio with the .NET desktop and WinUI application development tools,
  or the .NET 10 SDK with the corresponding Windows SDK build tools.

From a Developer PowerShell in the repository root:

```powershell
dotnet restore DisplayPilot.slnx
dotnet run --project src/DisplayPilot.App/DisplayPilot.App.csproj
```

The app is unpackaged and self-contained. A Release folder suitable for copying
to another x64 Windows system can be produced with:

```powershell
dotnet publish src/DisplayPilot.App/DisplayPilot.App.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true
```

Copy the entire publish directory to the test system and run `DisplayPilot.exe`.

## What to record

Select **Copy diagnostic report** after the scan. For every active path, verify:

- The friendly name resembles the monitor model (a VM may expose a generic name).
- The Windows name resembles `\\.\DISPLAY1`.
- The stable device path is non-empty and begins with `\\?\DISPLAY#`.
- Rescanning produces the same device path when the topology has not changed.

Run the check once in the VM and once on a physical desktop. A VM display is
expected to appear in the inventory even though it normally has no DDC/CI channel.
Physical-monitor DDC/CI capability probing is a separate checkpoint.
