#include "display.hpp"
#include <physicalmonitorenumerationapi.h>
#include <highlevelmonitorconfigurationapi.h>
#include <lowlevelmonitorconfigurationapi.h>
#include <wbemidl.h>
#include <oleauto.h>
#include <algorithm>
#include <cctype>
#include <cwctype>
#include <cstdlib>
#include <string>
#include <cwchar>
#include <utility>

namespace displaypilot {
namespace {
template<typename T> class Com {
public:
    T* value = nullptr;
    ~Com() { if (value) value->Release(); }
    Com() = default;
    Com(const Com&) = delete;
    Com& operator=(const Com&) = delete;
    T** out() { return &value; }
    T* operator->() const { return value; }
    explicit operator bool() const { return value != nullptr; }
};

class Bstr {
public:
    explicit Bstr(const wchar_t* text) : value_(SysAllocString(text)) {}
    ~Bstr() { SysFreeString(value_); }
    operator BSTR() const { return value_; }
private:
    BSTR value_;
};
class Variant {
public:
    VARIANT value{};
    Variant() { VariantInit(&value); }
    ~Variant() { VariantClear(&value); }
    Variant(const Variant&) = delete;
    Variant& operator=(const Variant&) = delete;
};

using PhysicalList = std::vector<PHYSICAL_MONITOR>;
PhysicalList openPhysical(HMONITOR logical) {
    DWORD count = 0;
    if (!GetNumberOfPhysicalMonitorsFromHMONITOR(logical, &count) || count == 0 || count > 32)
        return {};
    PhysicalList handles(count);
    if (!GetPhysicalMonitorsFromHMONITOR(logical, count, handles.data()))
        return {};
    return handles;
}
void closePhysical(PhysicalList& handles) {
    if (!handles.empty())
        DestroyPhysicalMonitors(static_cast<DWORD>(handles.size()), handles.data());
}
unsigned percentage(DWORD min, DWORD value, DWORD max) {
    return max > min ? static_cast<unsigned>(
        (100ULL * (value - min) + (max - min)/2) / (max - min)) : 0;
}
DWORD rawValue(unsigned percent, DWORD min, DWORD max) {
    return min + static_cast<DWORD>(
        (static_cast<unsigned long long>(std::min(100U, percent)) * (max - min) + 50) / 100);
}
bool equalPath(const std::wstring& a, const std::wstring& b) {
    return _wcsicmp(a.c_str(), b.c_str()) == 0;
}
std::wstring hardwareId(const std::wstring& instancePath) {
    // WMI uses DISPLAY\MODEL\INSTANCE; EnumDisplayDevices uses MONITOR\MODEL\INSTANCE.
    const size_t start = instancePath.find(L'\\');
    if (start == std::wstring::npos) return {};
    const size_t end = instancePath.find(L'\\', start + 1);
    if (end == std::wstring::npos || end == start + 1) return {};
    std::wstring id = instancePath.substr(start + 1, end - start - 1);
    for (auto& ch : id) ch = static_cast<wchar_t>(towupper(ch));
    return id;
}
std::vector<unsigned> supportedColorPresets(HANDLE monitor) {
    DWORD length = 0;
    if (!GetCapabilitiesStringLength(monitor, &length) || length < 4 || length > 16384)
        return {};
    std::string buffer(length, '\0');
    if (!CapabilitiesRequestAndCapabilitiesReply(monitor, &buffer[0], length))
        return {};
    const size_t terminator = buffer.find('\0');
    if (terminator != std::string::npos) buffer.resize(terminator);
    for (char& ch : buffer) ch = static_cast<char>(std::tolower(static_cast<unsigned char>(ch)));
    const size_t vcp = buffer.find("vcp(");
    if (vcp == std::string::npos) return {};
    size_t i = vcp + 4;
    int depth = 1;
    while (i < buffer.size() && depth > 0) {
        if (buffer[i] == ')') { --depth; ++i; continue; }
        if (buffer[i] == '(') { ++depth; ++i; continue; }
        if (std::isspace(static_cast<unsigned char>(buffer[i]))) { ++i; continue; }
        if (!std::isxdigit(static_cast<unsigned char>(buffer[i]))) { ++i; continue; }
        const size_t first = i;
        while (i < buffer.size() && std::isxdigit(static_cast<unsigned char>(buffer[i])))
            ++i;
        const unsigned code = static_cast<unsigned>(
            std::strtoul(buffer.substr(first, i - first).c_str(), nullptr, 16));
        if (depth != 1 || code != 0x14) continue;
        while (i < buffer.size() && std::isspace(static_cast<unsigned char>(buffer[i]))) ++i;
        if (i == buffer.size() || buffer[i] != '(') return {};
        ++i;
        std::vector<unsigned> presets;
        while (i < buffer.size() && buffer[i] != ')') {
            if (std::isspace(static_cast<unsigned char>(buffer[i]))) { ++i; continue; }
            if (!std::isxdigit(static_cast<unsigned char>(buffer[i]))) return {};
            const size_t firstValue = i;
            while (i < buffer.size() && std::isxdigit(static_cast<unsigned char>(buffer[i])))
                ++i;
            const auto value = static_cast<unsigned>(
                std::strtoul(buffer.substr(firstValue, i-firstValue).c_str(), nullptr, 16));
            if (value > 0xff) return {};
            if (std::find(presets.begin(), presets.end(), value) == presets.end())
                presets.push_back(value);
        }
        return presets;
    }
    return {};
}
void readRotation(Monitor& row) {
    if (row.deviceName.empty()) return;
    DEVMODEW mode{};
    mode.dmSize = sizeof(mode);
    if (EnumDisplaySettingsExW(row.deviceName.c_str(), ENUM_CURRENT_SETTINGS, &mode, 0)) {
        row.rotationAvailable = true;
        row.rotation = mode.dmDisplayOrientation;
    }
}
BOOL CALLBACK enumerateCallback(HMONITOR handle, HDC, LPRECT, LPARAM data) {
    auto& output = *reinterpret_cast<std::vector<Monitor>*>(data);
    MONITORINFOEXW info{};
    info.cbSize = sizeof(info);
    if (!GetMonitorInfoW(handle, &info)) return TRUE;

    DISPLAY_DEVICEW device{};
    device.cb = sizeof(device);
    const std::wstring model = EnumDisplayDevicesW(info.szDevice, 0, &device, 0)
        ? hardwareId(device.DeviceID) : std::wstring{};
    auto physical = openPhysical(handle);
    if (physical.empty()) {
        Monitor row;
        row.handle = handle;
        row.bounds = info.rcMonitor;
        row.name = info.szDevice;
        row.deviceName = info.szDevice;
        row.hardwareId = model;
        readRotation(row);
        output.push_back(std::move(row));
    }
    for (unsigned index = 0; index < physical.size(); ++index) {
        Monitor row;
        row.handle = handle;
        row.physicalIndex = index;
        row.bounds = info.rcMonitor;
        row.deviceName = info.szDevice;
        row.name = physical[index].szPhysicalMonitorDescription;
        if (row.name.empty()) row.name = info.szDevice;
        DWORD min = 0, current = 0, max = 0;
        const HANDLE physicalHandle = physical[index].hPhysicalMonitor;
        if (GetMonitorBrightness(physicalHandle, &min, &current, &max)) {
            row.brightnessAvailable = true;
            row.brightness = percentage(min, current, max);
        }
        if (GetMonitorContrast(physicalHandle, &min, &current, &max)) {
            row.contrastAvailable = true;
            row.contrast = percentage(min, current, max);
        }
        DWORD color = 0;
        MC_VCP_CODE_TYPE type = MC_MOMENTARY;
        if (GetVCPFeatureAndVCPFeatureReply(physicalHandle, 0x14, &type, &color, nullptr)) {
            row.colorPresets = supportedColorPresets(physicalHandle);
            row.colorAvailable = !row.colorPresets.empty();
            row.colorPreset = color;
        }
        readRotation(row);
        output.push_back(std::move(row));
    }
    closePhysical(physical);
    return TRUE;
}
class Wmi {
public:
    Wmi() {
        Com<IWbemLocator> locator;
        if (FAILED(CoCreateInstance(CLSID_WbemLocator, nullptr, CLSCTX_INPROC_SERVER,
                                    IID_IWbemLocator, reinterpret_cast<void**>(locator.out()))))
            return;
        Bstr space(L"ROOT\\WMI");
        if (FAILED(locator->ConnectServer(space, nullptr, nullptr, nullptr, 0,
                                          nullptr, nullptr, services_.out())))
            return;
        if (FAILED(CoSetProxyBlanket(services_.value, RPC_C_AUTHN_WINNT, RPC_C_AUTHZ_NONE,
                                    nullptr, RPC_C_AUTHN_LEVEL_CALL, RPC_C_IMP_LEVEL_IMPERSONATE,
                                    nullptr, EOAC_NONE))) {
            services_.value->Release();
            services_.value = nullptr;
        }
    }
    explicit operator bool() const { return !!services_; }
    void query(const wchar_t* statement, std::vector<std::pair<std::wstring, unsigned>>& result) {
        if (!services_) return;
        Com<IEnumWbemClassObject> rows;
        Bstr lang(L"WQL"), sql(statement);
        if (FAILED(services_->ExecQuery(lang, sql,
                   WBEM_FLAG_FORWARD_ONLY | WBEM_FLAG_RETURN_IMMEDIATELY,
                   nullptr, rows.out()))) return;
        for (unsigned i = 0; i < 64; ++i) {
            Com<IWbemClassObject> row;
            ULONG count = 0;
            if (FAILED(rows->Next(1000, 1, row.out(), &count)) || count == 0)
                break;
            Variant instance, brightness, active;
            if (FAILED(row->Get(L"InstanceName", 0, &instance.value, nullptr, nullptr)) ||
                instance.value.vt != VT_BSTR || !instance.value.bstrVal) continue;
            if (FAILED(row->Get(L"CurrentBrightness", 0, &brightness.value, nullptr, nullptr)))
                continue;
            if (SUCCEEDED(row->Get(L"Active", 0, &active.value, nullptr, nullptr)) &&
                active.value.vt == VT_BOOL && active.value.boolVal == VARIANT_FALSE) continue;
            unsigned value = 0;
            if (brightness.value.vt == VT_UI1) value = brightness.value.bVal;
            else if (brightness.value.vt == VT_I4) value = static_cast<unsigned>(brightness.value.lVal);
            else continue;
            result.emplace_back(instance.value.bstrVal, std::min(value, 100U));
        }
    }
    bool changeBrightness(const std::wstring& instanceName, unsigned percent) {
        if (!services_) return false;
        Com<IEnumWbemClassObject> rows;
        Bstr lang(L"WQL"), statement(L"SELECT * FROM WmiMonitorBrightnessMethods");
        if (FAILED(services_->ExecQuery(lang, statement,
                   WBEM_FLAG_FORWARD_ONLY | WBEM_FLAG_RETURN_IMMEDIATELY,
                   nullptr, rows.out()))) return false;
        for (unsigned i = 0; i < 64; ++i) {
            Com<IWbemClassObject> row;
            ULONG count = 0;
            if (FAILED(rows->Next(1000, 1, row.out(), &count)) || count == 0) break;
            Variant name;
            if (FAILED(row->Get(L"InstanceName", 0, &name.value, nullptr, nullptr)) ||
                name.value.vt != VT_BSTR || !name.value.bstrVal ||
                !equalPath(instanceName, name.value.bstrVal)) continue;

            Variant objectPath;
            if (FAILED(row->Get(L"__PATH", 0, &objectPath.value, nullptr, nullptr)) ||
                objectPath.value.vt != VT_BSTR || !objectPath.value.bstrVal) return false;
            Com<IWbemClassObject> methods, inputClass, parameters, response;
            if (FAILED(services_->GetObject(objectPath.value.bstrVal, 0, nullptr,
                                            methods.out(), nullptr))) return false;
            if (FAILED(methods->GetMethod(L"WmiSetBrightness", 0, inputClass.out(), nullptr)) ||
                !inputClass) return false;
            if (FAILED(inputClass->SpawnInstance(0, parameters.out()))) return false;
            Variant timeout, brightness;
            timeout.value.vt = VT_UI4;
            timeout.value.ulVal = 0;
            brightness.value.vt = VT_UI1;
            brightness.value.bVal = static_cast<BYTE>(std::min(100U, percent));
            if (FAILED(parameters->Put(L"Timeout", 0, &timeout.value, 0)) ||
                FAILED(parameters->Put(L"Brightness", 0, &brightness.value, 0))) return false;
            Bstr method(L"WmiSetBrightness");
            if (FAILED(services_->ExecMethod(objectPath.value.bstrVal, method, 0,
                                             nullptr, parameters.value,
                                             response.out(), nullptr))) return false;
            if (response) {
                Variant value;
                if (SUCCEEDED(response->Get(L"ReturnValue", 0, &value.value, nullptr, nullptr)) &&
                    value.value.vt == VT_I4 && value.value.lVal != 0) return false;
            }
            return true;
        }
        return false;
    }
private:
    Com<IWbemServices> services_;
};
bool ddcWrite(const Monitor& row, unsigned percent, bool contrast) {
    auto handles = openPhysical(row.handle);
    if (row.physicalIndex >= handles.size()) {
        closePhysical(handles);
        return false;
    }
    HANDLE h = handles[row.physicalIndex].hPhysicalMonitor;
    DWORD min = 0, current = 0, max = 0;
    const bool queried = contrast
        ? GetMonitorContrast(h, &min, &current, &max) != FALSE
        : GetMonitorBrightness(h, &min, &current, &max) != FALSE;
    bool success = false;
    if (queried) {
        DWORD target = rawValue(percent, min, max);
        success = contrast ? SetMonitorContrast(h, target) != FALSE
                           : SetMonitorBrightness(h, target) != FALSE;
        if (success) {
            DWORD checkMin = 0, check = 0, checkMax = 0;
            success = contrast ? GetMonitorContrast(h, &checkMin, &check, &checkMax) != FALSE
                               : GetMonitorBrightness(h, &checkMin, &check, &checkMax) != FALSE;
            if (success) {
                const auto result = percentage(checkMin, check, checkMax);
                success = result + 2 >= percent && percent + 2 >= result;
            }
        }
    }
    closePhysical(handles);
    return success;
}
}

std::vector<Monitor> enumerateMonitors() {
    std::vector<Monitor> result;
    EnumDisplayMonitors(nullptr, nullptr, enumerateCallback,
                        reinterpret_cast<LPARAM>(&result));
    Wmi wmi;
    if (wmi) {
        std::vector<std::pair<std::wstring, unsigned>> internal;
        wmi.query(L"SELECT Active, InstanceName, CurrentBrightness FROM WmiMonitorBrightness", internal);
        for (const auto& entry : internal) {
            // Never assign laptop WMI brightness to an arbitrary external monitor.
            const std::wstring model = hardwareId(entry.first);
            auto target = result.end();
            if (!model.empty()) {
                for (auto iter = result.begin(); iter != result.end(); ++iter) {
                    if (iter->hardwareId == model && !iter->brightnessAvailable &&
                        iter->provider != ControlProvider::Wmi) {
                        if (target != result.end()) { target = result.end(); break; }
                        target = iter;
                    }
                }
            }
            if (target == result.end()) {
                Monitor row;
                row.name = L"Internal display (WMI)";
                row.hardwareId = model;
                row.provider = ControlProvider::Wmi;
                row.brightnessAvailable = true;
                row.brightness = entry.second;
                row.wmiInstance = entry.first;
                result.push_back(std::move(row));
            } else {
                target->provider = ControlProvider::Wmi;
                target->brightnessAvailable = true;
                target->brightness = entry.second;
                target->wmiInstance = entry.first;
            }
        }
    }
    return result;
}

bool setBrightness(const Monitor& row, unsigned percentageValue) {
    if (!row.brightnessAvailable) return false;
    if (row.provider == ControlProvider::Wmi) {
        Wmi wmi;
        if (!wmi || !wmi.changeBrightness(row.wmiInstance, percentageValue))
            return false;
        std::vector<std::pair<std::wstring, unsigned>> readback;
        wmi.query(L"SELECT Active, InstanceName, CurrentBrightness FROM WmiMonitorBrightness", readback);
        for (const auto& candidate : readback) {
            if (equalPath(candidate.first, row.wmiInstance)) {
                return candidate.second + 2 >= percentageValue &&
                       percentageValue + 2 >= candidate.second;
            }
        }
        return false;
    }
    return ddcWrite(row, percentageValue, false);
}

bool setContrast(const Monitor& row, unsigned percentageValue) {
    return row.provider == ControlProvider::Ddc && row.contrastAvailable &&
           ddcWrite(row, percentageValue, true);
}

bool setColorPreset(const Monitor& row, unsigned value) {
    if (row.provider != ControlProvider::Ddc || !row.colorAvailable) return false;
    auto handles = openPhysical(row.handle);
    if (row.physicalIndex >= handles.size()) {
        closePhysical(handles);
        return false;
    }
    HANDLE h = handles[row.physicalIndex].hPhysicalMonitor;
    bool success = SetVCPFeature(h, 0x14, value) != FALSE;
    MC_VCP_CODE_TYPE codeType = MC_MOMENTARY;
    DWORD current = 0;
    if (success) success = GetVCPFeatureAndVCPFeatureReply(h, 0x14,
                                                &codeType, &current, nullptr) != FALSE &&
                            current == value;
    closePhysical(handles);
    return success;
}

bool setRotation(const Monitor& row, unsigned orientation) {
    if (!row.rotationAvailable || row.deviceName.empty() || orientation > 3) return false;
    DEVMODEW mode{};
    mode.dmSize = sizeof(mode);
    if (!EnumDisplaySettingsExW(row.deviceName.c_str(), ENUM_CURRENT_SETTINGS, &mode, 0))
        return false;
    const unsigned original = mode.dmDisplayOrientation;
    if ((original % 2) != (orientation % 2))
        std::swap(mode.dmPelsWidth, mode.dmPelsHeight);
    mode.dmDisplayOrientation = orientation;
    mode.dmFields |= DM_DISPLAYORIENTATION | DM_PELSWIDTH | DM_PELSHEIGHT;
    if (ChangeDisplaySettingsExW(row.deviceName.c_str(), &mode, nullptr, CDS_TEST, nullptr)
        != DISP_CHANGE_SUCCESSFUL) return false;
    if (ChangeDisplaySettingsExW(row.deviceName.c_str(), &mode, nullptr,
                                 CDS_UPDATEREGISTRY, nullptr) != DISP_CHANGE_SUCCESSFUL)
        return false;
    DEVMODEW check{};
    check.dmSize = sizeof(check);
    return EnumDisplaySettingsExW(row.deviceName.c_str(), ENUM_CURRENT_SETTINGS, &check, 0) &&
           check.dmDisplayOrientation == orientation;
}

std::wstring rotationLabel(unsigned orientation) {
    switch (orientation) {
        case DMDO_DEFAULT: return L"Landscape";
        case DMDO_90: return L"Portrait";
        case DMDO_180: return L"Landscape flipped";
        case DMDO_270: return L"Portrait flipped";
        default: return L"Unknown";
    }
}
std::wstring colorPresetLabel(unsigned value) {
    switch (value) {
        case 0x01: return L"sRGB";
        case 0x02: return L"Native";
        case 0x04: return L"5000 K";
        case 0x05: return L"6500 K";
        case 0x08: return L"9300 K";
        default: return std::to_wstring(value);
    }
}
}
