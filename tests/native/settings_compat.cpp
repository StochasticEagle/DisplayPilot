#include "settings.hpp"
#include "json_settings.hpp"
#include <windows.h>
#include <cmath>
#include <cstring>
#include <string>

using namespace displaypilot;

static bool fixture(const char* json) {
    const std::wstring file = json_settings::path();
    const size_t slash = file.find_last_of(L'\\');
    if (slash == std::wstring::npos) return false;
    CreateDirectoryW(file.substr(0, slash).c_str(), nullptr);
    HANDLE h = CreateFileW(file.c_str(), GENERIC_WRITE, 0, nullptr,
        CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (h == INVALID_HANDLE_VALUE) return false;
    DWORD written = 0;
    const DWORD length = static_cast<DWORD>(strlen(json));
    const bool ok = WriteFile(h, json, length, &written, nullptr) != FALSE &&
        written == length;
    CloseHandle(h);
    return ok;
}
static bool equal(double a, double b) {
    return std::abs(a - b) < 0.00000001;
}
int main() {
    const char* original = R"({
        "version":4,
        "lightMinutes":485,
        "darkMinutes":1265,
        "automationEnabled":true,
        "reduceBrightness":true,
        "brightnessReductionActive":true,
        "brightnessRestoreValues":{"DISPLAY\\ONE":73},
        "scheduleMode":1,
        "latitude":43.615,
        "longitude":-116.2023,
        "locationLabel":"M\u00fcnchen",
        "futureField":{"nested":[1,"retained"]}
    })";
    if (!fixture(original)) return 1;
    Settings s = loadSettings();
    if (!s.scheduleEnabled || !s.reduceBrightness ||
        s.scheduleMode != ScheduleMode::Solar || !s.locationConfigured ||
        s.lightMinute != 485 || s.darkMinute != 1265 ||
        !equal(s.latitude, 43.615) || !equal(s.longitude, -116.2023) ||
        s.locationLabel != L"M\u00fcnchen") return 2;

    unsigned brightness = 0;
    if (!restoreBrightness(L"DISPLAY\\ONE", brightness) || brightness != 73) return 3;
    if (!restoreBrightness(L"display\\one", brightness) || brightness != 73) return 20;
    saveBrightness(L"display\\one", 73);
    json_settings::Value caseDoc;
    if (json_settings::read(caseDoc) != json_settings::ReadResult::Valid ||
        json_settings::field(caseDoc, "brightnessRestoreValues")->object.size() != 1)
        return 21;
    s.lightMinute = 495;
    saveSettings(s);
    json_settings::Value doc;
    if (json_settings::read(doc) != json_settings::ReadResult::Valid) return 4;
    int minute = 0;
    if (!json_settings::asInteger(json_settings::field(doc, "lightMinutes"), minute) ||
        minute != 495 || !json_settings::field(doc, "futureField")) return 5;
    if (!restoreBrightness(L"DISPLAY\\ONE", brightness) || brightness != 73) return 6;
    saveBrightness(L"Monitor-123", 46);
    if (!restoreBrightness(L"Monitor-123", brightness) || brightness != 46) return 7;
    removeSavedBrightness(L"Monitor-123");
    if (restoreBrightness(L"Monitor-123", brightness) ||
        !restoreBrightness(L"DISPLAY\\ONE", brightness) || brightness != 73) return 8;
    if (json_settings::read(doc) != json_settings::ReadResult::Valid ||
        !json_settings::asBoolean(json_settings::field(doc, "brightnessReductionActive"), false))
        return 9;

    if (!fixture(R"({"version":1,"lightMinutes":420,"darkMinutes":1140})"))
        return 10;
    s = loadSettings();
    if (s.scheduleEnabled || s.reduceBrightness || s.scheduleMode != ScheduleMode::Fixed)
        return 11;
    if (!fixture(R"({"version":2,"lightMinutes":480,"darkMinutes":1200,"automationEnabled":true})"))
        return 12;
    s = loadSettings();
    if (!s.scheduleEnabled || s.reduceBrightness || s.lightMinute != 480) return 13;
    if (!fixture(R"({"version":3,"lightMinutes":480,"darkMinutes":1200,"automationEnabled":true,"reduceBrightness":true,"brightnessRestoreValues":{"old":100}})"))
        return 14;
    s = loadSettings();
    if (!s.scheduleEnabled || !s.reduceBrightness ||
        !restoreBrightness(L"old", brightness) || brightness != 100) return 15;

    if (!fixture("{not valid JSON")) return 16;
    s = loadSettings();
    if (s.scheduleEnabled || s.lightMinute != 420) return 17;
    saveSettings(s);
    HANDLE h = CreateFileW(json_settings::path().c_str(), GENERIC_READ, FILE_SHARE_READ,
        nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (h == INVALID_HANDLE_VALUE) return 18;
    char buffer[32]{};
    DWORD read = 0;
    const bool untouched = ReadFile(h, buffer, sizeof(buffer), &read, nullptr) != FALSE &&
        std::string(buffer, read) == "{not valid JSON";
    CloseHandle(h);
    if (!untouched) return 19;
    DeleteFileW(json_settings::path().c_str());
    return 0;
}
