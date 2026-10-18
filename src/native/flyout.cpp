#include "flyout.hpp"
#include <d2d1helper.h>
#include <algorithm>
#include <cmath>
#include <string>

namespace displaypilot {
namespace {
float windowScale(HWND window) {
    using DpiFn = UINT(WINAPI*)(HWND);
    static auto dpi = reinterpret_cast<DpiFn>(
        GetProcAddress(GetModuleHandleW(L"user32.dll"), "GetDpiForWindow"));
    if (dpi) return std::max(1.0f, dpi(window) / 96.0f);
    HDC context = GetDC(window);
    const float scale = context ? std::max(1.0f,
                              GetDeviceCaps(context, LOGPIXELSX) / 96.0f) : 1.0f;
    if (context) ReleaseDC(window, context);
    return scale;
}
int nearest(int value, int minimum, int maximum) {
    return std::max(minimum, std::min(maximum, value));
}
template<class T> void release(T*& object) {
    if (object) object->Release();
    object = nullptr;
}
}

Flyout::~Flyout() {
    releaseTarget();
    release(heading_);
    release(text_);
    release(writeFactory_);
    release(factory_);
}

bool Flyout::create(HINSTANCE instance, HWND owner) {
    WNDCLASSEXW wc{};
    wc.cbSize = sizeof(wc);
    wc.hInstance = instance;
    wc.lpfnWndProc = procedure;
    wc.lpszClassName = L"DisplayPilotFlyout";
    wc.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    if (!RegisterClassExW(&wc) && GetLastError() != ERROR_CLASS_ALREADY_EXISTS)
        return false;

    window_ = CreateWindowExW(WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE,
              wc.lpszClassName, L"DisplayPilot", WS_POPUP | WS_CLIPCHILDREN,
              0, 0, width_, height_, owner, nullptr, instance, this);
    if (!window_) return false;

    if (FAILED(D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED, &factory_)))
        return false;
    if (FAILED(DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED,
        __uuidof(IDWriteFactory), reinterpret_cast<IUnknown**>(&writeFactory_))))
        return false;

    if (FAILED(writeFactory_->CreateTextFormat(L"Segoe UI", nullptr,
        DWRITE_FONT_WEIGHT_NORMAL, DWRITE_FONT_STYLE_NORMAL,
        DWRITE_FONT_STRETCH_NORMAL, 13.0f, L"en-us", &text_)))
        return false;
    if (FAILED(writeFactory_->CreateTextFormat(L"Segoe UI", nullptr,
        DWRITE_FONT_WEIGHT_SEMI_BOLD, DWRITE_FONT_STYLE_NORMAL,
        DWRITE_FONT_STRETCH_NORMAL, 16.0f, L"en-us", &heading_)))
        return false;
    return true;
}

bool Flyout::visible() const {
    return window_ && IsWindowVisible(window_) != FALSE;
}

void Flyout::show(const RECT& anchor, std::vector<Monitor>* monitors) {
    if (!window_) return;
    monitors_ = monitors;
    scale_ = windowScale(window_);
    const int number = static_cast<int>(monitors ? monitors->size() : 0);
    width_ = static_cast<int>(370 * scale_);
    height_ = static_cast<int>((number == 0 ? 132 : 94 + 88 * number) * scale_);
    // Keep the panel within the active work area. Scrolling will be added
    // when extended monitor lists are implemented.
    POINT center{(anchor.left + anchor.right) / 2, (anchor.top + anchor.bottom) / 2};
    MONITORINFO info{};
    info.cbSize = sizeof(info);
    HMONITOR monitor = MonitorFromPoint(center, MONITOR_DEFAULTTONEAREST);
    if (!GetMonitorInfoW(monitor, &info)) return;
    RECT work = info.rcWork;
    width_ = std::min(width_, static_cast<int>(work.right - work.left));
    height_ = std::min(height_, static_cast<int>(work.bottom - work.top));
    const int x = nearest(anchor.left, work.left, work.right - width_);
    const int y = anchor.top > work.top + (work.bottom - work.top) / 2
        ? std::max(work.top, anchor.top - height_)
        : std::min(work.bottom - height_, anchor.bottom);
    releaseTarget();
    SetWindowPos(window_, HWND_TOPMOST, x, y, width_, height_,
                 SWP_NOACTIVATE | SWP_SHOWWINDOW);
    HRGN corners = CreateRoundRectRgn(0, 0, width_ + 1, height_ + 1,
                                    static_cast<int>(16 * scale_),
                                    static_cast<int>(16 * scale_));
    if (corners && !SetWindowRgn(window_, corners, TRUE)) DeleteObject(corners);
    InvalidateRect(window_, nullptr, FALSE);
}

void Flyout::hide() {
    dragIndex_ = -1;
    if (window_) ShowWindow(window_, SW_HIDE);
}

void Flyout::releaseTarget() {
    release(brush_);
    release(target_);
}

int Flyout::hitSlider(int y) const {
    if (!monitors_) return -1;
    const float logicalY = y / scale_;
    for (unsigned i = 0; i < monitors_->size(); ++i) {
        if (!(*monitors_)[i].brightnessAvailable) continue;
        const float track = 124.0f + 88.0f * i;
        if (std::abs(logicalY - track) <= 15.0f)
            return static_cast<int>(i);
    }
    return -1;
}

