# Features

| Feature | Implementation | Validation |
| --- | --- | --- |
| Tray icon and flyout | Native resource-based controls; collapsible fixed/solar editor, manual location, brightness, contrast, color and rotation | DPI, taskbar positions, focus |
| Context menu | Native Windows popup menu | Mouse and keyboard |
| Advanced window | Resizable top-level window | Layout, controls, scrolling |
| Brightness and contrast | External DDC/CI and WMI controls; serialized background writes, debounced sliders and verified read-back | Hardware read-back |
| Color temperature | Hardware-advertised VCP presets and serialized asynchronous writes | Monitor capabilities |
| Internal panel brightness | WMI COM | Laptop and mixed monitor setups |
| Brightness-key notifications | WMI events and raw HID Consumer Control keys; cursor-targeted DDC adjustment, coalesced changes, Windows power brightness synchronization | Mixed-monitor keyboard events |
| Display rotation | Windows display configuration, asynchronous writes and read-back | Validation and read-back |
| Appearance | Windows personalization; manual Light/Dark overrides end at the next enabled schedule boundary | Theme updates and transition timing |
| Scheduling | Fixed times and sunrise/sunset | Time zones and date transitions |
| Automatic location | Opt-in Windows Location API notifications and explicit Update location now button; never polled | Windows location enabled, denied, or unavailable |
| Brightness restoration | Shared JSON restoration state with legacy registry fallback; persists pre-reduction baselines across failed writes and manual changes | Restart, overrides, disconnected monitors |
| Startup and single-instance | Native Windows registration and mutex; second launch activates Advanced, failed tray registration opens UI | Sign-in and relaunch |
| Diagnostics | Copyable verified monitor paths, provider/capability data, startup and scheduling snapshot | Hardware failure diagnostics |
| Installers | Inno Setup | Installation and uninstall |

See [verification](verification.md) for test coverage.
