#include "settings.hpp"
#include "json_settings.hpp"
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

namespace {
using JsonValue = json_settings::Value;

void setScheduleFields(JsonValue& doc, const Settings& s) {
    doc.object["version"] = json_settings::number(4);
    doc.object["lightMinutes"] = json_settings::number(static_cast<int>(s.lightMinute));
    doc.object["darkMinutes"] = json_settings::number(static_cast<int>(s.darkMinute));
    doc.object["automationEnabled"] = json_settings::boolean(s.scheduleEnabled);
    doc.object["reduceBrightness"] = json_settings::boolean(s.reduceBrightness);
    doc.object["automaticLocation"] = json_settings::boolean(s.automaticLocation);
    doc.object["scheduleMode"] = json_settings::number(
        s.scheduleMode == ScheduleMode::Solar ? 1 : 0);
    doc.object["latitude"] = s.locationConfigured ?
        json_settings::number(s.latitude) : JsonValue{};
    doc.object["longitude"] = s.locationConfigured ?
        json_settings::number(s.longitude) : JsonValue{};
    doc.object["locationLabel"] = s.locationConfigured ?
        json_settings::string(json_settings::toUtf8(s.locationLabel)) : JsonValue{};
    if (!json_settings::field(doc, "brightnessRestoreValues"))
        doc.object["brightnessRestoreValues"] = json_settings::object();
    if (!json_settings::field(doc, "brightnessReductionActive"))
        doc.object["brightnessReductionActive"] = json_settings::boolean(false);
}

Settings loadRegistrySettings() {
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
    s.automaticLocation = false;
    return s;
}

std::string storedRestoreKey(const JsonValue& values, const std::wstring& name) {
    if (values.type != JsonValue::Type::Object) return {};
    for (const auto& pair : values.object) {
        if (_wcsicmp(json_settings::toWide(pair.first).c_str(), name.c_str()) == 0)
            return pair.first;
    }
    return {};
}

bool validRestoreValues(const JsonValue& doc) {
    const auto* values = json_settings::field(doc, "brightnessRestoreValues");
    if (!values || values->type == JsonValue::Type::Null) return true;
    if (values->type != JsonValue::Type::Object) return false;
    for (const auto& pair : values->object) {
        int percent = -1;
        if (pair.first.empty() || !json_settings::asInteger(&pair.second, percent) ||
            percent < 0 || percent > 100) return false;
    }
    return true;
}
}

Settings loadSettings() {
    Settings s;
    JsonValue doc;
    const auto result = json_settings::read(doc);
    if (result == json_settings::ReadResult::Missing) return loadRegistrySettings();
    if (result != json_settings::ReadResult::Valid ||
        !json_settings::supported(doc) || !validRestoreValues(doc)) return s;

    int version = 0, light = 420, dark = 1140, mode = 0;
    json_settings::asInteger(json_settings::field(doc, "version"), version);
    if (!json_settings::asInteger(json_settings::field(doc, "lightMinutes"), light) ||
        !json_settings::asInteger(json_settings::field(doc, "darkMinutes"), dark) ||
        light < 0 || light >= 1440 || dark < 0 || dark >= 1440 || light == dark)
        return s;
    s.lightMinute = static_cast<unsigned>(light);
    s.darkMinute = static_cast<unsigned>(dark);
    s.scheduleEnabled = version >= 2 &&
        json_settings::asBoolean(json_settings::field(doc, "automationEnabled"), false);
    s.reduceBrightness = version >= 3 &&
        json_settings::asBoolean(json_settings::field(doc, "reduceBrightness"), false);
    if (version >= 4) {
        if (!json_settings::asInteger(json_settings::field(doc, "scheduleMode"), mode) ||
            (mode != 0 && mode != 1)) return Settings{};
    }
    s.scheduleMode = mode == 1 ? ScheduleMode::Solar : ScheduleMode::Fixed;
    s.automaticLocation = json_settings::asBoolean(json_settings::field(doc, "automaticLocation"), false);
    const auto* savedLatitude = json_settings::field(doc, "latitude");
    const auto* savedLongitude = json_settings::field(doc, "longitude");
    if (savedLatitude && savedLongitude &&
        savedLatitude->type != JsonValue::Type::Null &&
        savedLongitude->type != JsonValue::Type::Null) {
        if (json_settings::asNumber(savedLatitude, s.latitude) &&
            json_settings::asNumber(savedLongitude, s.longitude) &&
            std::isfinite(s.latitude) && std::isfinite(s.longitude) &&
            std::abs(s.latitude) <= 90 && std::abs(s.longitude) <= 180)
            s.locationConfigured = true;
    }
    const auto* label = json_settings::field(doc, "locationLabel");
    if (label && label->type == JsonValue::Type::String)
        s.locationLabel = json_settings::toWide(label->scalar);
    return s;
}

void saveSettings(const Settings& s) {
    JsonValue doc;
    if (!json_settings::editable(doc) || !validRestoreValues(doc)) return;
    setScheduleFields(doc, s);
    json_settings::write(doc);
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
    JsonValue doc;
    const auto result = json_settings::read(doc);
    if (result == json_settings::ReadResult::Valid) {
        if (!json_settings::supported(doc) || !validRestoreValues(doc)) return false;
        const auto* values = json_settings::field(doc, "brightnessRestoreValues");
        if (!values || values->type != JsonValue::Type::Object) return false;
        const auto it = values->object.find(storedRestoreKey(*values, keyName));
        if (it == values->object.end()) return false;
        int value = -1;
        if (!json_settings::asInteger(&it->second, value) || value < 0 || value > 100)
            return false;
        percent = static_cast<unsigned>(value);
        return true;
    }
    if (result != json_settings::ReadResult::Missing) return false;
    Key key(restorationPath, KEY_READ, false);
    if (!key) return false;
    const DWORD value = key.getDword(keyName.c_str(), 0xFFFFFFFFu);
    if (value > 100) return false;
    percent = value;
    return true;
}
void saveBrightness(const std::wstring& keyName, unsigned percent) {
    JsonValue doc;
    const auto result = json_settings::read(doc);
    if (result == json_settings::ReadResult::Missing) {
        doc = json_settings::newDocument();
        setScheduleFields(doc, loadRegistrySettings());
    } else if (result != json_settings::ReadResult::Valid ||
               !json_settings::supported(doc) || !validRestoreValues(doc)) return;
    auto& values = doc.object["brightnessRestoreValues"];
    if (values.type == JsonValue::Type::Null) values = json_settings::object();
    if (values.type != JsonValue::Type::Object) return;
    const auto existing = storedRestoreKey(values, keyName);
    values.object[existing.empty() ? json_settings::toUtf8(keyName) : existing] =
        json_settings::number(static_cast<int>(std::min(percent, 100U)));
    doc.object["brightnessReductionActive"] = json_settings::boolean(true);
    json_settings::write(doc);
}
void removeSavedBrightness(const std::wstring& keyName) {
    JsonValue doc;
    const auto result = json_settings::read(doc);
    if (result == json_settings::ReadResult::Missing) {
        Key key(restorationPath, KEY_WRITE, false);
        if (key) RegDeleteValueW(key.get(), keyName.c_str());
        return;
    }
    if (result != json_settings::ReadResult::Valid ||
        !json_settings::supported(doc) || !validRestoreValues(doc)) return;
    auto* values = &doc.object["brightnessRestoreValues"];
    if (values->type != JsonValue::Type::Object) return;
    if (!values->object.erase(storedRestoreKey(*values, keyName))) return;
    doc.object["brightnessReductionActive"] =
        json_settings::boolean(!values->object.empty());
    json_settings::write(doc);
}
}
