#pragma once
#include "display.hpp"
#include "settings.hpp"
#include <windows.h>
#include <functional>
#include <vector>

namespace displaypilot {
class AdvancedWindow {
public:
    AdvancedWindow() = default;
    AdvancedWindow(const AdvancedWindow&) = delete;
    AdvancedWindow& operator=(const AdvancedWindow&) = delete;
    ~AdvancedWindow();

    bool create(HINSTANCE instance);
    void show(std::vector<Monitor>* monitors, Settings* settings, bool appearance);
    void refresh();
    void synchronizeMonitorControls();
    void refreshLocation();
    void hide();
    bool visible() const;
    HWND handle() const { return window_; }

    std::function<void(unsigned, unsigned)> brightnessChanged;
    std::function<void(unsigned, unsigned)> contrastChanged;
    std::function<void(unsigned, unsigned)> colorChanged;
    std::function<void(unsigned, unsigned)> rotationChanged;
    std::function<void(bool)> themeChanged;
    std::function<bool(const Settings&)> settingsChanged;
    std::function<bool()> locationUpdateRequested;
    std::function<void()> displaysRefreshRequested;

private:
    struct RowControls {
        HWND label = nullptr, brightness = nullptr, contrast = nullptr;
        HWND color = nullptr, colorApply = nullptr;
        HWND rotation = nullptr, rotationApply = nullptr;
        std::vector<HWND> members;
    };
    HINSTANCE instance_ = nullptr;
    HWND window_ = nullptr;
    WNDPROC originalProcedure_ = nullptr;
    HFONT font_ = nullptr;
    HFONT titleFont_ = nullptr;
    HICON appIcon_ = nullptr;
    HWND brandIcon_ = nullptr, brandName_ = nullptr, brandVersion_ = nullptr;
    std::vector<Monitor>* monitors_ = nullptr;
    Settings* settings_ = nullptr;
    bool themesAvailable_ = false;
    std::vector<RowControls> rows_;
    std::vector<HWND> children_;
    HWND scheduleCheckbox_ = nullptr, brightnessCheckbox_ = nullptr;
    HWND mode_ = nullptr, lightTime_ = nullptr, darkTime_ = nullptr;
    HWND latitude_ = nullptr, longitude_ = nullptr, label_ = nullptr;
    HWND automaticLocation_ = nullptr, updateLocation_ = nullptr;
    HWND startup_ = nullptr, status_ = nullptr;
    HWND save_ = nullptr, diagnostics_ = nullptr;
    HWND rescan_ = nullptr, probe_ = nullptr, preview_ = nullptr, solarTimes_ = nullptr;
    HWND displaysHeading_ = nullptr, scheduleHeading_ = nullptr, integrationHeading_ = nullptr;
    HWND lightCaption_ = nullptr, darkCaption_ = nullptr;
    HWND latitudeCaption_ = nullptr, longitudeCaption_ = nullptr, locationCaption_ = nullptr;
    int dpi_ = 96;
    int px(int logical) const { return MulDiv(logical, dpi_, 96); }
    int contentHeight_ = 0;
    int scroll_ = 0;

    void rebuild();
    void updateFonts();
    void layout();
    void scrollBy(int delta);
    void onCommand(WORD id, WORD notification);
    void onScroll(HWND control);
    bool saveInput();
    void previewSchedule();
    void copyDiagnostics();
    static LRESULT CALLBACK procedure(HWND hwnd, UINT message, WPARAM w, LPARAM l);
};
}
