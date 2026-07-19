# Display-path verification

This screen verifies active Windows display-path discovery on a VM and on physical
Windows 10/11 systems. Its startup scan does not open physical monitor handles or
send DDC/CI commands. A separate, explicit button performs one read-only brightness
VCP 0x10 query per physical monitor. No control-writing API is imported or called.

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

Alternatively, open the latest successful **Build** workflow run on GitHub and
download the `DisplayPilot-win-x64` artifact. Extract the entire archive before
running `DisplayPilot.exe`; the executable still depends on the files beside it.

## What to record

Select **Copy diagnostic report** after the scan. For every active path, verify:

> The report contains stable, instance-specific device paths. Keep physical-machine
> reports private or redact those paths before posting the report publicly.

- The friendly name resembles the monitor model (a VM may expose a generic name).
- The Windows name resembles `\\.\DISPLAY1`.
- The stable device path is non-empty and begins with `\\?\DISPLAY#`.
- Rescanning produces the same device path when the topology has not changed.

Then select **Read brightness (VCP 0x10)** and copy the report again. Expected
results are:

- Most VMs report `NoPhysicalMonitor` or `ReadFailed` because their virtual display
  does not expose a DDC/CI channel.
- A DDC/CI-capable desktop monitor should report `ReadSucceeded`, together with its
  raw current and maximum brightness values.
- A single button click makes at most three read attempts, separated by a short
  delay, because some otherwise-compatible monitors reject the first VCP request.
- Internal laptop panels may report no readable DDC brightness; their eventual
  brightness path uses WMI rather than DDC/CI.
- The app must continue to report that no writes were issued.
- If the Windows clipboard is temporarily unavailable, copying the report must show
  an error and allow another attempt without closing the app.

The current QEMU test baseline is one `QEMU Monitor` at `\\.\DISPLAY1`, with an
EDID identifier beginning `RHT1234`. The DDC probe should remain attached to that
same display card even when the virtual monitor reports no readable VCP code.

Run the check once in the VM and once on a physical desktop. A VM display is
expected to appear in the inventory even though it normally has no DDC/CI channel.
