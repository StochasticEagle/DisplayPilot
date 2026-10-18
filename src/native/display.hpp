#pragma once
#include <windows.h>
#include <string>
#include <vector>

namespace displaypilot {
enum class ControlProvider { Ddc, Wmi };

struct Monitor {
    ControlProvider provider = ControlProvider::Ddc;
    HMONITOR handle = nullptr;
    unsigned physicalIndex = 0;
    std::wstring name;
    std::wstring deviceName;
    std::wstring wmiInstance;
    RECT bounds{};
    bool brightnessAvailable = false;
    bool contrastAvailable = false;
    bool colorAvailable = false;
    bool rotationAvailable = false;
    unsigned brightness = 0;
    unsigned contrast = 0;
    unsigned colorPreset = 0;
    unsigned rotation = 0;
    std::vector<unsigned> colorPresets;
};

std::vector<Monitor> enumerateMonitors();
bool setBrightness(const Monitor& monitor, unsigned percentage);
bool setContrast(const Monitor& monitor, unsigned percentage);
bool setColorPreset(const Monitor& monitor, unsigned preset);
bool setRotation(const Monitor& monitor, unsigned orientation);
std::wstring rotationLabel(unsigned orientation);
std::wstring colorPresetLabel(unsigned preset);
}
