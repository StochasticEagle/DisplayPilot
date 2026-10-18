#include "display.hpp"
#include <physicalmonitorenumerationapi.h>
#include <highlevelmonitorconfigurationapi.h>
#include <algorithm>
#include <vector>

namespace displaypilot {
namespace {
using PhysicalList = std::vector<PHYSICAL_MONITOR>;

PhysicalList openPhysicalMonitors(HMONITOR monitor) {
    DWORD count = 0;
    if (!GetNumberOfPhysicalMonitorsFromHMONITOR(monitor, &count) || count == 0)
        return {};
    PhysicalList result(count);
    if (!GetPhysicalMonitorsFromHMONITOR(monitor, count, result.data()))
        return {};
    return result;
}

void closePhysicalMonitors(PhysicalList& physical) {
    if (!physical.empty())
        DestroyPhysicalMonitors(static_cast<DWORD>(physical.size()), physical.data());
    physical.clear();
}

unsigned normalized(DWORD value, DWORD minimum, DWORD maximum) {
    if (maximum <= minimum) return 0;
    return static_cast<unsigned>(
        (100ULL * (value - minimum) + (maximum - minimum) / 2) / (maximum - minimum));
}

DWORD rawPercent(unsigned percentage, DWORD minimum, DWORD maximum) {
    return minimum + static_cast<DWORD>(
        (static_cast<unsigned long long>(std::min(percentage, 100U)) *
         (maximum - minimum) + 50) / 100);
}

BOOL CALLBACK enumerateCallback(HMONITOR monitor, HDC, LPRECT, LPARAM cookie) {
    auto& monitors = *reinterpret_cast<std::vector<Monitor>*>(cookie);
    MONITORINFOEXW info{};
    info.cbSize = sizeof(info);
    if (!GetMonitorInfoW(monitor, &info)) return TRUE;
    auto physical = openPhysicalMonitors(monitor);
    for (unsigned index = 0; index < physical.size(); ++index) {
        Monitor row;
        row.handle = monitor;
        row.physicalIndex = index;
        row.bounds = info.rcMonitor;
        row.name = physical[index].szPhysicalMonitorDescription;
        if (row.name.empty())
            row.name = info.szDevice;
        DWORD minimum = 0, value = 0, maximum = 0;
        if (GetMonitorBrightness(physical[index].hPhysicalMonitor,
                                 &minimum, &value, &maximum)) {
            row.brightnessAvailable = true;
            row.brightness = normalized(value, minimum, maximum);
        }
        if (GetMonitorContrast(physical[index].hPhysicalMonitor,
                               &minimum, &value, &maximum)) {
            row.contrastAvailable = true;
            row.contrast = normalized(value, minimum, maximum);
        }
        monitors.push_back(std::move(row));
    }
    closePhysicalMonitors(physical);
    // Internal panels which do not expose a DXVA2 handle are handled by WMI in
    // the next hardware-provider implementation phase.
    return TRUE;
}
}

std::vector<Monitor> enumerateMonitors() {
    std::vector<Monitor> output;
    EnumDisplayMonitors(nullptr, nullptr, enumerateCallback,
                        reinterpret_cast<LPARAM>(&output));
    return output;
}

bool setBrightness(const Monitor& monitor, unsigned percentage) {
    auto physical = openPhysicalMonitors(monitor.handle);
    if (monitor.physicalIndex >= physical.size()) {
        closePhysicalMonitors(physical);
        return false;
    }
    HANDLE handle = physical[monitor.physicalIndex].hPhysicalMonitor;
    DWORD minimum = 0, current = 0, maximum = 0;
    const bool valid = GetMonitorBrightness(handle, &minimum, &current, &maximum) != FALSE;
    const bool success = valid && SetMonitorBrightness(handle,
                            rawPercent(percentage, minimum, maximum)) != FALSE;
    closePhysicalMonitors(physical);
    return success;
}

bool setContrast(const Monitor& monitor, unsigned percentage) {
    auto physical = openPhysicalMonitors(monitor.handle);
    if (monitor.physicalIndex >= physical.size()) {
        closePhysicalMonitors(physical);
        return false;
    }
    HANDLE handle = physical[monitor.physicalIndex].hPhysicalMonitor;
    DWORD minimum = 0, current = 0, maximum = 0;
    const bool valid = GetMonitorContrast(handle, &minimum, &current, &maximum) != FALSE;
    const bool success = valid && SetMonitorContrast(handle,
                            rawPercent(percentage, minimum, maximum)) != FALSE;
    closePhysicalMonitors(physical);
    return success;
}
}