void Flyout::moveSlider(int x) {
    if (dragIndex_ < 0 || !monitors_ ||
        static_cast<unsigned>(dragIndex_) >= monitors_->size()) return;
    const float logicalX = x / scale_;
    const unsigned percent = static_cast<unsigned>(nearest(
        static_cast<int>(std::lround((logicalX - 22.0f) * 100.0f / 326.0f)),
        0, 100));
    auto& monitor = (*monitors_)[static_cast<unsigned>(dragIndex_)];
    if (percent != monitor.brightness) {
        monitor.brightness = percent;
        if (brightnessChanged)
            brightnessChanged(static_cast<unsigned>(dragIndex_), percent);
        InvalidateRect(window_, nullptr, FALSE);
    }
}

void Flyout::paint() {
    PAINTSTRUCT ps{};
    BeginPaint(window_, &ps);
    if (!factory_ || !writeFactory_) { EndPaint(window_, &ps); return; }
    if (!target_) {
        RECT size{};
        GetClientRect(window_, &size);
        const auto dimensions = D2D1::SizeU(static_cast<UINT>(size.right),
                                           static_cast<UINT>(size.bottom));
        if (FAILED(factory_->CreateHwndRenderTarget(
            D2D1::RenderTargetProperties(),
            D2D1::HwndRenderTargetProperties(window_, dimensions), &target_))) {
            EndPaint(window_, &ps);
            return;
        }
        target_->SetDpi(96 * scale_, 96 * scale_);
        if (FAILED(target_->CreateSolidColorBrush(
                D2D1::ColorF(0xE7EDF5), &brush_))) {
            releaseTarget();
            EndPaint(window_, &ps);
            return;
        }
    }

    target_->BeginDraw();
    target_->Clear(D2D1::ColorF(0x172331));
    auto label = [this](const std::wstring& value, float x, float y,
                        float w, float h, IDWriteTextFormat* format,
                        unsigned color) {
        brush_->SetColor(D2D1::ColorF(color));
        target_->DrawText(value.c_str(), static_cast<UINT32>(value.size()),
            format, D2D1::RectF(x, y, x + w, y + h), brush_);
    };
    label(L"DisplayPilot", 22, 13, 300, 32, heading_, 0xFFFFFF);
    label(L"Monitor controls", 22, 45, 300, 26, text_, 0xACBCCE);

    if (!monitors_ || monitors_->empty()) {
        label(L"No compatible DDC/CI monitors detected", 22, 86,
              328, 32, text_, 0xCED8E4);
    } else {
        for (unsigned i = 0; i < monitors_->size(); ++i) {
            const auto& monitor = (*monitors_)[i];
            const float top = 83.0f + i * 88.0f;
            if (top > static_cast<float>(height_) / scale_) break;
            label(monitor.name, 22, top, 270, 28, text_, 0xEDF2F9);
            if (monitor.brightnessAvailable) {
                label(std::to_wstring(monitor.brightness) + L"%", 304,
                      top, 44, 28, text_, 0xEDF2F9);
                brush_->SetColor(D2D1::ColorF(0x536477));
                target_->FillRoundedRectangle(D2D1::RoundedRect(
                    D2D1::RectF(22, top + 38, 348, top + 45), 4, 4), brush_);
                brush_->SetColor(D2D1::ColorF(0x65C6FF));
                const float end = 22 + 326 * (monitor.brightness / 100.0f);
                target_->FillRoundedRectangle(D2D1::RoundedRect(
                    D2D1::RectF(22, top + 38, std::max(23.0f, end), top + 45),
                    4, 4), brush_);
                target_->FillEllipse(D2D1::Ellipse(D2D1::Point2F(
                    end, top + 41.5f), 8, 8), brush_);
            } else {
                label(L"Brightness unsupported", 22, top + 31, 300,
                      26, text_, 0xACBCCE);
            }
        }
    }
    if (target_->EndDraw() == D2DERR_RECREATE_TARGET)
        releaseTarget();
    EndPaint(window_, &ps);
}

LRESULT CALLBACK Flyout::procedure(HWND hwnd, UINT msg, WPARAM w, LPARAM l) {
    auto* self = reinterpret_cast<Flyout*>(GetWindowLongPtrW(hwnd, GWLP_USERDATA));
    if (msg == WM_NCCREATE) {
        const auto* cs = reinterpret_cast<CREATESTRUCTW*>(l);
        self = static_cast<Flyout*>(cs->lpCreateParams);
        SetWindowLongPtrW(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(self));
    }
    if (!self) return DefWindowProcW(hwnd, msg, w, l);
    switch (msg) {
    case WM_PAINT:
        self->paint();
        return 0;
    case WM_ERASEBKGND:
        return 1;
    case WM_LBUTTONDOWN: {
        const int x = static_cast<short>(LOWORD(l));
        const int y = static_cast<short>(HIWORD(l));
        self->dragIndex_ = self->hitSlider(y);
        if (self->dragIndex_ >= 0) {
            SetCapture(hwnd);
            self->moveSlider(x);
        }
        return 0;
    }
    case WM_MOUSEMOVE:
        if (self->dragIndex_ >= 0)
            self->moveSlider(static_cast<short>(LOWORD(l)));
        return 0;
    case WM_LBUTTONUP:
        if (self->dragIndex_ >= 0) {
            ReleaseCapture();
            self->dragIndex_ = -1;
        }
        return 0;
    case WM_CAPTURECHANGED:
        self->dragIndex_ = -1;
        return 0;
    case WM_DESTROY:
        self->releaseTarget();
        self->window_ = nullptr;
        return 0;
    default:
        return DefWindowProcW(hwnd, msg, w, l);
    }
}
}
