# DisplayPilot — native C++ rebuild

DisplayPilot is a single-process native Windows notification-area monitor utility.

The dev/rebuild branch is a **clean new application**, not a port of source code from the previous .NET/WinUI application. Only the existing icons and LICENSE are reused. There are no third-party runtime libraries, .NET, WinUI, XAML, JSON configuration files, web assets or extra application processes.

## Current functionality

The first implementation provides a native tray icon, delayed-hover modeless flyout, Direct2D/DirectWrite rendering, an interactive per-monitor DDC/CI brightness slider, a native Windows right-click menu with Open DisplayPilot / Advanced / Exit, an independently movable Advanced window, Explorer restart recovery and a single-instance mutex.

The remaining functionality (internal-panel WMI, VCP contrast/color, rotation, keyboard brightness integration, schedule and location services, full Advanced controls and persistence) is not yet reimplemented. The current source compiles the new foundation; it is not feature-complete.

## Build

Requires Windows with MSVC C++ desktop build tools, CMake and Windows SDK. Windows 7 SP1 is the minimum runtime target.

    cmake -S . -B build/32 -A Win32
    cmake --build build/32 --config Release

    cmake -S . -B build/64 -A x64
    cmake --build build/64 --config Release

The output directories are exactly:

    dist/32/DisplayPilot.exe
    dist/64/DisplayPilot.exe

Each app directory contains the executable alone. Windows DLLs are dynamically linked from the operating system; the MSVC C/C++ runtime is statically linked.

## Platform compatibility

Windows 7 SP1: both 32 and 64 builds; Windows 10: both; Windows 11: 64 is supported, 32 is untested and unblocked. Windows 8/8.1 is untested and unblocked.

Windows 7 system theme switching is disabled; the other compatible controls remain available. The same DPI-scalable panel is used on all target operating systems.

Read the platform specification at docs/rebuild-platforms.md and the architecture overview at docs/architecture.md.

## License

The Unlicense (LICENSE).
