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
    std::function<void(unsigned, unsigned)> brightnessChanged;

private:
    HWND window_ = nullptr;
    std::vector<Monitor>* monitors_ = nullptr;
    ID2D1Factory* factory_ = nullptr;
    ID2D1HwndRenderTarget* target_ = nullptr;
    ID2D1SolidColorBrush* brush_ = nullptr;
    IDWriteFactory* writeFactory_ = nullptr;
    IDWriteTextFormat* text_ = nullptr;
    IDWriteTextFormat* heading_ = nullptr;
    float scale_ = 1;
    int width_ = 370;
    int height_ = 160;
    int dragIndex_ = -1;

    void paint();
    void releaseTarget();
    void moveSlider(int x);
    int hitSlider(int y) const;
    static LRESULT CALLBACK procedure(HWND window, UINT message, WPARAM w, LPARAM l);
};
}
