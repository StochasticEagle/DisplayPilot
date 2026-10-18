#pragma once
#include "display.hpp"
#include <windows.h>
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
    struct MonitorControls {
        HWND name = nullptr;
        HWND brightnessLabel = nullptr;
        HWND brightness = nullptr;
        HWND contrastLabel = nullptr;
        HWND contrast = nullptr;
        HWND colorLabel = nullptr;
        HWND color = nullptr;
    };
    HINSTANCE instance_ = nullptr;
    HWND window_ = nullptr;
    HFONT font_ = nullptr;
    std::vector<Monitor>* monitors_ = nullptr;
    std::vector<HWND> children_;
    std::vector<MonitorControls> rows_;
    HWND title_ = nullptr;
    HWND empty_ = nullptr;
    HWND themeLight_ = nullptr;
    HWND themeDark_ = nullptr;
    HWND schedule_ = nullptr;
    HWND advanced_ = nullptr;
    int scroll_ = 0;
    int contentHeight_ = 0;
    int dpi_ = 96;
    bool themeSupported_ = false;
    bool themeDark_ = false;
    bool scheduleEnabled_ = false;
    bool updating_ = false;

    int px(int logical) const;
    HWND add(const wchar_t* kind, const wchar_t* caption, DWORD style, int id);
    void rebuild();
    void layout();
    void scrollBy(int delta);
    void synchronize();
    void onCommand(WORD id, WORD notification);
    void onTrackbar(HWND control);
    static LRESULT CALLBACK procedure(HWND window, UINT message, WPARAM w, LPARAM l);
};
}
