#include "display.hpp"
#include "flyout.hpp"
#include "advanced.hpp"
#include "settings.hpp"
#include "wmi_events.hpp"
#include "location.hpp"
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
constexpr UINT commandOpen = 101;
constexpr UINT commandAdvanced = 102;
constexpr UINT commandExit = 103;
constexpr UINT commandLight = 104;
constexpr UINT commandDark = 105;
constexpr UINT commandSchedule = 106;

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
    UINT taskbarRestart = 0;
    DWORD lastNearIcon = 0;
    bool supportsThemes = false;
    bool scheduleApplied = false;
    bool lastDark = false;
    bool savedBrightness = false;
} app;

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
void updateIcon(bool add) {
    NOTIFYICONDATAW data{};
    data.cbSize = sizeof(data);
    data.hWnd = app.owner;
    data.uID = iconNumber;
    data.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
    data.uCallbackMessage = trayMessage;
    data.hIcon = app.icon;
    lstrcpynW(data.szTip, L"DisplayPilot", ARRAYSIZE(data.szTip));
    if (Shell_NotifyIconW(add ? NIM_ADD : NIM_MODIFY, &data)) {
        data.uVersion = NOTIFYICON_VERSION_4;
        Shell_NotifyIconW(NIM_SETVERSION, &data);
    }
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
    app.savedBrightness = pending;
    if (changed) queueRefresh();
}
void applyNightBrightness() {
    if (app.savedBrightness) return;
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
                saveBrightness(key, baseline);
            }
        }
        const unsigned target = std::max(10U, baseline / 2);
        if (setBrightness(monitor, target)) any = true;
        else removeSavedBrightness(key);
    }
    app.savedBrightness = any;
    queueRefresh();
}
void applySchedule(bool force) {
    KillTimer(app.owner, scheduleTimer);
    SYSTEMTIME now{};
    GetLocalTime(&now);
    const auto state = evaluateSchedule(app.settings, now);
    if (!state.valid) {
        restoreMonitorBrightness();
        app.scheduleApplied = false;
        SetTimer(app.owner, scheduleTimer, 60000, nullptr);
        updateAppearance();
        return;
    }
    if (force || !app.scheduleApplied || app.lastDark != state.dark) {
        if (app.supportsThemes) setWindowsTheme(state.dark);
        if (state.dark && app.settings.reduceBrightness)
            applyNightBrightness();
        else restoreMonitorBrightness();
        app.lastDark = state.dark;
        app.scheduleApplied = true;
    } else if (!state.dark || !app.settings.reduceBrightness) {
        restoreMonitorBrightness();
    }
    // Re-evaluate every minute for system time/time-zone adjustments.
    SetTimer(app.owner, scheduleTimer,
             std::max(1000UL, std::min(60000UL, state.millisecondsUntilChange)), nullptr);
    updateAppearance();
}
void manualTheme(bool dark) {
    if (!app.supportsThemes) return;
    if (app.settings.scheduleEnabled) {
        app.settings.scheduleEnabled = false;
        saveSettings(app.settings);
        syncLocation();
    }
    if (app.savedBrightness) restoreMonitorBrightness();
    app.scheduleApplied = false;
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
void updateLocationNow() {
    if (!app.location || !app.settings.scheduleEnabled ||
        app.settings.scheduleMode != ScheduleMode::Solar) return;
    app.location->updateNow();
}
void saveNewSettings(const Settings& next) {
    const bool previousEnabled = app.settings.scheduleEnabled;
    app.settings = next;
    saveSettings(app.settings);
    syncLocation();
    if (previousEnabled && !next.scheduleEnabled && app.savedBrightness)
        restoreMonitorBrightness();
    applySchedule(true);
    app.advanced.refresh();
}
void changeBrightness(unsigned index, unsigned value) {
    if (index >= app.monitors.size()) return;
    // Manual control cancels pending schedule restoration for this monitor.
    const auto key = restoreKey(app.monitors[index]);
    if (app.savedBrightness) {
        removeSavedBrightness(key);
        if (key != legacyRestoreKey(app.monitors[index]))
            removeSavedBrightness(legacyRestoreKey(app.monitors[index]));
    }
    if (setBrightness(app.monitors[index], value)) {
        app.monitors[index].brightness = value;
        app.flyout.repaint();
    } else queueRefresh();
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
    app.flyout.show(iconRectangle(), &app.monitors);
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
        updateIcon(true);
        return 0;
    }
    switch (msg) {
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
                    applySchedule(true);
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
        } else if (w == brightnessEventTimer) {
            std::vector<BrightnessKeyEvent> updates;
            if (!app.brightnessKeys.poll(updates)) {
                KillTimer(hwnd, brightnessEventTimer);
            } else {
                bool changed = false;
                for (const auto& update : updates) {
                    for (auto& monitor : app.monitors) {
                        if (monitor.provider == ControlProvider::Wmi &&
                            _wcsicmp(monitor.wmiInstance.c_str(),
                                     update.instance.c_str()) == 0 &&
                            monitor.brightness != update.percent) {
                            monitor.brightness = update.percent;
                            changed = true;
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
    app.flyout.brightnessChanged = changeBrightness;
    app.flyout.contrastChanged = changeContrast;
    app.flyout.colorChanged = changeColor;
    app.flyout.advancedRequested = showAdvanced;
    app.flyout.themeChanged = manualTheme;
    app.flyout.scheduleChanged = [](bool enabled) {
        app.settings.scheduleEnabled = enabled;
        saveSettings(app.settings);
        syncLocation();
        applySchedule(true);
    };
    app.advanced.brightnessChanged = changeBrightness;
    app.advanced.contrastChanged = changeContrast;
    app.advanced.colorChanged = changeColor;
    app.advanced.rotationChanged = changeRotation;
    app.advanced.themeChanged = manualTheme;
    app.advanced.settingsChanged = saveNewSettings;
    app.advanced.locationUpdateRequested = updateLocationNow;
    app.location = new (std::nothrow) LocationUpdates(app.owner);

    refreshMonitors();
    syncLocation();
    applySchedule(true);
    updateIcon(true);

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
