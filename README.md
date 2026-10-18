# DisplayPilot — Native Windows C++ Application 

DisplayPilot is a native, single-process Windows notification-area monitor utility.

This branch is a clean C++ application.

## Build in Visual Studio 2026

Open **DisplayPilot.sln** in Visual Studio 2026 with the **Desktop development with C++** workload and a Windows SDK installed.

Choose **Release** or **Debug** and the desired solution platform:

| Visual Studio platform | Application output |
| --- | --- |
| Win32 | `dist/32/DisplayPilot.exe` |
| Win64 | `dist/64/DisplayPilot.exe` |

Select **Build > Build Solution** or press **Ctrl+Shift+B**.

The native project defaults to Visual Studio 2026's MSVC **v145** toolset. The GitHub Windows build uses the compatible v143 toolset available on its Visual Studio 2022 runner.

From the Visual Studio Developer Command Prompt:

```cmd
msbuild DisplayPilot.sln /m /p:Configuration=Release /p:Platform=Win32
msbuild DisplayPilot.sln /m /p:Configuration=Release /p:Platform=x64
```

Release uses static C/C++ runtime linking and the Windows system libraries. It does not ship those libraries, debug information, helper executables or separate resources. Both outputs consist only of `DisplayPilot.exe`.

## Implementation status

The current native implementation includes the notification-area icon, modeless Direct2D flyout, native context menu, expanded Advanced window, DDC/CI brightness and contrast, hardware-advertised color presets, internal-panel WMI brightness, rotation, Windows light/dark controls, scheduling, startup registration, and Registry persistence. Feature parity and hardware testing remain incomplete: WMI brightness-key events are handled without an additional process; automatic Windows location integration remains outstanding. See [feature parity](docs/feature-parity.md) and [verification](docs/verification.md).

## Platforms

Windows 7 SP1 is the minimum runtime target, with 32-bit and 64-bit outputs. Windows 10 is targeted on both architectures. Windows 11 supports 64-bit; running the 32-bit application under WOW64 is untested and unblocked. Windows 8/8.1 is likewise untested and unblocked.

See [platform requirements](docs/rebuild-platforms.md) and [architecture](docs/architecture.md).

## License

The [Unlicense](LICENSE).
