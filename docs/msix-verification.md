# Framework-dependent MSIX verification

The MSIX build intentionally does not bundle .NET or the Windows App SDK
runtime. Test on Windows 10 and Windows 11 x64 after the existing Inno Setup
version of DisplayPilot has been uninstalled.

## Prerequisites

1. Install the x64 .NET 10 Desktop Runtime.
2. Install the x64 Windows App SDK 2.3 runtime.
3. Confirm that `dotnet --list-runtimes` includes
   `Microsoft.WindowsDesktop.App 10`.

## Trust the test certificate

The CI workflow creates a new test certificate for each build. Import the
`DisplayPilot-Test.cer` file from the workflow artifact for the current user:

```powershell
Import-Certificate `
  -FilePath .\DisplayPilot-Test.cer `
  -CertStoreLocation Cert:\CurrentUser\TrustedPeople
```

The certificate is only for sideload testing. A public release needs a stable
code-signing certificate or Microsoft Store signing.

## Install

Install the `.msix` file from the `DisplayPilot-MSIX-win-x64` workflow artifact
by double-clicking it, or run:

```powershell
$package = Get-ChildItem . -Filter *.msix | Select-Object -First 1
Add-AppxPackage $package.FullName
```

Verify the notification-area icon, flyout, Advanced interface, DDC/CI or WMI
brightness control, theme scheduling, and diagnostic report.

## Start at sign-in

1. Open **Advanced**.
2. Turn on **Start DisplayPilot when I sign in**.
3. Sign out and sign in.
4. Confirm that DisplayPilot starts in the notification area.
5. Turn the option off and confirm that it does not start after the next
   sign-in.

## Uninstall

Remove DisplayPilot from Windows Settings, or run:

```powershell
Get-AppxPackage StochasticEagle.DisplayPilot | Remove-AppxPackage
```

Confirm that the package and its startup task are removed.
