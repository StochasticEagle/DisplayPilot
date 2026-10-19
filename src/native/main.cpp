#include "display.hpp"
#include "flyout.hpp"
#include "advanced.hpp"
#include "settings.hpp"
#include "wmi_events.hpp"
#include "location.hpp"
#include "raw_brightness.hpp"
#include "power_brightness.hpp"
#include <wtsapi32.h>
#include <dbt.h>
#include <windows.h>
#include <shellapi.h>
#include <algorithm>
#include <string>
#include <vector>
#include <new>

namespace displaypilot {
namespace {
constexpr UINT trayMessage = WM_APP + 10;
constexpr UINT refreshMessage = WM_APP + 11;
constexpr UINT iconNumber = 1;
constexpr UINT hoverTimer = 1;
constexpr UINT leaveTimer = 2;
constexpr UINT scheduleTimer = 3;
constexpr UINT refreshTimer = 4;
constexpr UINT brightnessEventTimer = 5;
constexpr UINT rawBrightnessTimer = 6;
constexpr UINT hardwareDebounceTimer = 7;
constexpr UINT activateExisting = WM_APP + 90;
constexpr UINT commandOpen = 101;
constexpr UINT commandAdvanced = 102;
constexpr UINT commandExit = 103;
constexpr UINT commandLight = 104;
constexpr UINT commandDark = 105;
constexpr UINT commandSchedule = 106;

struct PendingControlWrite {
    std::wstring monitorKey;
    unsigned value = 0;
    bool contrast = false;
};
struct Application {
    HINSTANCE instance = nullptr;
    HWND owner = nullptr;
    HICON icon = nullptr;
    Flyout flyout;
    AdvancedWindow advanced;
    BrightnessEvents brightnessKeys;
    LocationUpdates* location = nullptr;
    Settings settings;
    std::vector<Monitor> monitors;
    std::vector<PendingControlWrite> pendingControls;
    UINT taskbarRestart = 0;
    DWORD lastNearIcon = 0;
    bool supportsThemes = false;
    bool scheduleApplied = false;
    bool lastDark = false;
    bool savedBrightness = false;
    bool manualThemeOverride = false;
    bool sessionActive = true;
    bool startupBrightnessSynchronized = false;
    HMONITOR pendingBrightnessMonitor = nullptr;
    int pendingBrightnessDelta = 0;
    DWORD lastRawBrightnessEvent = 0;
} app;
void syncLocation();

using NotifyRectFn = HRESULT(WINAPI*)(const NOTIFYICONIDENTIFIER*, RECT*);
RECT iconRectangle() {
    RECT result{};
    NOTIFYICONIDENTIFIER id{};
    id.cbSize = sizeof(id);
    id.hWnd = app.owner;
    id.uID = iconNumber;
    HMODULE shell = GetModuleHandleW(L"shell32.dll");
    auto getRect = shell ? reinterpret_cast<NotifyRectFn>(
        GetProcAddress(shell, "Shell_NotifyIconGetRect")) : nullptr;
    if (getRect && SUCCEEDED(getRect(&id, &result))) return result;
    POINT cursor{};
    GetCursorPos(&cursor);
    return RECT{cursor.x, cursor.y, cursor.x + 1, cursor.y + 1};
}
bool contains(const RECT& area, POINT point) {
    return PtInRect(&area, point) != FALSE;
}
bool updateIcon(bool add) {
    NOTIFYICONDATAW data{};
    data.cbSize = sizeof(data);
    data.hWnd = app.owner;
    data.uID = iconNumber;
    data.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
    data.uCallbackMessage = trayMessage;
    data.hIcon = app.icon;
    lstrcpynW(data.szTip, L"DisplayPilot", ARRAYSIZE(data.szTip));
    const bool registered=Shell_NotifyIconW(add ? NIM_ADD : NIM_MODIFY,&data)!=FALSE;
    if(registered) {
        data.uVersion = NOTIFYICON_VERSION_4;
        Shell_NotifyIconW(NIM_SETVERSION, &data);
    }
    return registered;
}

void hideFlyout() {
    app.flyout.hide();
    if (app.owner) KillTimer(app.owner, leaveTimer);
}

void refreshMonitors() {
    app.monitors = enumerateMonitors();
    const bool hasWmi = std::any_of(app.monitors.begin(), app.monitors.end(),
        [](const Monitor& monitor) {
            return monitor.provider == ControlProvider::Wmi;
        });
    if (hasWmi && !app.brightnessKeys.active()) {
        if (app.brightnessKeys.start())
            SetTimer(app.owner, brightnessEventTimer, 350, nullptr);
    } else if (!hasWmi && app.brightnessKeys.active()) {
        KillTimer(app.owner, brightnessEventTimer);
        app.brightnessKeys.stop();
    }
    if (!app.startupBrightnessSynchronized) {
        for (const auto& monitor : app.monitors) {
            if (monitor.provider==ControlProvider::Wmi && monitor.brightnessAvailable) {
                app.startupBrightnessSynchronized=syncWindowsBrightness(monitor.brightness);
                break;
            }
        }
    }
    app.flyout.repaint();
    app.advanced.refresh();
}
void queueRefresh() {
    if (app.owner) PostMessageW(app.owner, refreshMessage, 0, 0);
}
void updateAppearance() {
    bool dark = false;
    if (app.supportsThemes)
        readWindowsTheme(dark);
    app.flyout.setAppearance(app.supportsThemes, dark, app.settings.scheduleEnabled);
}
std::wstring legacyRestoreKey(const Monitor& monitor) {
    std::wstring stable = monitor.provider == ControlProvider::Wmi
        ? monitor.wmiInstance : monitor.deviceName + L":" + std::to_wstring(monitor.physicalIndex);
    // Registry value-name size is limited. Hashing keeps WMI instance paths bounded.
    unsigned long long hash = 14695981039346656037ULL;
    for (wchar_t ch : stable) {
        hash ^= static_cast<unsigned long long>(ch);
        hash *= 1099511628211ULL;
    }
    return L"Monitor-" + std::to_wstring(hash);
}
std::wstring restoreKey(const Monitor& monitor) {
    return monitor.persistentPath.empty() ?
        legacyRestoreKey(monitor) : monitor.persistentPath;
}
void restoreMonitorBrightness() {
    bool pending = false;
    bool changed = false;
    for (const auto& monitor : app.monitors) {
        if (!monitor.brightnessAvailable) continue;
        const std::wstring key = restoreKey(monitor);
        unsigned previous = 0;
        const std::wstring legacy = legacyRestoreKey(monitor);
        bool found = restoreBrightness(key, previous);
        bool fromLegacy = false;
        if (!found && key != legacy) {
            found = restoreBrightness(legacy, previous);
            fromLegacy = found;
        }
        if (!found) continue;
        if (setBrightness(monitor, previous)) {
            removeSavedBrightness(fromLegacy ? legacy : key);
            changed = true;
        } else pending = true;
    }
    app.savedBrightness = pending || anySavedBrightness();
    if (changed) queueRefresh();
}
void applyNightBrightness() {
    // Process each monitor independently: an already-dimmed display must not
    // prevent a newly connected monitor from receiving scheduled reduction.
    bool any = false;
    for (const auto& monitor : app.monitors) {
        if (!monitor.brightnessAvailable) continue;
        std::wstring key = restoreKey(monitor);
        unsigned baseline = 0;
        if (!restoreBrightness(key, baseline)) {
            const auto legacy = legacyRestoreKey(monitor);
            if (key != legacy && restoreBrightness(legacy, baseline)) {
                key = legacy;
            } else {
                baseline = monitor.brightness;
                if (!saveBrightness(key, baseline)) continue;
            }
        }
        // Never throw away a saved baseline if a hardware write fails:
        // it might have reached the device despite failing verification.
        any = true;
        const unsigned target = baseline > 10U ? baseline / 2U : 0U;
        setBrightness(monitor, target);
    }
    app.savedBrightness = any || anySavedBrightness();
    queueRefresh();
}
DWORD untilTomorrow(const SYSTEMTIME& now) {
    const DWORD seconds = (23U - now.wHour) * 3600U +
        (59U - now.wMinute) * 60U + (60U - now.wSecond);
    return std::max(1000UL, seconds * 1000UL);
}
void applySchedule(bool force) {
    KillTimer(app.owner, scheduleTimer);
    SYSTEMTIME now{};
    GetLocalTime(&now);
    const auto state = evaluateSchedule(app.settings, now);
    if (!state.valid) {
        app.manualThemeOverride = false;
        restoreMonitorBrightness();
        app.scheduleApplied = false;
        // Polar dates need a fresh calculation tomorrow; absent coordinates do not.
        if (app.settings.scheduleEnabled && app.settings.scheduleMode == ScheduleMode::Solar &&
            app.settings.locationConfigured)
            SetTimer(app.owner, scheduleTimer, untilTomorrow(now), nullptr);
        updateAppearance();
        return;
    }
    const bool activeOverride = app.manualThemeOverride &&
        app.scheduleApplied && app.lastDark == state.dark;
    if (!activeOverride) app.manualThemeOverride = false;
    if (force || !app.scheduleApplied || app.lastDark != state.dark) {
        if (app.supportsThemes && !activeOverride) setWindowsTheme(state.dark);
        if (state.dark && app.settings.reduceBrightness)
            applyNightBrightness();
        else restoreMonitorBrightness();
        app.lastDark = state.dark;
        app.scheduleApplied = true;
    } else if (!state.dark || !app.settings.reduceBrightness) {
        restoreMonitorBrightness();
    }
    // One-shot transition timer, bounded by midnight when solar times change.
    DWORD interval = state.millisecondsUntilChange;
    if (app.settings.scheduleMode == ScheduleMode::Solar)
        interval = std::min(interval, untilTomorrow(now));
    SetTimer(app.owner, scheduleTimer, std::max(1000UL, interval), nullptr);
    updateAppearance();
}
void manualTheme(bool dark) {
    if (!app.supportsThemes) return;
    // Match WinUI: preserve enabled scheduling and override only until the
    // next schedule boundary. The existing one-shot timer remains armed.
    if (app.settings.scheduleEnabled) {
        SYSTEMTIME now{};
        GetLocalTime(&now);
        const auto current = evaluateSchedule(app.settings, now);
        app.manualThemeOverride = current.valid;
        if (current.valid) {
            app.lastDark = current.dark;
            app.scheduleApplied = true;
        }
    } else {
        app.manualThemeOverride = false;
        if (app.savedBrightness) restoreMonitorBrightness();
        app.scheduleApplied = false;
    }
    setWindowsTheme(dark);
    updateAppearance();
    app.advanced.refresh();
}
void syncLocation() {
    if (!app.location) return;
    if (app.settings.scheduleEnabled && app.settings.scheduleMode == ScheduleMode::Solar &&
        app.settings.automaticLocation) app.location->start();
    else app.location->stop();
}
bool updateLocationNow() {
    if (!app.location || !app.settings.scheduleEnabled ||
        app.settings.scheduleMode != ScheduleMode::Solar) return false;
    return app.location->updateNow();
}
bool saveNewSettings(const Settings& next) {
    if (!saveSettings(next)) return false;
    const bool previousEnabled = app.settings.scheduleEnabled;
    app.settings = next;
    app.manualThemeOverride = false;
    syncLocation();
    if (previousEnabled && !next.scheduleEnabled && app.savedBrightness)
        restoreMonitorBrightness();
    applySchedule(true);
    app.advanced.refresh();
    return true;
}
void changeBrightness(unsigned index, unsigned value) {
    if (index >= app.monitors.size()) return;
    // Preserve the pre-schedule baseline across manual changes; master
    // restores it when the scheduled reduction ends.
    if (setBrightness(app.monitors[index], value)) {
        app.monitors[index].brightness = value;
        app.flyout.repaint();
    } else queueRefresh();
}
void queueManualControl(unsigned index, unsigned value, bool contrast) {
    if(index>=app.monitors.size()) return;
    const auto key=restoreKey(app.monitors[index]);
    auto found=std::find_if(app.pendingControls.begin(),app.pendingControls.end(),
        [&](const PendingControlWrite& x) { return x.monitorKey==key && x.contrast==contrast; });
    if(found==app.pendingControls.end())
        app.pendingControls.push_back(PendingControlWrite{key,value,contrast});
    else found->value=value;
    SetTimer(app.owner,hardwareDebounceTimer,120,nullptr);
}
void changeContrast(unsigned index, unsigned value) {
    if (index >= app.monitors.size()) return;
    if (setContrast(app.monitors[index], value))
        app.monitors[index].contrast = value;
    else queueRefresh();
    app.flyout.repaint();
}
void changeColor(unsigned index, unsigned preset) {
    if (index >= app.monitors.size()) return;
    if (setColorPreset(app.monitors[index], preset))
        app.monitors[index].colorPreset = preset;
    else queueRefresh();
    app.flyout.repaint();
}
void changeRotation(unsigned index, unsigned orientation) {
    if (index >= app.monitors.size()) return;
    if (setRotation(app.monitors[index], orientation)) {
        app.monitors[index].rotation = orientation;
        SetTimer(app.owner, refreshTimer, 350, nullptr);
    } else queueRefresh();
}

void showFlyout() {
    KillTimer(app.owner, hoverTimer);
    refreshMonitors();
    updateAppearance();
    app.flyout.show(iconRectangle(), &app.monitors, &app.settings);
    app.lastNearIcon = GetTickCount();
    SetTimer(app.owner, leaveTimer, 180, nullptr);
}
void showAdvanced() {
    hideFlyout();
    refreshMonitors();
    app.advanced.show(&app.monitors, &app.settings, app.supportsThemes);
}
void showContextMenu() {
    hideFlyout();
    HMENU menu = CreatePopupMenu();
    if (!menu) return;
    AppendMenuW(menu, MF_STRING, commandOpen, L"Open DisplayPilot");
    AppendMenuW(menu, MF_STRING, commandAdvanced, L"Advanced");
    if (app.supportsThemes) {
        AppendMenuW(menu, MF_SEPARATOR, 0, nullptr);
        AppendMenuW(menu, MF_STRING, commandLight, L"Light theme");
        AppendMenuW(menu, MF_STRING, commandDark, L"Dark theme");
        AppendMenuW(menu, MF_STRING | (app.settings.scheduleEnabled ? MF_CHECKED : 0),
                    commandSchedule, L"Automatic schedule");
    }
    AppendMenuW(menu, MF_SEPARATOR, 0, nullptr);
    AppendMenuW(menu, MF_STRING, commandExit, L"Exit");
    POINT position{};
    GetCursorPos(&position);
    SetForegroundWindow(app.owner);
    const UINT choice = TrackPopupMenuEx(menu,
        TPM_RIGHTBUTTON | TPM_RETURNCMD | TPM_NONOTIFY,
        position.x, position.y, app.owner, nullptr);
    DestroyMenu(menu);
    PostMessageW(app.owner, WM_NULL, 0, 0);
    switch (choice) {
    case commandOpen: showFlyout(); break;
    case commandAdvanced: showAdvanced(); break;
    case commandLight: manualTheme(false); break;
    case commandDark: manualTheme(true); break;
    case commandSchedule:
        app.settings.scheduleEnabled = !app.settings.scheduleEnabled;
        app.manualThemeOverride = false;
        saveSettings(app.settings);
        syncLocation();
        applySchedule(true);
        break;
    case commandExit: DestroyWindow(app.owner); break;
    default: break;
    }
}
LRESULT CALLBACK ownerProcedure(HWND hwnd, UINT msg, WPARAM w, LPARAM l) {
    if (msg == app.taskbarRestart && app.taskbarRestart != 0) {
        if(!updateIcon(true)) showAdvanced();
        return 0;
    }
    switch (msg) {
    case activateExisting:
        showAdvanced();
        return 0;
    case WM_WTSSESSION_CHANGE:
        if (w==WTS_SESSION_LOCK || w==WTS_CONSOLE_DISCONNECT ||
            w==WTS_REMOTE_DISCONNECT || w==WTS_SESSION_LOGOFF) {
            app.sessionActive=false;
            hideFlyout();
            app.advanced.hide();
            app.brightnessKeys.stop();
            KillTimer(hwnd,brightnessEventTimer);
            KillTimer(hwnd,rawBrightnessTimer);
            app.pendingBrightnessDelta=0;
            KillTimer(hwnd,scheduleTimer);
        } else if (w==WTS_SESSION_UNLOCK || w==WTS_CONSOLE_CONNECT ||
                   w==WTS_REMOTE_CONNECT || w==WTS_SESSION_LOGON ||
                   w==WTS_SESSION_DESKTOP_READY) {
            app.sessionActive=true;
            refreshMonitors();
            applySchedule(true);
        }
        return 0;
    case WM_DEVICECHANGE:
        if(w==DBT_DEVNODES_CHANGED) SetTimer(hwnd,refreshTimer,350,nullptr);
        return 0;
    case WM_INPUT: {
        const int delta=rawBrightnessDelta(l);
        if (app.sessionActive && delta) {
            POINT cursor{};
            if (GetCursorPos(&cursor)) {
                HMONITOR target=MonitorFromPoint(cursor,MONITOR_DEFAULTTONULL);
                if(target) {
                    if(app.pendingBrightnessMonitor!=target)
                        app.pendingBrightnessDelta=0;
                    app.pendingBrightnessMonitor=target;
                    app.pendingBrightnessDelta=std::clamp(app.pendingBrightnessDelta+delta,-100,100);
                    app.lastRawBrightnessEvent=GetTickCount();
                    SetTimer(hwnd,rawBrightnessTimer,120,nullptr);
                }
            }
        }
        return DefWindowProcW(hwnd,msg,w,l);
    }
    case LocationUpdates::message: {
        auto* coordinates = reinterpret_cast<LocationCoordinates*>(l);
        if (coordinates) {
            if (app.settings.scheduleEnabled && app.settings.scheduleMode == ScheduleMode::Solar &&
                (app.settings.automaticLocation || w != 0)) {
                if (!app.settings.locationConfigured ||
                    app.settings.latitude != coordinates->latitude ||
                    app.settings.longitude != coordinates->longitude) {
                    app.settings.latitude = coordinates->latitude;
                    app.settings.longitude = coordinates->longitude;
                    app.settings.locationConfigured = true;
                    saveSettings(app.settings);
                    // A location update only changes solar transition timing.
                    // Apply monitor/theme changes solely when the schedule state changes.
                    applySchedule(false);
                    app.advanced.refresh();
                }
            }
            delete coordinates;
        }
        return 0;
    }
    case trayMessage: {
        const UINT event = LOWORD(l);
        if (event == WM_MOUSEMOVE) {
            app.lastNearIcon = GetTickCount();
            if (!app.flyout.visible()) SetTimer(hwnd, hoverTimer, 260, nullptr);
        } else if (event == WM_LBUTTONUP || event == NIN_SELECT ||
                   event == NIN_KEYSELECT) {
            if (app.flyout.visible()) hideFlyout();
            else showFlyout();
        } else if (event == WM_RBUTTONUP || event == WM_CONTEXTMENU) {
            KillTimer(hwnd, hoverTimer);
            showContextMenu();
        }
        return 0;
    }
    case refreshMessage:
        KillTimer(hwnd, refreshTimer);
        refreshMonitors();
        return 0;
    case WM_TIMER:
        if (w == hoverTimer) {
            KillTimer(hwnd, hoverTimer);
            POINT pointer{};
            GetCursorPos(&pointer);
            if (contains(iconRectangle(), pointer) && !app.flyout.visible())
                showFlyout();
        } else if (w == leaveTimer) {
            POINT pointer{};
            GetCursorPos(&pointer);
            RECT bounds{};
            if (app.flyout.handle()) GetWindowRect(app.flyout.handle(), &bounds);
            if (GetForegroundWindow() == app.flyout.handle() ||
                GetCapture() == app.flyout.handle() ||
                contains(bounds, pointer) || contains(iconRectangle(), pointer))
                app.lastNearIcon = GetTickCount();
            else if (GetTickCount() - app.lastNearIcon > 480)
                hideFlyout();
        } else if (w == scheduleTimer) {
            applySchedule(false);
        } else if (w == hardwareDebounceTimer) {
            KillTimer(hwnd,hardwareDebounceTimer);
            auto writes=std::move(app.pendingControls);
            app.pendingControls.clear();
            for(const auto& write:writes) {
                for(unsigned i=0;i<app.monitors.size();++i)
                    if(restoreKey(app.monitors[i])==write.monitorKey) {
                        if(write.contrast) changeContrast(i,write.value);
                        else changeBrightness(i,write.value);
                        break;
                    }
            }
        } else if (w == rawBrightnessTimer) {
            KillTimer(hwnd,rawBrightnessTimer);
            const int delta=app.pendingBrightnessDelta;
            const HMONITOR target=app.pendingBrightnessMonitor;
            app.pendingBrightnessDelta=0;
            app.pendingBrightnessMonitor=nullptr;
            if(app.sessionActive && delta) {
                for(unsigned i=0;i<app.monitors.size();++i) {
                    const auto& m=app.monitors[i];
                    if(m.handle==target && m.brightnessAvailable) {
                        const int percent=std::clamp(static_cast<int>(m.brightness)+delta,0,100);
                        changeBrightness(i,static_cast<unsigned>(percent));
                        if(app.monitors[i].brightness==static_cast<unsigned>(percent))
                            syncWindowsBrightness(static_cast<unsigned>(percent));
                    }
                }
            }
        } else if (w == brightnessEventTimer) {
            std::vector<BrightnessKeyEvent> updates;
            if (!app.brightnessKeys.poll(updates)) {
                KillTimer(hwnd, brightnessEventTimer);
        KillTimer(hwnd, rawBrightnessTimer);
        KillTimer(hwnd, hardwareDebounceTimer);
        app.pendingControls.clear();
            } else {
                bool changed = false;
                for (const auto& update : updates) {
                    bool sourcePresent = false;
                    for (auto& monitor : app.monitors) {
                        if (monitor.provider == ControlProvider::Wmi &&
                            monitor.brightnessAvailable &&
                            _wcsicmp(monitor.wmiInstance.c_str(),
                                     update.instance.c_str()) == 0) {
                            sourcePresent = true;
                            if (monitor.brightness != update.percent) {
                                monitor.brightness = update.percent;
                                changed = true;
                            }
                        }
                    }
                    if (!sourcePresent || GetTickCount()-app.lastRawBrightnessEvent<500) continue;
                    POINT cursor{};
                    if (!GetCursorPos(&cursor)) continue;
                    const HMONITOR target = MonitorFromPoint(cursor, MONITOR_DEFAULTTONULL);
                    if (!target) continue;
                    for (unsigned i = 0; i < app.monitors.size(); ++i) {
                        const auto& monitor = app.monitors[i];
                        if (monitor.provider == ControlProvider::Ddc &&
                            monitor.brightnessAvailable && monitor.handle == target) {
                            if (monitor.brightness != update.percent)
                                changeBrightness(i, update.percent);
                        }
                    }
                }
                if (changed) {
                    app.flyout.repaint();
                    app.advanced.refresh();
                }
            }
        } else if (w == refreshTimer) {
            KillTimer(hwnd, refreshTimer);
            refreshMonitors();
        }
        return 0;
    case WM_TIMECHANGE:
    case WM_POWERBROADCAST:
        if (msg == WM_TIMECHANGE || w == PBT_APMRESUMEAUTOMATIC ||
                                    w == PBT_APMRESUMESUSPEND) {
            applySchedule(true);
        }
        return 0;
    case WM_DISPLAYCHANGE:
        SetTimer(hwnd, refreshTimer, 350, nullptr);
        return 0;
    case WM_SETTINGCHANGE:
        if(l && (wcscmp(reinterpret_cast<const wchar_t*>(l),L"TimeZoneInformation")==0 ||
                 wcscmp(reinterpret_cast<const wchar_t*>(l),L"intl")==0)) {
            applySchedule(true);
            return 0;
        }
        if (l && wcscmp(reinterpret_cast<const wchar_t*>(l), L"ImmersiveColorSet") == 0) {
            updateAppearance();
            return 0;
        }
        SetTimer(hwnd, refreshTimer, 350, nullptr);
        return 0;
    case WM_DESTROY: {
        NOTIFYICONDATAW data{};
        data.cbSize = sizeof(data);
        data.hWnd = hwnd;
        data.uID = iconNumber;
        Shell_NotifyIconW(NIM_DELETE, &data);
        KillTimer(hwnd, scheduleTimer);
        KillTimer(hwnd, refreshTimer);
        KillTimer(hwnd, brightnessEventTimer);
        app.brightnessKeys.stop();
        unregisterBrightnessKeys();
        WTSUnRegisterSessionNotification(hwnd);
        if (app.location) {
            app.location->stop();
            app.location->Release();
            app.location = nullptr;
        }
        MSG pending{};
        while (PeekMessageW(&pending, hwnd, LocationUpdates::message,
                            LocationUpdates::message, PM_REMOVE))
            delete reinterpret_cast<LocationCoordinates*>(pending.lParam);
        hideFlyout();
        restoreMonitorBrightness();
        if (app.advanced.handle()) DestroyWindow(app.advanced.handle());
        if (app.flyout.handle()) DestroyWindow(app.flyout.handle());
        PostQuitMessage(0);
        return 0;
    }
    default:
        return DefWindowProcW(hwnd, msg, w, l);
    }
}

int run(HINSTANCE instance) {
    HANDLE singleton = CreateMutexW(nullptr, TRUE, L"Local\\DisplayPilot.Native.SingleInstance");
    if (!singleton || GetLastError() == ERROR_ALREADY_EXISTS) {
        HWND active=FindWindowW(L"DisplayPilotMessageOwner",nullptr);
        if(active) PostMessageW(active,activateExisting,0,0);
        if (singleton) CloseHandle(singleton);
        return 0;
    }
    app.instance = instance;
    app.settings = loadSettings();
    app.supportsThemes = supportsWindowsTheme();
    app.taskbarRestart = RegisterWindowMessageW(L"TaskbarCreated");
    WNDCLASSEXW cls{};
    cls.cbSize = sizeof(cls);
    cls.hInstance = instance;
    cls.lpfnWndProc = ownerProcedure;
    cls.lpszClassName = L"DisplayPilotMessageOwner";
    if (!RegisterClassExW(&cls)) { CloseHandle(singleton); return 31; }
    app.owner = CreateWindowExW(0, cls.lpszClassName,
        L"DisplayPilot", WS_OVERLAPPED, 0, 0, 0, 0,
        nullptr, nullptr, instance, nullptr);
    if (!app.owner) { CloseHandle(singleton); return 32; }
    app.icon = LoadIconW(instance, MAKEINTRESOURCEW(1));
    const int flyoutStage = app.flyout.create(instance, app.owner);
    if (flyoutStage != 0) {
        DestroyWindow(app.owner);
        CloseHandle(singleton);
        return 40 + flyoutStage;
    }
    if (!app.advanced.create(instance)) {
        DestroyWindow(app.owner);
        CloseHandle(singleton);
        return 34;
    }
    app.flyout.brightnessChanged = [](unsigned i,unsigned v) {
        queueManualControl(i,v,false);
    };
    app.flyout.contrastChanged = [](unsigned i,unsigned v) {
        queueManualControl(i,v,true);
    };
    app.flyout.colorChanged = changeColor;
    app.flyout.rotationChanged = changeRotation;
    app.flyout.advancedRequested = showAdvanced;
    app.flyout.refreshRequested = refreshMonitors;
    app.flyout.themeChanged = manualTheme;
    app.flyout.settingsChanged = saveNewSettings;
    app.flyout.locationUpdateRequested = updateLocationNow;
    app.flyout.scheduleChanged = [](bool enabled) {
        app.settings.scheduleEnabled = enabled;
        app.manualThemeOverride = false;
        saveSettings(app.settings);
        syncLocation();
        applySchedule(true);
    };
    app.advanced.brightnessChanged = [](unsigned i,unsigned v) {
        queueManualControl(i,v,false);
    };
    app.advanced.contrastChanged = [](unsigned i,unsigned v) {
        queueManualControl(i,v,true);
    };
    app.advanced.colorChanged = changeColor;
    app.advanced.rotationChanged = changeRotation;
    app.advanced.themeChanged = manualTheme;
    app.advanced.settingsChanged = saveNewSettings;
    app.advanced.locationUpdateRequested = updateLocationNow;
    app.advanced.displaysRefreshRequested = refreshMonitors;
    app.location = new (std::nothrow) LocationUpdates(app.owner);

    WTSRegisterSessionNotification(app.owner,NOTIFY_FOR_THIS_SESSION);
    registerBrightnessKeys(app.owner);
    refreshMonitors();
    syncLocation();
    applySchedule(true);
    if(!updateIcon(true)) showAdvanced();

    MSG message{};
    while (GetMessageW(&message, nullptr, 0, 0) > 0) {
        if (app.flyout.visible() &&
            IsDialogMessageW(app.flyout.handle(), &message)) continue;
        if (app.advanced.visible() &&
            IsDialogMessageW(app.advanced.handle(), &message)) continue;
        TranslateMessage(&message);
        DispatchMessageW(&message);
    }
    CloseHandle(singleton);
    return static_cast<int>(message.wParam);
}
}
}
int WINAPI wWinMain(HINSTANCE instance, HINSTANCE, PWSTR, int) {
    const HRESULT com = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
    if (FAILED(com)) return 21;
    // Security must be initialized before the first WMI proxy is created.
    const HRESULT security = CoInitializeSecurity(nullptr, -1, nullptr, nullptr,
        RPC_C_AUTHN_LEVEL_DEFAULT, RPC_C_IMP_LEVEL_IMPERSONATE, nullptr, EOAC_NONE, nullptr);
    if (FAILED(security) && security != RPC_E_TOO_LATE) {
        CoUninitialize();
        return 22;
    }
    const int result = displaypilot::run(instance);
    CoUninitialize();
    return result;
}
