#include "display.hpp"
#include "flyout.hpp"
#include <windows.h>
#include <shellapi.h>
#include <algorithm>
#include <memory>
#include <string>
#include <vector>

namespace displaypilot {
namespace {
constexpr UINT trayMessage = WM_APP + 10;
constexpr UINT iconNumber = 1;
constexpr UINT hoverTimer = 1;
constexpr UINT leaveTimer = 2;
constexpr UINT commandOpen = 101;
constexpr UINT commandAdvanced = 102;
constexpr UINT commandExit = 103;

struct Application {
    HINSTANCE instance = nullptr;
    HWND owner = nullptr;
    HWND advanced = nullptr;
    HICON icon = nullptr;
    Flyout flyout;
    std::vector<Monitor> monitors;
    UINT taskbarRestart = 0;
    DWORD lastNearIcon = 0;
    bool supportsThemes = false;
} app;

using NotifyRectFn = HRESULT(WINAPI*)(const NOTIFYICONIDENTIFIER*, RECT*);
RECT iconRectangle() {
    RECT result{};
    NOTIFYICONIDENTIFIER id{};
    id.cbSize = sizeof(id);
    id.hWnd = app.owner;
    id.uID = iconNumber;
    HMODULE shell = GetModuleHandleW(L"shell32.dll");
    const auto getRect = shell ? reinterpret_cast<NotifyRectFn>(
        GetProcAddress(shell, "Shell_NotifyIconGetRect")) : nullptr;
    if (getRect && SUCCEEDED(getRect(&id, &result))) return result;
    POINT cursor{};
    GetCursorPos(&cursor);
    return RECT{cursor.x, cursor.y, cursor.x + 1, cursor.y + 1};
}

bool contains(const RECT& area, POINT position) {
    return PtInRect(&area, position) != FALSE;
}

bool isWindows10OrLater() {
    using VersionFn = LONG(WINAPI*)(OSVERSIONINFOW*);
    HMODULE module = GetModuleHandleW(L"ntdll.dll");
    if (!module) return false;
    const auto version = reinterpret_cast<VersionFn>(
        GetProcAddress(module, "RtlGetVersion"));
    if (!version) return false;
    OSVERSIONINFOW info{};
    info.dwOSVersionInfoSize = sizeof(info);
    return version(&info) >= 0 && info.dwMajorVersion >= 10;
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

void refreshMonitors() {
    app.monitors = enumerateMonitors();
    if (app.advanced)
        InvalidateRect(app.advanced, nullptr, TRUE);
}

void showFlyout() {
    KillTimer(app.owner, hoverTimer);
    refreshMonitors();
    app.flyout.show(iconRectangle(), &app.monitors);
    app.lastNearIcon = GetTickCount();
    SetTimer(app.owner, leaveTimer, 180, nullptr);
}

void hideFlyout() {
    app.flyout.hide();
    KillTimer(app.owner, leaveTimer);
}

void setTheme(bool useLight) {
    if (!app.supportsThemes) return;
    HKEY key = nullptr;
    if (RegCreateKeyExW(HKEY_CURRENT_USER,
        L"Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize",
        0, nullptr, 0, KEY_SET_VALUE, nullptr, &key, nullptr) != ERROR_SUCCESS)
        return;
    const DWORD light = useLight ? 1U : 0U;
    RegSetValueExW(key, L"AppsUseLightTheme", 0, REG_DWORD,
                   reinterpret_cast<const BYTE*>(&light), sizeof(light));
    RegSetValueExW(key, L"SystemUsesLightTheme", 0, REG_DWORD,
                   reinterpret_cast<const BYTE*>(&light), sizeof(light));
    RegCloseKey(key);
    DWORD_PTR ignored = 0;
    SendMessageTimeoutW(HWND_BROADCAST, WM_SETTINGCHANGE, 0,
        reinterpret_cast<LPARAM>(L"ImmersiveColorSet"),
        SMTO_ABORTIFHUNG, 1000, &ignored);
}

void showAdvanced() {
    hideFlyout();
    if (!app.advanced) {
        app.advanced = CreateWindowExW(0, L"DisplayPilotAdvanced",
            L"DisplayPilot - Advanced", WS_OVERLAPPEDWINDOW,
            CW_USEDEFAULT, CW_USEDEFAULT, 660, 470,
            nullptr, nullptr, app.instance, nullptr);
    }
    if (!app.advanced) return;
    ShowWindow(app.advanced, SW_SHOWNORMAL);
    SetForegroundWindow(app.advanced);
    InvalidateRect(app.advanced, nullptr, TRUE);
}

void showContextMenu() {
    hideFlyout();
    HMENU menu = CreatePopupMenu();
    if (!menu) return;
    AppendMenuW(menu, MF_STRING, commandOpen, L"Open DisplayPilot");
    AppendMenuW(menu, MF_STRING, commandAdvanced, L"Advanced");
    AppendMenuW(menu, MF_SEPARATOR, 0, nullptr);
    AppendMenuW(menu, MF_STRING, commandExit, L"Exit");
    POINT pos{};
    GetCursorPos(&pos);
    SetForegroundWindow(app.owner);
    const UINT selected = TrackPopupMenuEx(menu,
        TPM_RIGHTBUTTON | TPM_RETURNCMD | TPM_NONOTIFY,
        pos.x, pos.y, app.owner, nullptr);
    DestroyMenu(menu);
    PostMessageW(app.owner, WM_NULL, 0, 0);
    switch (selected) {
    case commandOpen: showFlyout(); break;
    case commandAdvanced: refreshMonitors(); showAdvanced(); break;
    case commandExit: DestroyWindow(app.owner); break;
    default: break;
    }
}

LRESULT CALLBACK advancedProcedure(HWND hwnd, UINT msg, WPARAM w, LPARAM l) {
    switch (msg) {
    case WM_PAINT: {
        PAINTSTRUCT ps{};
        HDC dc = BeginPaint(hwnd, &ps);
        if (!dc) return 0;
        SetBkMode(dc, TRANSPARENT);
        SetTextColor(dc, RGB(35, 45, 55));
        HFONT font = reinterpret_cast<HFONT>(GetStockObject(DEFAULT_GUI_FONT));
        HFONT old = reinterpret_cast<HFONT>(SelectObject(dc, font));
        const std::wstring title = L"DisplayPilot - Advanced monitor information";
        TextOutW(dc, 24, 22, title.c_str(), static_cast<int>(title.size()));
        int y = 66;
        for (const auto& monitor : app.monitors) {
            std::wstring line = monitor.name + L"  |  Brightness: ";
            line += monitor.brightnessAvailable
                 ? std::to_wstring(monitor.brightness) + L"%"
                 : L"unsupported";
            line += L"  |  Contrast: ";
            line += monitor.contrastAvailable
                 ? std::to_wstring(monitor.contrast) + L"%"
                 : L"unsupported";
            TextOutW(dc, 24, y, line.c_str(), static_cast<int>(line.size()));
            y += 32;
        }
        if (app.supportsThemes) {
            const std::wstring hint = L"Press L for Light theme or D for Dark theme.";
            TextOutW(dc, 24, y + 22, hint.c_str(), static_cast<int>(hint.size()));
        }
        SelectObject(dc, old);
        EndPaint(hwnd, &ps);
        return 0;
    }
    case WM_KEYDOWN:
        if (app.supportsThemes && (w == 'L' || w == 'D'))
            setTheme(w == 'L');
        return 0;
    case WM_CLOSE:
        ShowWindow(hwnd, SW_HIDE);
        return 0;
    case WM_DESTROY:
        app.advanced = nullptr;
        return 0;
    default:
        return DefWindowProcW(hwnd, msg, w, l);
    }
}

LRESULT CALLBACK ownerProcedure(HWND hwnd, UINT msg, WPARAM w, LPARAM l) {
    if (msg == app.taskbarRestart && app.taskbarRestart != 0) {
        updateIcon(true);
        return 0;
    }
    switch (msg) {
    case trayMessage: {
        const UINT event = LOWORD(l);
        if (event == WM_MOUSEMOVE) {
            app.lastNearIcon = GetTickCount();
            SetTimer(hwnd, hoverTimer, 260, nullptr);
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
    case WM_TIMER:
        if (w == hoverTimer) {
            KillTimer(hwnd, hoverTimer);
            POINT pos{};
            GetCursorPos(&pos);
            if (contains(iconRectangle(), pos) && !app.flyout.visible())
                showFlyout();
        } else if (w == leaveTimer) {
            POINT pos{};
            GetCursorPos(&pos);
            RECT flyout{};
            GetWindowRect(app.flyout.handle(), &flyout);
            const bool inside = contains(flyout, pos) ||
                                contains(iconRectangle(), pos);
            if (inside) app.lastNearIcon = GetTickCount();
            else if (GetTickCount() - app.lastNearIcon > 480)
                hideFlyout();
        }
        return 0;
    case WM_DISPLAYCHANGE:
    case WM_SETTINGCHANGE:
        refreshMonitors();
        return 0;
    case WM_DESTROY: {
        NOTIFYICONDATAW data{};
        data.cbSize = sizeof(data);
        data.hWnd = hwnd;
        data.uID = iconNumber;
        Shell_NotifyIconW(NIM_DELETE, &data);
        hideFlyout();
        if (app.advanced) DestroyWindow(app.advanced);
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
    app.supportsThemes = isWindows10OrLater();
    app.taskbarRestart = RegisterWindowMessageW(L"TaskbarCreated");
    WNDCLASSEXW ownerClass{};
    ownerClass.cbSize = sizeof(ownerClass);
    ownerClass.hInstance = instance;
    ownerClass.lpfnWndProc = ownerProcedure;
    ownerClass.lpszClassName = L"DisplayPilotMessageOwner";
    if (!RegisterClassExW(&ownerClass)) { CloseHandle(singleton); return 1; }
    WNDCLASSEXW advancedClass{};
    advancedClass.cbSize = sizeof(advancedClass);
    advancedClass.hInstance = instance;
    advancedClass.lpfnWndProc = advancedProcedure;
    advancedClass.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    advancedClass.hbrBackground = reinterpret_cast<HBRUSH>(COLOR_WINDOW + 1);
    advancedClass.lpszClassName = L"DisplayPilotAdvanced";
    if (!RegisterClassExW(&advancedClass)) { CloseHandle(singleton); return 1; }

    app.owner = CreateWindowExW(0, ownerClass.lpszClassName,
         L"DisplayPilot", WS_OVERLAPPED, 0, 0, 0, 0,
         nullptr, nullptr, instance, nullptr);
    if (!app.owner) { CloseHandle(singleton); return 1; }
    app.icon = LoadIconW(instance, MAKEINTRESOURCEW(1));
    if (!app.flyout.create(instance, app.owner)) {
        DestroyWindow(app.owner);
        CloseHandle(singleton);
        return 1;
    }
    app.flyout.brightnessChanged = [](unsigned index, unsigned value) {
        if (index < app.monitors.size()) {
            if (!setBrightness(app.monitors[index], value))
                refreshMonitors();
        }
    };
    updateIcon(true);
    MSG message{};
    while (GetMessageW(&message, nullptr, 0, 0) > 0) {
        TranslateMessage(&message);
        DispatchMessageW(&message);
    }
    if (app.flyout.handle()) DestroyWindow(app.flyout.handle());
    CloseHandle(singleton);
    return static_cast<int>(message.wParam);
}
}
}

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE, PWSTR, int) {
    const HRESULT com = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
    if (FAILED(com)) return 1;
    const int result = displaypilot::run(instance);
    CoUninitialize();
    return result;
}
