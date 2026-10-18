# Features

| Feature | Implementation | Validation |
| --- | --- | --- |
| Tray icon and flyout | Native Win32 and Direct2D | DPI, taskbar positions, focus |
| Context menu | Native Windows popup menu | Mouse and keyboard |
| Advanced window | Resizable top-level window | Layout, controls, scrolling |
| Brightness and contrast | External DDC/CI monitors | Hardware read-back |
| Color temperature | Hardware-advertised VCP presets | Monitor capabilities |
| Internal panel brightness | WMI COM | Laptop and mixed monitor setups |
| Brightness-key notifications | WMI event polling | Device-specific events |
| Display rotation | Windows display configuration | Validation and read-back |
| Appearance | Windows personalization | Theme updates |
| Scheduling | Fixed times and sunrise/sunset | Time zones and date transitions |
| Automatic location | Not implemented | Windows location service integration |
| Brightness restoration | Registry state | Restart, overrides, disconnected monitors |
| Startup and single-instance | Native Windows registration and mutex | Sign-in and relaunch |
| Diagnostics | Copyable state snapshot | Hardware failure diagnostics |
| Installers | Inno Setup | Installation and uninstall |

See [verification](verification.md) for test coverage.
