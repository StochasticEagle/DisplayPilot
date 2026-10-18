# DisplayPilot native architecture

All new application source is original C++ under src/native. No old C# or WinUI source is retained.

- main.cpp: one process, one hidden Win32 owner window, tray icon callback, hover timers, native context menu, Advanced top-level window and system-version feature gating.
- flyout.cpp: a non-modal WS_POPUP with WS_EX_TOOLWINDOW and WS_EX_NOACTIVATE, rounded-corner geometry, Direct2D graphics, DirectWrite labels and pointer-operated slider controls. No dialog boxes.
- display.cpp: discover physical monitors with Win32, read and write DDC/CI brightness and contrast via DXVA2 and destroy handles after each operation.
- app.rc.in: compile the icon and DPI/compatibility manifest into the executable. The resource index remains inside the executable.

The Windows 7 SP1 API baseline is required on both architectures. Newer DPI functions are resolved at runtime; Windows 7 uses system DPI. Windows-wide theme modification is only offered on Windows 10 and later.

Build with MSVC and /MT to avoid redistributing the VC runtime. Link to Windows-supplied operating-system DLLs by normal dynamic DLL import. There is no .NET, WinUI, third-party code or separate loader.

The first executable is a foundational slice, **not feature-parity complete**. Future implementation must extend it with WMI brightness, DDC/CI color and contrast UI, display rotation, brightness-key routing, solar scheduling, Windows location, native settings persistence, complete Advanced controls and installer packaging. Platform-specific hardware behavior must be tested on physical machines.
