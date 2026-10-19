#include "flyout.hpp"
#include "resource.h"
#include "ui_platform.hpp"
#include <commctrl.h>
#include <algorithm>
#include <string>

namespace displaypilot {
namespace {
constexpr int firstMonitor = 2000;
constexpr int lightId = 100;
constexpr int darkId = 101;
constexpr int scheduleId = 102;
constexpr int advancedId = 103;
constexpr int scrollStep = 40;

int windowDpi(HWND window) {
    using DpiFunction = UINT (WINAPI*)(HWND);
    const HMODULE module = GetModuleHandleW(L"user32.dll");
    const auto getDpi = module ? reinterpret_cast<DpiFunction>(
        GetProcAddress(module, "GetDpiForWindow")) : nullptr;
    if (getDpi) return static_cast<int>(getDpi(window));
    HDC dc = GetDC(window);
    const int dpi = dc ? GetDeviceCaps(dc, LOGPIXELSX) : 96;
    if (dc) ReleaseDC(window, dc);
    return dpi;
}
}

Flyout::~Flyout() {
    if (window_ && IsWindow(window_)) DestroyWindow(window_);
    if (font_) DeleteObject(font_);
}

int Flyout::px(int logical) const {
    return MulDiv(logical, dpi_, 96);
}

int Flyout::create(HINSTANCE instance, HWND owner) {
    instance_ = instance;
    INITCOMMONCONTROLSEX controls{};
    controls.dwSize = sizeof(controls);
    controls.dwICC = ICC_BAR_CLASSES;
    if (!InitCommonControlsEx(&controls)) return 1;
    window_ = CreateDialogParamW(instance, MAKEINTRESOURCEW(IDD_DISPLAYPILOT_FLYOUT),
        owner, [](HWND, UINT, WPARAM, LPARAM) -> INT_PTR { return FALSE; }, 0);
    if (!window_) return 3;
    SetWindowLongPtrW(window_, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(this));
    originalProcedure_ = reinterpret_cast<WNDPROC>(
        SetWindowLongPtrW(window_, GWLP_WNDPROC, reinterpret_cast<LONG_PTR>(procedure)));
    SetWindowPos(window_, nullptr, 0, 0, 304, 200,
        SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE);
    dpi_ = windowDpi(window_);
    NONCLIENTMETRICSW metrics{};
    metrics.cbSize = sizeof(metrics);
    if (SystemParametersInfoW(SPI_GETNONCLIENTMETRICS, sizeof(metrics), &metrics, 0))
        font_ = CreateFontIndirectW(&metrics.lfMessageFont);
    if (!font_) font_ = CreateFontW(-MulDiv(9, dpi_, 72), 0, 0, 0, FW_NORMAL,
        FALSE, FALSE, FALSE, DEFAULT_CHARSET, OUT_DEFAULT_PRECIS,
        CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_SWISS,
        L"Segoe UI");
    applyNativeWindowChrome(window_);
    return 0;
}

bool Flyout::visible() const {
    return window_ && IsWindowVisible(window_) != FALSE;
}

void Flyout::setAppearance(bool supported, bool dark, bool scheduled) {
    themeSupported_ = supported;
    themeDark_ = dark;
    scheduleEnabled_ = scheduled;
    repaint();
}

HWND Flyout::add(const wchar_t* kind, const wchar_t* caption, DWORD style, int id) {
    HWND child = CreateWindowExW(0, kind, caption, WS_CHILD | WS_VISIBLE | style,
        0, 0, 10, 10, window_, reinterpret_cast<HMENU>(static_cast<INT_PTR>(id)),
        instance_, nullptr);
    if (child) {
        SendMessageW(child, WM_SETFONT, reinterpret_cast<WPARAM>(font_), FALSE);
        children_.push_back(child);
    }
    return child;
}

void Flyout::rebuild() {
    if (!window_) return;
    for (HWND child : children_) DestroyWindow(child);
    children_.clear();
    rows_.clear();
    title_ = empty_ = themeLight_ = themeDarkButton_ = schedule_ = advanced_ = nullptr;
    title_ = GetDlgItem(window_, IDC_FLY_TITLE);
    empty_ = GetDlgItem(window_, IDC_FLY_EMPTY);
    ShowWindow(empty_, (!monitors_ || monitors_->empty()) ? SW_SHOW : SW_HIDE);
    if (monitors_) for (size_t i = 0; i < monitors_->size(); ++i) {
        const auto& monitor = (*monitors_)[i];
        const int base = firstMonitor + static_cast<int>(i) * 10;
        MonitorControls row{};
        row.name = add(L"STATIC", monitor.name.c_str(), SS_LEFT, 0);
        if (monitor.brightnessAvailable) {
            row.brightnessLabel = add(L"STATIC", L"Brightness", SS_LEFT, 0);
            row.brightness = add(TRACKBAR_CLASSW, L"",
                TBS_HORZ | TBS_NOTICKS | WS_TABSTOP, base);
            if (row.brightness) {
                SendMessageW(row.brightness, TBM_SETRANGE, FALSE, MAKELPARAM(0, 100));
                SendMessageW(row.brightness, TBM_SETPOS, TRUE, monitor.brightness);
            }
        }
        if (monitor.contrastAvailable) {
            row.contrastLabel = add(L"STATIC", L"Contrast", SS_LEFT, 0);
            row.contrast = add(TRACKBAR_CLASSW, L"",
                TBS_HORZ | TBS_NOTICKS | WS_TABSTOP, base + 1);
            if (row.contrast) {
                SendMessageW(row.contrast, TBM_SETRANGE, FALSE, MAKELPARAM(0, 100));
                SendMessageW(row.contrast, TBM_SETPOS, TRUE, monitor.contrast);
            }
        }
        if (monitor.colorAvailable && !monitor.colorPresets.empty()) {
            row.colorLabel = add(L"STATIC", L"Color temperature", SS_LEFT, 0);
            row.color = add(L"COMBOBOX", L"",
                CBS_DROPDOWNLIST | WS_VSCROLL | WS_TABSTOP, base + 2);
            if (row.color) {
                for (unsigned preset : monitor.colorPresets) {
                    const auto name = colorPresetLabel(preset);
                    const LRESULT index = SendMessageW(row.color, CB_ADDSTRING, 0,
                        reinterpret_cast<LPARAM>(name.c_str()));
                    if (index != CB_ERR && index != CB_ERRSPACE)
                        SendMessageW(row.color, CB_SETITEMDATA,
                                     static_cast<WPARAM>(index), preset);
                }
            }
        }
        rows_.push_back(row);
    }
    themeLight_ = GetDlgItem(window_, lightId);
    themeDarkButton_ = GetDlgItem(window_, darkId);
    ShowWindow(themeLight_, themeSupported_ ? SW_SHOW : SW_HIDE);
    ShowWindow(themeDarkButton_, themeSupported_ ? SW_SHOW : SW_HIDE);
    schedule_ = GetDlgItem(window_, scheduleId);
    advanced_ = GetDlgItem(window_, advancedId);
    layout();
    synchronize();
}

void Flyout::layout() {
    if (!window_) return;
    RECT client{};
    GetClientRect(window_, &client);
    const int width = client.right - client.left;
    const int height = client.bottom - client.top;
    const int margin = px(18);
    const int available = std::max(px(120), width - margin * 2);
    int y = px(14) - scroll_;
    auto place = [](HWND child, int x, int top, int w, int h) {
        if (child) MoveWindow(child, x, top, w, h, TRUE);
    };
    place(title_, margin, y, available, px(26));
    y += px(38);
    if (empty_ && (!monitors_ || monitors_->empty())) {
        place(empty_, margin, y, available, px(30));
        y += px(42);
    }
    for (const auto& row : rows_) {
        place(row.name, margin, y, available, px(26));
        y += px(28);
        auto control = [&](HWND label, HWND field) {
            if (!field) return;
            place(label, margin + px(4), y + px(3), px(105), px(25));
            place(field, margin + px(112), y, std::max(px(85), available - px(112)),
                  px(30));
            y += px(38);
        };
        control(row.brightnessLabel, row.brightness);
        control(row.contrastLabel, row.contrast);
        if (row.color) {
            place(row.colorLabel, margin + px(4), y + px(4), px(105), px(25));
            place(row.color, margin + px(112), y,
                  std::max(px(85), available - px(112)), px(240));
            y += px(39);
        }
        y += px(12);
    }
    if (themeSupported_) {
        place(themeLight_, margin, y, available / 2, px(28));
        place(themeDarkButton_, margin + available / 2, y, available / 2, px(28));
        y += px(36);
    }
    place(schedule_, margin, y, available, px(29));
    y += px(40);
    place(advanced_, margin, y, available, px(33));
    y += px(48);
    contentHeight_ = y + scroll_;
    SCROLLINFO info{};
    info.cbSize = sizeof(info);
    info.fMask = SIF_RANGE | SIF_PAGE | SIF_POS;
    info.nMin = 0;
    info.nMax = std::max(0, contentHeight_ - 1);
    info.nPage = static_cast<UINT>(std::max(0, height));
    info.nPos = scroll_;
    SetScrollInfo(window_, SB_VERT, &info, TRUE);
}

void Flyout::scrollBy(int delta) {
    if (!window_) return;
    RECT client{};
    GetClientRect(window_, &client);
    const int maxScroll = std::max(0, contentHeight_ - static_cast<int>(client.bottom - client.top));
    const int next = std::clamp(scroll_ + delta, 0, maxScroll);
    if (next == scroll_) return;
    scroll_ = next;
    layout();
}

void Flyout::synchronize() {
    if (!window_ || updating_) return;
    updating_ = true;
    if (monitors_) {
        for (size_t i = 0; i < rows_.size() && i < monitors_->size(); ++i) {
            const auto& monitor = (*monitors_)[i];
            const auto& row = rows_[i];
            if (row.name) SetWindowTextW(row.name, monitor.name.c_str());
            if (row.brightness)
                SendMessageW(row.brightness, TBM_SETPOS, TRUE, monitor.brightness);
            if (row.contrast)
                SendMessageW(row.contrast, TBM_SETPOS, TRUE, monitor.contrast);
            if (row.color) {
                const LRESULT count = SendMessageW(row.color, CB_GETCOUNT, 0, 0);
                for (LRESULT item = 0; item < count; ++item) {
                    if (static_cast<unsigned>(SendMessageW(row.color, CB_GETITEMDATA,
                        static_cast<WPARAM>(item), 0)) == monitor.colorPreset) {
                        SendMessageW(row.color, CB_SETCURSEL,
                                     static_cast<WPARAM>(item), 0);
                        break;
                    }
                }
            }
        }
    }
    if (themeLight_) SendMessageW(themeLight_, BM_SETCHECK,
                                  themeDark_ ? BST_UNCHECKED : BST_CHECKED, 0);
    if (themeDarkButton_) SendMessageW(themeDarkButton_, BM_SETCHECK,
                                 themeDark_ ? BST_CHECKED : BST_UNCHECKED, 0);
    if (schedule_) SendMessageW(schedule_, BM_SETCHECK,
                                scheduleEnabled_ ? BST_CHECKED : BST_UNCHECKED, 0);
    updating_ = false;
}

void Flyout::repaint() const {
    auto* self = const_cast<Flyout*>(this);
    if (!window_ || !visible()) return;
    if (!monitors_ || rows_.size() != monitors_->size()) self->rebuild();
    else self->synchronize();
}

void Flyout::show(const RECT& anchor, std::vector<Monitor>* monitors) {
    if (!window_) return;
    monitors_ = monitors;
    dpi_ = windowDpi(window_);
    scroll_ = 0;
    rebuild();
    RECT client{};
    GetClientRect(window_, &client);
    const int width = px(304);
    // The first layout computes the required content height.
    const int desiredHeight = contentHeight_ + px(4);
    const POINT center{(anchor.left + anchor.right) / 2,
                       (anchor.top + anchor.bottom) / 2};
    MONITORINFO info{};
    info.cbSize = sizeof(info);
    if (!GetMonitorInfoW(MonitorFromPoint(center, MONITOR_DEFAULTTONEAREST), &info))
        return;
    const RECT work = info.rcWork;
    const int actualWidth = std::min(width, static_cast<int>(work.right - work.left));
    const int actualHeight = std::min(desiredHeight, static_cast<int>(work.bottom - work.top));
    const int x = std::clamp(anchor.left, work.left, work.right - actualWidth);
    const int y = anchor.top > work.top + (work.bottom - work.top) / 2
        ? std::max(work.top, anchor.top - actualHeight)
        : std::min(work.bottom - actualHeight, anchor.bottom);
    SetWindowPos(window_, HWND_TOPMOST, x, y, actualWidth, actualHeight,
                 SWP_NOACTIVATE | SWP_SHOWWINDOW);
    layout();
    synchronize();
}

void Flyout::hide() {
    if (window_) ShowWindow(window_, SW_HIDE);
}

void Flyout::onTrackbar(HWND control) {
    if (!control || !monitors_ || updating_) return;
    const int id = GetDlgCtrlID(control);
    if (id < firstMonitor) return;
    const size_t index = static_cast<size_t>((id - firstMonitor) / 10);
    const int field = (id - firstMonitor) % 10;
    if (index >= monitors_->size()) return;
    const unsigned value = static_cast<unsigned>(SendMessageW(control, TBM_GETPOS, 0, 0));
    auto& monitor = (*monitors_)[index];
    if (field == 0 && monitor.brightnessAvailable && monitor.brightness != value) {
        monitor.brightness = value;
        if (brightnessChanged) brightnessChanged(static_cast<unsigned>(index), value);
    } else if (field == 1 && monitor.contrastAvailable && monitor.contrast != value) {
        monitor.contrast = value;
        if (contrastChanged) contrastChanged(static_cast<unsigned>(index), value);
    }
}

void Flyout::onCommand(WORD id, WORD notification) {
    if (updating_) return;
    if (notification == BN_CLICKED) {
        if (id == lightId && themeChanged) themeChanged(false);
        else if (id == darkId && themeChanged) themeChanged(true);
        else if (id == scheduleId && scheduleChanged)
            scheduleChanged(SendMessageW(schedule_, BM_GETCHECK, 0, 0) == BST_CHECKED);
        else if (id == advancedId && advancedRequested) advancedRequested();
    }
    if (notification == CBN_SELCHANGE && id >= firstMonitor && monitors_) {
        const size_t index = static_cast<size_t>((id - firstMonitor) / 10);
        if (index >= rows_.size() || index >= monitors_->size() ||
            (id - firstMonitor) % 10 != 2) return;
        HWND combo = rows_[index].color;
        const LRESULT choice = SendMessageW(combo, CB_GETCURSEL, 0, 0);
        if (choice != CB_ERR && colorChanged) {
            const unsigned preset = static_cast<unsigned>(
                SendMessageW(combo, CB_GETITEMDATA, static_cast<WPARAM>(choice), 0));
            colorChanged(static_cast<unsigned>(index), preset);
        }
    }
}

LRESULT CALLBACK Flyout::procedure(HWND hwnd, UINT message, WPARAM w, LPARAM l) {
    auto* self = reinterpret_cast<Flyout*>(GetWindowLongPtrW(hwnd, GWLP_USERDATA));
    if (message == WM_NCCREATE) {
        self = static_cast<Flyout*>(reinterpret_cast<CREATESTRUCTW*>(l)->lpCreateParams);
        SetWindowLongPtrW(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(self));
    }
    if (!self) return DefWindowProcW(hwnd, message, w, l);
    switch (message) {
    case WM_ERASEBKGND: {
        RECT bounds{};
        GetClientRect(hwnd, &bounds);
        FillRect(reinterpret_cast<HDC>(w), &bounds, GetSysColorBrush(COLOR_WINDOW));
        return TRUE;
    }
    case WM_CTLCOLORDLG:
        return reinterpret_cast<LRESULT>(GetSysColorBrush(COLOR_WINDOW));
    case WM_CTLCOLORSTATIC:
        SetBkMode(reinterpret_cast<HDC>(w), TRANSPARENT);
        SetTextColor(reinterpret_cast<HDC>(w), GetSysColor(COLOR_WINDOWTEXT));
        return reinterpret_cast<LRESULT>(GetSysColorBrush(COLOR_WINDOW));
    case WM_COMMAND:
        self->onCommand(LOWORD(w), HIWORD(w));
        return 0;
    case WM_HSCROLL:
        if (l) self->onTrackbar(reinterpret_cast<HWND>(l));
        return 0;
    case WM_VSCROLL:
        if (!l) {
            switch (LOWORD(w)) {
            case SB_LINEUP: self->scrollBy(-self->px(scrollStep)); break;
            case SB_LINEDOWN: self->scrollBy(self->px(scrollStep)); break;
            case SB_PAGEUP: self->scrollBy(-self->px(220)); break;
            case SB_PAGEDOWN: self->scrollBy(self->px(220)); break;
            case SB_THUMBTRACK:
            case SB_THUMBPOSITION: {
                SCROLLINFO info{};
                info.cbSize = sizeof(info);
                info.fMask = SIF_TRACKPOS;
                if (GetScrollInfo(hwnd, SB_VERT, &info))
                    self->scrollBy(info.nTrackPos - self->scroll_);
                break;
            }
            }
        }
        return 0;
    case WM_MOUSEWHEEL:
        self->scrollBy(-GET_WHEEL_DELTA_WPARAM(w) / WHEEL_DELTA *
                       self->px(scrollStep));
        return 0;
    case WM_SIZE:
        self->layout();
        return 0;
    case WM_DPICHANGED: {
        self->dpi_ = HIWORD(w);
        const RECT* bounds = reinterpret_cast<const RECT*>(l);
        SetWindowPos(hwnd, nullptr, bounds->left, bounds->top,
            bounds->right - bounds->left, bounds->bottom - bounds->top,
            SWP_NOZORDER | SWP_NOACTIVATE);
        self->layout();
        return 0;
    }
    case WM_DESTROY:
        self->children_.clear();
        self->rows_.clear();
        self->window_ = nullptr;
        return 0;
    default:
        return CallWindowProcW(self->originalProcedure_, hwnd, message, w, l);
    }
}
}
