# DisplayPilot native architecture

The C++ implementation resides under `src/native` and builds directly as a Visual Studio C++ project. The root `DisplayPilot.sln` opens in Visual Studio 2026; `DisplayPilot.vcxproj` specifies all compiler and linker settings. **CMake is not used.**

- `main.cpp`: single-process Win32 message loop, notification icon, mouse-hover timing, native context menu, Advanced top-level window and OS-specific feature gating.
- `flyout.cpp`: a modeless `WS_POPUP` window with `WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`, Direct2D drawing and interactive slider input. Not a dialog box.
- `display.cpp`: DXVA2 physical monitor discovery, DDC/CI brightness and contrast queries and writes.
- `app.rc`: directly compiles the icon and Windows 7-compatible manifest into the executable.

The project uses the MSVC v145 platform toolset on Visual Studio 2026, C++17 and static C/C++ runtime linking (`/MT`, `/MTd` in Debug). It dynamically imports standard Windows operating-system APIs. The Windows 7 SP1 application baseline is specified with `_WIN32_WINNT=0x0601` and subsystem version 6.01. Higher-version DPI APIs are dynamically resolved. Debug intermediates and PDBs are outside the deployable `dist/32` and `dist/64` folders.

The application ships only `DisplayPilot.exe`, with no .NET, JavaScript/web payloads, JSON/XBF files, third-party libraries or additional processes.

This is the **first functional native foundation**; full feature parity and architecture-specific hardware validation remain in progress. See [platform specification](rebuild-platforms.md).
