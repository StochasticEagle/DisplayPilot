#include "settings.hpp"
#include <algorithm>
#include <cmath>
#include <cstdint>
#include <string>

namespace displaypilot {
namespace {
constexpr wchar_t settingsPath[] = L"Software\\DisplayPilot\\Settings";
constexpr wchar_t restorationPath[] = L"Software\\DisplayPilot\\Restoration";
constexpr double pi = 3.14159265358979323846;
constexpr double deg2rad = pi / 180.0;

class Key {
public:
    Key(const wchar_t* path, REGSAM access, bool create) {
        if (create) RegCreateKeyExW(HKEY_CURRENT_USER, path, 0, nullptr, 0,
                                    access, nullptr, &handle_, nullptr);
        else RegOpenKeyExW(HKEY_CURRENT_USER, path, 0, access, &handle_);
    }
    ~Key() { if (handle_) RegCloseKey(handle_); }
    explicit operator bool() const { return handle_ != nullptr; }
    HKEY get() const { return handle_; }
    DWORD getDword(const wchar_t* name, DWORD fallback) const {
        DWORD size = sizeof(DWORD), type = 0, value = 0;
        if (!handle_ || RegQueryValueExW(handle_, name, nullptr, &type,
                 reinterpret_cast<BYTE*>(&value), &size) != ERROR_SUCCESS ||
            type != REG_DWORD || size != sizeof(value)) return fallback;
        return value;
    }
    void putDword(const wchar_t* name, DWORD value) const {
        if (handle_) RegSetValueExW(handle_, name, 0, REG_DWORD,
                    reinterpret_cast<const BYTE*>(&value), sizeof(value));
    }
    std::wstring getString(const wchar_t* name) const {
        DWORD size = 0, type = 0;
        if (!handle_ || RegQueryValueExW(handle_, name, nullptr, &type, nullptr, &size) ||
            type != REG_SZ || size < sizeof(wchar_t) || size > 4096) return {};
        std::wstring value(size / sizeof(wchar_t), L'\0');
        if (RegQueryValueExW(handle_, name, nullptr, &type,
              reinterpret_cast<BYTE*>(&value[0]), &size)) return {};
        value.resize(wcsnlen_s(value.c_str(), value.size()));
        return value;
    }
    void putString(const wchar_t* name, const std::wstring& value) const {
        if (handle_) RegSetValueExW(handle_, name, 0, REG_SZ,
          reinterpret_cast<const BYTE*>(value.c_str()),
          static_cast<DWORD>((value.size() + 1) * sizeof(wchar_t)));
    }
private:
    HKEY handle_ = nullptr;
};

bool solarMinutes(const SYSTEMTIME& date, const Settings& settings,
                  unsigned& sunrise, unsigned& sunset) {
    const int year = date.wYear, month = date.wMonth, day = date.wDay;
    static constexpr int daysBeforeMonth[12] =
        {0,31,59,90,120,151,181,212,243,273,304,334};
    if (month < 1 || month > 12 || day < 1 || day > 31) return false;
    const bool leap = (year % 4 == 0 && year % 100 != 0) || year % 400 == 0;
    const int yearDay = daysBeforeMonth[month-1] + day + (leap && month > 2 ? 1 : 0);
    const double gamma = (2.0 * pi / (leap ? 366.0 : 365.0)) * (yearDay - 1);
    const double equation = 229.18 * (0.000075 + 0.001868 * cos(gamma) -
         0.032077 * sin(gamma) - 0.014615 * cos(2*gamma) - 0.040849 * sin(2*gamma));
    const double declination = 0.006918 - 0.399912*cos(gamma) + 0.070257*sin(gamma)
        - 0.006758*cos(2*gamma) + 0.000907*sin(2*gamma)
        - 0.002697*cos(3*gamma) + 0.00148*sin(3*gamma);
    const double latitude = settings.latitude * deg2rad;
    const double denominator = cos(latitude) * cos(declination);
    if (std::abs(denominator) < 0.000001) return false;
    const double h = (cos(90.833 * deg2rad) - sin(latitude) * sin(declination)) /
                     denominator;
    if (h < -1.0 || h > 1.0) return false; // polar day/night
    const double hourAngle = acos(h) / deg2rad;
    // UTC event times. Convert the date's local noon UTC offset to the local
    // sunrise and sunset, preserving daylight-saving rules from Windows.
    SYSTEMTIME localNoon = date;
    localNoon.wHour = 12;
    localNoon.wMinute = 0;
    localNoon.wSecond = 0;
    localNoon.wMilliseconds = 0;
    SYSTEMTIME utcNoon{};
    if (!TzSpecificLocalTimeToSystemTime(nullptr, &localNoon, &utcNoon)) return false;
    FILETIME localFile{}, utcFile{};
    if (!SystemTimeToFileTime(&localNoon, &localFile) ||
        !SystemTimeToFileTime(&utcNoon, &utcFile)) return false;
    ULARGE_INTEGER a{}, b{};
    a.LowPart = localFile.dwLowDateTime; a.HighPart = localFile.dwHighDateTime;
    b.LowPart = utcFile.dwLowDateTime; b.HighPart = utcFile.dwHighDateTime;
    const double offset = static_cast<double>(
         static_cast<std::int64_t>(a.QuadPart) - static_cast<std::int64_t>(b.QuadPart)) /
         600000000.0;
    const double center = 720.0 - 4.0 * settings.longitude - equation + offset;
    auto normalizedMinute = [](double minute) {
        const long rounded = std::lround(minute);
        return static_cast<unsigned>((rounded % 1440 + 1440) % 1440);
    };
    sunrise = normalizedMinute(center - 4 * hourAngle);
    sunset = normalizedMinute(center + 4 * hourAngle);
    return true;
}

DWORD secondsUntil(unsigned eventMinute, unsigned minuteNow, unsigned secondsNow) {
    const unsigned minutes = (eventMinute + 1440 - minuteNow) % 1440;
    unsigned seconds = minutes * 60;
    if (minutes == 0) seconds = 24 * 3600;
    if (seconds > secondsNow) seconds -= secondsNow;
    return seconds;
}
}

Settings loadSettings() {
    Settings s;
    Key key(settingsPath, KEY_READ, false);
    if (!key) return s;
    s.scheduleEnabled = key.getDword(L"Enabled", 0) != 0;
    s.reduceBrightness = key.getDword(L"ReduceBrightness", 0) != 0;
    s.scheduleMode = key.getDword(L"Mode", 0) == 1 ?
                      ScheduleMode::Solar : ScheduleMode::Fixed;
    s.lightMinute = std::min(1439UL, key.getDword(L"LightMinute", 420));
    s.darkMinute = std::min(1439UL, key.getDword(L"DarkMinute", 1140));
    const auto lat = static_cast<std::int32_t>(key.getDword(L"LatitudeMicro", 0));
    const auto lng = static_cast<std::int32_t>(key.getDword(L"LongitudeMicro", 0));
    s.latitude = lat / 1000000.0;
    s.longitude = lng / 1000000.0;
    s.locationConfigured = key.getDword(L"LocationConfigured", 0) != 0 &&
        std::abs(s.latitude) <= 90 && std::abs(s.longitude) <= 180;
    s.locationLabel = key.getString(L"LocationLabel");
    return s;
}

void saveSettings(const Settings& s) {
    Key key(settingsPath, KEY_WRITE, true);
    if (!key) return;
    key.putDword(L"Enabled", s.scheduleEnabled ? 1 : 0);
    key.putDword(L"ReduceBrightness", s.reduceBrightness ? 1 : 0);
    key.putDword(L"Mode", s.scheduleMode == ScheduleMode::Solar ? 1 : 0);
    key.putDword(L"LightMinute", s.lightMinute);
    key.putDword(L"DarkMinute", s.darkMinute);
    key.putDword(L"LatitudeMicro", static_cast<DWORD>(
             static_cast<std::int32_t>(std::lround(s.latitude * 1e6))));
    key.putDword(L"LongitudeMicro", static_cast<DWORD>(
             static_cast<std::int32_t>(std::lround(s.longitude * 1e6))));
    key.putDword(L"LocationConfigured", s.locationConfigured ? 1 : 0);
    key.putString(L"LocationLabel", s.locationLabel);
}

bool supportsWindowsTheme() {
    typedef LONG (WINAPI* VersionFn)(OSVERSIONINFOW*);
    HMODULE dll = GetModuleHandleW(L"ntdll.dll");
    auto version = dll ? reinterpret_cast<VersionFn>(
        GetProcAddress(dll, "RtlGetVersion")) : nullptr;
    if (!version) return false;
    OSVERSIONINFOW v{};
    v.dwOSVersionInfoSize = sizeof(v);
    return version(&v) >= 0 && v.dwMajorVersion >= 10;
}

bool setWindowsTheme(bool dark) {
    if (!supportsWindowsTheme()) return false;
    Key key(L"Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize", KEY_WRITE, true);
    if (!key) return false;
    key.putDword(L"AppsUseLightTheme", dark ? 0 : 1);
    key.putDword(L"SystemUsesLightTheme", dark ? 0 : 1);
    DWORD_PTR unused = 0;
    SendMessageTimeoutW(HWND_BROADCAST, WM_SETTINGCHANGE, 0,
        reinterpret_cast<LPARAM>(L"ImmersiveColorSet"),
        SMTO_ABORTIFHUNG, 800, &unused);
    return true;
}

bool readWindowsTheme(bool& dark) {
    Key key(L"Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize", KEY_READ, false);
    if (!key || !supportsWindowsTheme()) return false;
    dark = key.getDword(L"AppsUseLightTheme", 1) == 0;
    return true;
}

ScheduleState evaluateSchedule(const Settings& s, const SYSTEMTIME& now) {
    ScheduleState state;
    if (!s.scheduleEnabled) return state;
    unsigned light = s.lightMinute, dark = s.darkMinute;
    if (s.scheduleMode == ScheduleMode::Solar) {
        if (!s.locationConfigured || !solarMinutes(now, s, light, dark))
            return state;
    }
    if (light == dark) return state;
    const unsigned minuteNow = now.wHour * 60U + now.wMinute;
    const bool daylight = light < dark
        ? minuteNow >= light && minuteNow < dark
        : minuteNow >= light || minuteNow < dark;
    state.dark = !daylight;
    state.valid = true;
    state.lightMinute = light;
    state.darkMinute = dark;
    const DWORD closest = std::min(
        secondsUntil(light, minuteNow, now.wSecond),
        secondsUntil(dark, minuteNow, now.wSecond));
    state.millisecondsUntilChange =
        std::max(1000UL, std::min(closest * 1000UL, 24UL * 60UL * 60UL * 1000UL));
    return state;
}

bool restoreBrightness(const std::wstring& keyName, unsigned& percent) {
    Key key(restorationPath, KEY_READ, false);
    if (!key) return false;
    const DWORD value = key.getDword(keyName.c_str(), 0xFFFFFFFFu);
    if (value > 100) return false;
    percent = value;
    return true;
}
void saveBrightness(const std::wstring& keyName, unsigned percent) {
    Key key(restorationPath, KEY_WRITE, true);
    key.putDword(keyName.c_str(), std::min(percent, 100U));
}
void removeSavedBrightness(const std::wstring& keyName) {
    Key key(restorationPath, KEY_WRITE, false);
    if (key) RegDeleteValueW(key.get(), keyName.c_str());
}
}
