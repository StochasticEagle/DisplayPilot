#pragma once
#include "display.hpp"
#include "settings.hpp"
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

    int create(HINSTANCE instance, HWND owner);
    void show(const RECT& anchor, std::vector<Monitor>* monitors, Settings* settings);
    void hide();
    bool visible() const;
    HWND handle() const { return window_; }
    void setAppearance(bool supported, bool dark, bool scheduled);
    void repaint() const;
    void refreshLocation();

    std::function<void(unsigned, unsigned)> brightnessChanged;
    std::function<void(unsigned, unsigned)> contrastChanged;
    std::function<void(unsigned, unsigned)> colorChanged;
    std::function<void(unsigned, unsigned)> rotationChanged;
    std::function<void()> advancedRequested;
    std::function<void()> refreshRequested;
    std::function<void(bool)> themeChanged;
    std::function<void(bool)> scheduleChanged;
    std::function<bool(const Settings&)> settingsChanged;
    std::function<bool()> locationUpdateRequested;

private:
    struct MonitorControls {
        HWND name = nullptr;
        HWND brightnessLabel = nullptr;
        HWND brightness = nullptr;
        HWND contrastLabel = nullptr;
        HWND contrast = nullptr;
        HWND colorLabel = nullptr;
        HWND color = nullptr;
        HWND rotationLabel = nullptr;
        HWND rotation = nullptr;
        HWND rotationApply = nullptr;
    };
    HINSTANCE instance_ = nullptr;
    HWND window_ = nullptr;
    WNDPROC originalProcedure_ = nullptr;
    HFONT font_ = nullptr;
    std::vector<Monitor>* monitors_ = nullptr;
    Settings* settings_ = nullptr;
    RECT anchor_{};
    std::vector<HWND> children_;
    std::vector<MonitorControls> rows_;
    HWND title_ = nullptr;
    HWND empty_ = nullptr;
    HWND themeLight_ = nullptr;
    HWND themeDarkButton_ = nullptr;
    HWND schedule_ = nullptr;
    HWND advanced_ = nullptr;
    HWND refresh_ = nullptr;
    HWND options_ = nullptr, scheduleMode_ = nullptr;
    HWND lightTime_ = nullptr, darkTime_ = nullptr;
    HWND latitude_ = nullptr, longitude_ = nullptr, locationLabel_ = nullptr;
    HWND reduce_ = nullptr, saveSchedule_ = nullptr, scheduleStatus_ = nullptr;
    HWND solarUpdate_ = nullptr, timeCaption_ = nullptr, coordinateCaption_ = nullptr;
    bool scheduleExpanded_ = false;
    int scroll_ = 0;
    int contentHeight_ = 0;
    int dpi_ = 96;
    bool themeSupported_ = false;
    bool themeDark_ = false;
    bool scheduleEnabled_ = false;
    bool updating_ = false;

    int px(int logical) const;
    void rebuild();
    void layout();
    void scrollBy(int delta);
    void synchronize();
    void onCommand(WORD id, WORD notification);
    void onTrackbar(HWND control);
    void resizeToContent();
    bool saveSchedule();
    static LRESULT CALLBACK procedure(HWND window, UINT message, WPARAM w, LPARAM l);
};
}
