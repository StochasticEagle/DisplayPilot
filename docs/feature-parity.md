# Functional parity — native rebuild

This is the working checklist for replacing the former managed application, not permission to reuse its source. A compile-green feature still requires UI and physical-device validation before being marked complete.

| Feature | Current native implementation | Remaining acceptance |
| --- | --- | --- |
| Tray icon, hover open, delayed hide | Implemented | Test pointer edges, auto-hide taskbar, second monitor, Win7/10/11 |
| Modeless nonactivating Direct2D panel | Implemented | Verify no focus theft, accessible keyboard navigation, scrolling |
| Native right-click menu | Implemented | Compare final commands/labels against former interface |
| Advanced separate window, same process | Implemented | Match prior layout, feature organization and accessibility |
| DDC/CI brightness and contrast | Implemented | Verify hardware capability detection, writes and read-back |
| Color-temperature presets (MCCS VCP 0x14) | Capability-gated implemented | More extensive capability parsing, unsupported monitor testing |
| Internal-screen brightness via WMI COM | Implemented | Accurate mapping and test against laptop panel models |
| Multi-monitor discovery and rotation | Implemented | Hotplug, mirrored topology, rotation test/read-back |
| Brightness keys and WMI change notifications | Not implemented | Subscribe to brightness events, prevent feedback loops |
| Windows 10/11 theme switch | Implemented | Verify setting changes and external synchronization |
| Windows 7 theme-control hiding | Implemented | Validate Win7 32-bit and 64-bit |
| Fixed-time scheduling | Implemented | DST/time-zone transition testing |
| Solar sunrise/sunset calculations | Implemented | Validate polar cases, DST, locations and dates |
| Windows automatic location provider | Not implemented | Permission-aware system API integration |
| Manual lat/longitude and Registry settings | Implemented | Persistence, validation, migration from old settings |
| Scheduled brightness reduction and restoration | Implemented | Crash/reboot, device removal, user override |
| Launch at login, single instance, Explorer restart | Implemented | Confirm no second process/services |
| Diagnostics | Copyable runtime snapshot | Expand errors and debug trace; keep logger out of runtime report |
| Installer 32 and 64 | Script present | Build and test both installers and uninstall |
| Documentation | Native docs in progress | Full end-user and troubleshooting manuals |

No third-party libraries, loaders, JSON/XBF, web files or auxiliary runtime assets may be installed.
