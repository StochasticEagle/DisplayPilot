#pragma once
#include <windows.h>
#include <string>
#include <vector>

namespace displaypilot {
enum class ScheduleMode : unsigned { Fixed = 0, Solar = 1 };

struct Settings {
    bool scheduleEnabled = false;
    bool reduceBrightness = false;
    ScheduleMode scheduleMode = ScheduleMode::Fixed;
    unsigned lightMinute = 7 * 60;
    unsigned darkMinute = 19 * 60;
    double latitude = 0.0;
    double longitude = 0.0;
    bool locationConfigured = false;
    bool automaticLocation = false;
    std::wstring locationLabel;
};

struct ScheduleState {
    bool valid = false;
    bool dark = false;
    DWORD millisecondsUntilChange = 60000;
    unsigned lightMinute = 0;
    unsigned darkMinute = 0;
};

Settings loadSettings();
void saveSettings(const Settings& settings);
bool supportsWindowsTheme();
bool setWindowsTheme(bool dark);
bool readWindowsTheme(bool& dark);
ScheduleState evaluateSchedule(const Settings& settings, const SYSTEMTIME& now);
bool restoreBrightness(const std::wstring& key, unsigned& percent);
void saveBrightness(const std::wstring& key, unsigned percent);
void removeSavedBrightness(const std::wstring& key);
}
