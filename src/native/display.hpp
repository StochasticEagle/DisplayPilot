#pragma once
#include <windows.h>
#include <string>
#include <vector>

namespace displaypilot {
struct Monitor {
    HMONITOR handle = nullptr;
    unsigned physicalIndex = 0;
    std::wstring name;
    RECT bounds{};
    bool brightnessAvailable = false;
    bool contrastAvailable = false;
    unsigned brightness = 0;   // normalized 0 to 100
    unsigned contrast = 0;
};

std::vector<Monitor> enumerateMonitors();
bool setBrightness(const Monitor& monitor, unsigned percentage);
bool setContrast(const Monitor& monitor, unsigned percentage);
}
