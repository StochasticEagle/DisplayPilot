# DisplayPilot native rebuild — platform and packaging contract

This is a new application, not a translation or continuation of the .NET / WinUI implementation. Implement everything in original native C++ source. The earlier application may be consulted only as a behavioral specification. The only assets copied from it are the icons and LICENSE. Rewrite all documentation and tests.

## Operating-system support

| System | 32-bit build | 64-bit build | Validation |
| --- | --- | --- | --- |
| Windows 7 SP1 | Required minimum | Required | Test both architectures |
| Windows 8 / 8.1 | Do not deliberately block | Do not deliberately block | No testing required |
| Windows 10 | Required | Required | Test both architectures |
| Windows 11 | Untested, not blocked (32-bit app under WOW64) | Required | Test 64-bit only |

Windows 11 itself is 64-bit only; the `32` application build is a 32-bit program that may execute under WOW64 on Windows 11. It is explicitly **untested and unblocked**, not a required test target. The `64` application build remains supported and tested on Windows 11.

Set the common minimum executable and imported-API baseline to Windows 7 (NT 6.1). Build directly in Visual Studio 2026 from DisplayPilot.sln (MSVC v143, no CMake). Retain the Windows 7 API and subsystem baseline, and dynamically resolve newer APIs instead of introducing OS-version-specific imports. Dynamically resolve any newer Windows API before using it, and never require that API for program startup.

### DPI and presentation

Use one Direct2D/DirectWrite rendering and layout implementation on all supported systems. The flyout's geometry, text and controls scale from the actual DPI; the 4K-capable UI is also used on Windows 7.

Windows 7 has system-DPI awareness, not Windows 8.1+ per-monitor DPI-awareness APIs. Use the Windows 7 DPI baseline there, and opt into newer per-monitor APIs only if available. Do not remove controls, graphics quality or high-resolution support on Windows 7 merely because the newer DPI APIs are absent.

### Windows appearance and scheduling

Hide and disable Windows-wide light/dark-theme switching and any controls that depend on unsupported Windows theme settings on Windows 7. Keep independent monitor controls, scheduling calculations, sunrise/sunset logic, and other compatible functions available. Preserve Windows 10/11 theme functionality. Never block launch based solely on Windows 8/8.1.

## Deployment layout

Produce two **native** C++ executable builds, with exactly these architecture directory names:

- `dist/32/DisplayPilot.exe` — Windows 7 SP1 32-bit baseline; also usable on supported 64-bit Windows through WOW64.
- `dist/64/DisplayPilot.exe` — Windows 7 SP1 64-bit baseline.

Use `32` and `64` in build/output and installer artifact directories. Never name directories `x32`, `x64`, `win-x32` or `win-x64`. The build may produce two separate installers, one for each architecture, with correspondingly named output directories (`dist/32/`, `dist/64/`).

Each installed application directory contains **DisplayPilot.exe only**. Embed icons, user interface, strings and other resources in the PE executable; do not ship external configuration or content files.

No .NET, CLR, WinUI, XAML/XBF, JSON, web assets, apphost/launcher, WebView2, extra helper executable, runtime DLL copy, archive or AppImage-style runtime bundle. No third-party libraries. System DLLs are supplied by the operating system, not copied into the installation. Do not copy Microsoft-authored application source or sample code into this project; using public Windows APIs and the SDK to compile the native app is allowed.

## Process and window model

One DisplayPilot process, one Win32 message-loop owner, no service, worker process or extra launcher. Background work, when needed, is confined to threads in the same process.

The notification-area icon uses Windows Shell APIs. The main flyout is a frameless, **non-modal**, non-activating popup resembling Windows Volume Mixer/Quick Settings, anchored to the tray notification area. No dialog-box implementation. Preserve hover/pointer-leave behavior, keyboard access, interactive controls and the existing flyout layout.

The context menu is the native shell-style Win32 popup menu with existing actions. The Advanced interface is a separate, resizable, normal top-level window **within the same process**, not a separate program.

## Functional parity

Reimplement all current functional behavior, without copying old source: multi-monitor detection; DDC/CI; WMI internal-panel brightness; contrast and color-temperature controls; rotation; brightness-key routing; read-back verification; DPI-aware flyout placement; scheduled theme/brightness controls; fixed-time and sunrise/sunset schedules; location handling; restoration of brightness; startup registration; single-instance behavior; Explorer restart recovery; diagnostics; and installer/uninstaller behavior.

Feature availability is gated by the OS and the hardware, never by removing otherwise portable functionality.

## Release and documentation

Build a compact Release executable with symbols/debug tools excluded from the installation. Tests, intermediate object files and debug symbols may exist in development/build workspaces but must not be installed. Document the Windows 7 compatibility baseline, architecture-specific builds, native API dependencies, UI behavior, complete test matrix, and any OS-specific feature gating. No PowerShell scripts in the application or build process.
