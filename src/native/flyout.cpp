#include "flyout.hpp"
#include <d2d1helper.h>
#include <algorithm>
#include <cmath>
#include <string>

namespace displaypilot {
namespace {
template<class T> void release(T*& pointer) {
    if (pointer) pointer->Release();
    pointer = nullptr;
}
float dpiScale(HWND window) {
    using DpiForWindow = UINT(WINAPI*)(HWND);
    HMODULE module = GetModuleHandleW(L"user32.dll");
    const auto getDpi = module ? reinterpret_cast<DpiForWindow>(
        GetProcAddress(module, "GetDpiForWindow")) : nullptr;
    if (getDpi) return std::max(1.0f, getDpi(window) / 96.0f);
    HDC dc = GetDC(window);
    float scale = dc ? std::max(1.0f, GetDeviceCaps(dc, LOGPIXELSX) / 96.0f) : 1.0f;
    if (dc) ReleaseDC(window, dc);
    return scale;
}
int clampInt(int value, int lo, int hi) {
    return std::max(lo, std::min(hi, value));
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
    WNDCLASSEXW cls{};
    cls.cbSize = sizeof(cls);
    cls.hInstance = instance;
    cls.lpfnWndProc = procedure;
    cls.lpszClassName = L"DisplayPilotFlyout";
    cls.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    if (!RegisterClassExW(&cls) && GetLastError() != ERROR_CLASS_ALREADY_EXISTS)
        return false;
    window_ = CreateWindowExW(WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE,
        cls.lpszClassName, L"DisplayPilot", WS_POPUP | WS_CLIPCHILDREN,
        0, 0, 394, 170, owner, nullptr, instance, this);
    if (!window_) return false;
    if (FAILED(D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED, &factory_)))
        return false;
    if (FAILED(DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED, __uuidof(IDWriteFactory),
        reinterpret_cast<IUnknown**>(&writeFactory_)))) return false;
    if (FAILED(writeFactory_->CreateTextFormat(L"Segoe UI", nullptr,
        DWRITE_FONT_WEIGHT_NORMAL, DWRITE_FONT_STYLE_NORMAL,
        DWRITE_FONT_STRETCH_NORMAL, 13.0f, L"en-us", &text_))) return false;
    if (FAILED(writeFactory_->CreateTextFormat(L"Segoe UI", nullptr,
        DWRITE_FONT_WEIGHT_SEMI_BOLD, DWRITE_FONT_STYLE_NORMAL,
        DWRITE_FONT_STRETCH_NORMAL, 17.0f, L"en-us", &heading_))) return false;
    return true;
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
void Flyout::repaint() const {
    if (window_) InvalidateRect(window_, nullptr, FALSE);
}
float Flyout::rowHeight(const Monitor& row) const {
    return 55.0f + (row.brightnessAvailable ? 42.0f : 0.0f) +
           (row.contrastAvailable ? 38.0f : 0.0f) +
           (row.colorAvailable ? 35.0f : 0.0f);
}
float Flyout::contentHeight() const {
    float y = 79.0f;
    if (!monitors_ || monitors_->empty()) y += 44.0f;
    else for (const auto& monitor : *monitors_) y += rowHeight(monitor);
    y += 10.0f;
    y += themeSupported_ ? 76.0f : 36.0f;
    return y + 48.0f;
}
void Flyout::show(const RECT& anchor, std::vector<Monitor>* monitors) {
    if (!window_) return;
    monitors_ = monitors;
    scroll_ = 0.0f;
    scale_ = dpiScale(window_);
    const POINT center{(anchor.left + anchor.right)/2, (anchor.top + anchor.bottom)/2};
    MONITORINFO info{};
    info.cbSize = sizeof(info);
    if (!GetMonitorInfoW(MonitorFromPoint(center, MONITOR_DEFAULTTONEAREST), &info))
        return;
    const RECT work = info.rcWork;
    width_ = std::min(static_cast<int>(394 * scale_), static_cast<int>(work.right - work.left));
    height_ = std::min(static_cast<int>(contentHeight() * scale_),
                       static_cast<int>(work.bottom - work.top));
    const int x = clampInt(anchor.left, work.left, work.right - width_);
    const int y = (anchor.top > work.top + (work.bottom - work.top)/2)
        ? std::max(static_cast<int>(work.top), static_cast<int>(anchor.top - height_))
        : std::min(static_cast<int>(work.bottom - height_), static_cast<int>(anchor.bottom));
    releaseTarget();
    SetWindowPos(window_, HWND_TOPMOST, x, y, width_, height_,
                 SWP_NOACTIVATE | SWP_SHOWWINDOW);
    HRGN rounded = CreateRoundRectRgn(0, 0, width_ + 1, height_ + 1,
                                     static_cast<int>(18*scale_),
                                     static_cast<int>(18*scale_));
    if (rounded && !SetWindowRgn(window_, rounded, TRUE)) DeleteObject(rounded);
    repaint();
}
void Flyout::hide() {
    dragControl_ = Control::None;
    dragIndex_ = -1;
    if (window_) ShowWindow(window_, SW_HIDE);
}
void Flyout::releaseTarget() {
    release(brush_);
    release(target_);
}
bool Flyout::hitSlider(float x, float y) {
    if (!monitors_ || x < 15 || x > 378) return false;
    float top = 79 - scroll_;
    for (unsigned i = 0; i < monitors_->size(); ++i) {
        const auto& m = (*monitors_)[i];
        float at = top + 37;
        if (m.brightnessAvailable) {
            if (std::abs(y - at) < 17) {
                dragIndex_ = static_cast<int>(i);
                dragControl_ = Control::Brightness;
                return true;
            }
            at += 42;
        }
        if (m.contrastAvailable) {
            if (std::abs(y - at) < 17) {
                dragIndex_ = static_cast<int>(i);
                dragControl_ = Control::Contrast;
                return true;
            }
            at += 38;
        }
        top += rowHeight(m);
    }
    return false;
}
void Flyout::moveSlider(int physicalX) {
    if (!monitors_ || dragControl_ == Control::None || dragIndex_ < 0 ||
        static_cast<size_t>(dragIndex_) >= monitors_->size()) return;
    const unsigned value = static_cast<unsigned>(clampInt(
        static_cast<int>(std::lround((physicalX / scale_ - 24.0f) * 100 / 344)),
        0, 100));
    auto& monitor = (*monitors_)[static_cast<size_t>(dragIndex_)];
    if (dragControl_ == Control::Brightness && monitor.brightness != value) {
        monitor.brightness = value;
        if (brightnessChanged) brightnessChanged(static_cast<unsigned>(dragIndex_), value);
    } else if (dragControl_ == Control::Contrast && monitor.contrast != value) {
        monitor.contrast = value;
        if (contrastChanged) contrastChanged(static_cast<unsigned>(dragIndex_), value);
    }
    repaint();
}
void Flyout::click(int px, int py) {
    const float x = px / scale_;
    const float y = py / scale_;
    if (hitSlider(x, y)) {
        SetCapture(window_);
        moveSlider(px);
        return;
    }
    float top = 79 - scroll_;
    if (monitors_) {
        for (unsigned i = 0; i < monitors_->size(); ++i) {
            const auto& m = (*monitors_)[i];
            const float colorY = top + 47 +
                (m.brightnessAvailable ? 42.0f : 0.0f) +
                (m.contrastAvailable ? 38.0f : 0.0f);
            if (m.colorAvailable && y >= colorY && y <= colorY + 31 &&
                x >= 20 && x < 370 && !m.colorPresets.empty()) {
                const auto next = std::find(m.colorPresets.begin(),
                                            m.colorPresets.end(), m.colorPreset);
                const auto index = next == m.colorPresets.end() ?
                    0U : static_cast<unsigned>((next - m.colorPresets.begin() + 1) %
                         m.colorPresets.size());
                if (colorChanged) colorChanged(i, m.colorPresets[index]);
                repaint();
                return;
            }
            top += rowHeight(m);
        }
    } else top += 44;
    top += 10;
    if (themeSupported_) {
        if (y >= top && y < top + 39) {
            if (x >= 22 && x < 185 && themeChanged) themeChanged(false);
            else if (x >= 195 && x <= 369 && themeChanged) themeChanged(true);
            return;
        }
        top += 40;
    }
    if (y >= top && y < top + 34 && scheduleChanged) {
        scheduleChanged(!scheduleEnabled_);
        return;
    }
    top += 36;
    if (y >= top && y < top + 45 && advancedRequested) advancedRequested();
}
void Flyout::paint() {
    PAINTSTRUCT ps{};
    BeginPaint(window_, &ps);
    if (!factory_ || !writeFactory_) { EndPaint(window_, &ps); return; }
    if (!target_) {
        RECT size{};
        GetClientRect(window_, &size);
        if (FAILED(factory_->CreateHwndRenderTarget(
            D2D1::RenderTargetProperties(),
            D2D1::HwndRenderTargetProperties(window_,
                D2D1::SizeU(static_cast<UINT>(size.right),
                            static_cast<UINT>(size.bottom))), &target_))) {
            EndPaint(window_, &ps);
            return;
        }
        target_->SetDpi(scale_ * 96, scale_ * 96);
        if (FAILED(target_->CreateSolidColorBrush(D2D1::ColorF(0xFAFBFD), &brush_))) {
            releaseTarget();
            EndPaint(window_, &ps);
            return;
        }
    }
    auto color = [this](unsigned rgb) { brush_->SetColor(D2D1::ColorF(rgb)); };
    auto textAt = [this, &color](const std::wstring& label,
          float x, float y, float width, float height, unsigned rgb,
          IDWriteTextFormat* format) {
        color(rgb);
        target_->DrawText(label.c_str(), static_cast<UINT32>(label.size()), format,
            D2D1::RectF(x, y, x + width, y + height), brush_);
    };
    auto pill = [this, &color](float left,float top,float right,float bottom,
                            unsigned fill) {
        color(fill);
        target_->FillRoundedRectangle(D2D1::RoundedRect(
             D2D1::RectF(left,top,right,bottom),8,8),brush_);
    };
    auto slider = [this,&color,&textAt](const std::wstring& name,
                                unsigned value,float y) {
        textAt(name, 24, y-36, 270, 23, 0xA8BACB, text_);
        textAt(std::to_wstring(value) + L"%", 320, y-36, 55, 23,
               0xFFFFFF, text_);
        color(0x46596C);
        target_->FillRoundedRectangle(D2D1::RoundedRect(
            D2D1::RectF(24,y-2,368,y+5),4,4),brush_);
        color(0x67C5FF);
        float right = 24 + value*3.44f;
        if (right > 24)
            target_->FillRoundedRectangle(D2D1::RoundedRect(
                D2D1::RectF(24,y-2,right,y+5),4,4),brush_);
        target_->FillEllipse(D2D1::Ellipse(D2D1::Point2F(right,y+1.5f),8,8),brush_);
    };
    target_->BeginDraw();
    target_->Clear(D2D1::ColorF(0x182434));
    textAt(L"DisplayPilot", 22, 12, 320, 31, 0xFFFFFF, heading_);
    textAt(L"MONITORS", 22, 49, 240, 23, 0xAABDD0, text_);
    color(0x3B4B60);
    target_->DrawLine(D2D1::Point2F(20,74),D2D1::Point2F(374,74),brush_,1);
    float top = 79 - scroll_;
    if (!monitors_ || monitors_->empty()) {
        textAt(L"No monitors detected", 24, top, 345, 30, 0xC6D3E1,text_);
        top += 44;
    } else {
        for (const auto& monitor : *monitors_) {
            const float rowBottom = top + rowHeight(monitor);
            if (rowBottom < 75 || top > height_ / scale_) { top = rowBottom; continue; }
            textAt(monitor.name, 24, top+4, 335, 28, 0xFFFFFF, text_);
            float controlY = top + 37;
            if (monitor.brightnessAvailable) {
                slider(L"Brightness", monitor.brightness, controlY);
                controlY += 42;
            }
            if (monitor.contrastAvailable) {
                slider(L"Contrast", monitor.contrast, controlY);
                controlY += 38;
            }
            if (monitor.colorAvailable) {
                pill(23, controlY-1, 368, controlY+27, 0x2D455C);
                textAt(L"Color temperature: " + colorPresetLabel(monitor.colorPreset)
                         + L"  >", 32, controlY+3, 330, 24, 0xE9F4FF, text_);
            }
            color(0x3A4C60);
            target_->DrawLine(D2D1::Point2F(22,rowBottom-4),
                              D2D1::Point2F(370,rowBottom-4),brush_,1);
            top = rowBottom;
        }
    }
    top += 10;
    if (themeSupported_) {
        pill(22,top,185,top+36,themeDark_?0x304355:0x3E91D0);
        pill(195,top,370,top+36,themeDark_?0x3E91D0:0x304355);
        textAt(L"Light theme",39,top+6,145,26,0xFFFFFF,text_);
        textAt(L"Dark theme",218,top+6,145,26,0xFFFFFF,text_);
        top += 40;
    }
    pill(22,top,370,top+34,scheduleEnabled_?0x355A62:0x304355);
    textAt(scheduleEnabled_?L"Automatic schedule: ON":L"Automatic schedule: OFF",
           38,top+5,320,25,0xFFFFFF,text_);
    top += 36;
    pill(22,top,370,top+42,0x294C69);
    textAt(L"Advanced settings    >",42,top+9,305,28,0xFFFFFF,text_);
    const HRESULT end = target_->EndDraw();
    if (end == D2DERR_RECREATE_TARGET) releaseTarget();
    EndPaint(window_, &ps);
}
LRESULT CALLBACK Flyout::procedure(HWND hwnd, UINT msg, WPARAM w, LPARAM l) {
    auto* self = reinterpret_cast<Flyout*>(GetWindowLongPtrW(hwnd,GWLP_USERDATA));
    if (msg == WM_NCCREATE) {
        self = static_cast<Flyout*>(reinterpret_cast<CREATESTRUCTW*>(l)->lpCreateParams);
        SetWindowLongPtrW(hwnd,GWLP_USERDATA,reinterpret_cast<LONG_PTR>(self));
    }
    if (!self) return DefWindowProcW(hwnd,msg,w,l);
    switch (msg) {
    case WM_PAINT: self->paint(); return 0;
    case WM_ERASEBKGND: return 1;
    case WM_MOUSEWHEEL: {
        self->scroll_ = std::max(0.0f,std::min(
            std::max(0.0f,self->contentHeight() - self->height_/self->scale_),
            self->scroll_ - (static_cast<short>(HIWORD(w))/120.0f)*35.0f));
        self->repaint();
        return 0;
    }
    case WM_LBUTTONDOWN:
        self->click(static_cast<short>(LOWORD(l)),static_cast<short>(HIWORD(l)));
        return 0;
    case WM_MOUSEMOVE:
        if (self->dragControl_ != Control::None)
            self->moveSlider(static_cast<short>(LOWORD(l)));
        return 0;
    case WM_LBUTTONUP:
        if (self->dragControl_ != Control::None) {
            self->moveSlider(static_cast<short>(LOWORD(l)));
            ReleaseCapture();
            self->dragControl_ = Control::None;
            self->dragIndex_ = -1;
        }
        return 0;
    case WM_CAPTURECHANGED:
        self->dragControl_ = Control::None;
        self->dragIndex_ = -1;
        return 0;
    case WM_DESTROY:
        self->releaseTarget();
        self->window_ = nullptr;
        return 0;
    default: return DefWindowProcW(hwnd,msg,w,l);
    }
}
}
