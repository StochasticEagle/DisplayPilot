# Display-path verification

This screen verifies active Windows display-path discovery on a VM and on physical
Windows 10/11 systems. Its startup scan does not open physical monitor handles or
send DDC/CI or WMI commands. A separate, explicit button performs a read-only
brightness VCP 0x10 query per physical monitor and queries the read-only
`WmiMonitorBrightness` class for internal panels. After a successful read, the user
may select exactly one display and explicitly set brightness. External monitors use
VCP 0x10; internal panels use `WmiSetBrightness`. Every write is followed by a
read-back and no other monitor feature is written.

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

Then select **Read brightness (DDC + WMI)** and copy the report again. Expected
results are:

- Most VMs report `NoPhysicalMonitor` or `ReadFailed` because their virtual display
  does not expose a DDC/CI channel.
- A DDC/CI-capable desktop monitor should report `ReadSucceeded`, together with its
  raw current and maximum brightness values.
- A single button click makes at most three read attempts, separated by a short
  delay, because some otherwise-compatible monitors reject the first VCP request.
- Physical-monitor handle acquisition also retries up to three times at 200 ms
  intervals because Windows can transiently return a null handle for a valid monitor.
- An active internal laptop panel should report `ReadSucceeded` through WMI with
  its current percentage and advertised brightness-level count, even when its DDC
  probe reports a protocol error.
- External monitors normally report no matching `WmiMonitorBrightness` instance;
  their brightness path remains DDC/CI.
- WMI instances are correlated to display paths with the full PnP instance identity,
  so identical panel models with different UIDs are not conflated.
- The app must continue to report that no writes were issued.
- If the Windows clipboard is temporarily unavailable, copying the report must show
  an error and allow another attempt without closing the app.

## Brightness write verification

Only continue after the selected display has a successful DDC/CI or WMI brightness
read. If exactly one display has a validated write path, it is selected automatically;
otherwise select its card. Enter a value from 0 through 100 and select **Set selected**.
Use non-extreme values first (for example 40, 60, then 50).

- Only the selected display should change.
- The status line must name the chosen provider and show requested, applied, and
  verified percentages.
- DDC/CI percentages are mapped to the monitor's raw maximum before VCP 0x10 is set.
- WMI requests snap to the nearest level advertised by the panel.
- A display without a validated read path must never enable the write button.
- Do not test power, input source, contrast, volume, color temperature, or profiles;
  those controls are not part of this checkpoint.

The current QEMU test baseline is one `QEMU Monitor` at `\\.\DISPLAY1`, with an
EDID identifier beginning `RHT1234`. The DDC probe should remain attached to that
same display card even when the virtual monitor reports no readable VCP code.

Run the check once in the VM and once on a physical desktop. A VM display is
expected to appear in the inventory even though it normally has no DDC/CI channel.
