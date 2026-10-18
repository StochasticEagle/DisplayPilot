#pragma once
#include "display.hpp"
#include <windows.h>
#include <d2d1.h>
#include <dwrite.h>
#include <functional>
#include <vector>

namespace displaypilot {
class Flyout {
public:
    Flyout() = default;
    Flyout(const Flyout&) = delete;
    Flyout& operator=(const Flyout&) = delete;
    ~Flyout();

    bool create(HINSTANCE instance, HWND owner);
    void show(const RECT& anchor, std::vector<Monitor>* monitors);
    void hide();
    bool visible() const;
    HWND handle() const { return window_; }
    void setAppearance(bool supported, bool dark, bool scheduled);
    void repaint() const;

    std::function<void(unsigned, unsigned)> brightnessChanged;
    std::function<void(unsigned, unsigned)> contrastChanged;
    std::function<void(unsigned, unsigned)> colorChanged;
    std::function<void()> advancedRequested;
    std::function<void(bool)> themeChanged;
    std::function<void(bool)> scheduleChanged;

private:
    enum class Control { None, Brightness, Contrast };
    HWND window_ = nullptr;
    std::vector<Monitor>* monitors_ = nullptr;
    ID2D1Factory* factory_ = nullptr;
    ID2D1HwndRenderTarget* target_ = nullptr;
    ID2D1SolidColorBrush* brush_ = nullptr;
    IDWriteFactory* writeFactory_ = nullptr;
    IDWriteTextFormat* text_ = nullptr;
    IDWriteTextFormat* heading_ = nullptr;
    float scale_ = 1.0f;
    int width_ = 394;
    int height_ = 170;
    int dragIndex_ = -1;
    Control dragControl_ = Control::None;
    float scroll_ = 0.0f;
    float footer_ = 80.0f;
    bool themeSupported_ = false;
    bool themeDark_ = false;
    bool scheduleEnabled_ = false;

    float rowHeight(const Monitor& monitor) const;
    float contentHeight() const;
    void paint();
    void releaseTarget();
    void moveSlider(int physicalX);
    void click(int physicalX, int physicalY);
    bool hitSlider(float x, float y);
    static LRESULT CALLBACK procedure(HWND window, UINT message, WPARAM w, LPARAM l);
};
}
