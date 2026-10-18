#pragma once
#include <windows.h>
#include <shlobj.h>
#include <algorithm>
#include <cmath>
#include <cstdlib>
#include <iomanip>
#include <limits>
#include <locale>
#include <map>
#include <sstream>
#include <string>
#include <vector>

namespace displaypilot {
namespace json_settings {

struct Value {
    enum class Type { Null, Boolean, Number, String, Object, Array };
    Type type = Type::Null;
    std::string scalar;
    std::map<std::string, Value> object;
    std::vector<Value> array;
};

inline Value object() {
    Value v;
    v.type = Value::Type::Object;
    return v;
}
inline Value boolean(bool b) {
    Value v;
    v.type = Value::Type::Boolean;
    v.scalar = b ? "true" : "false";
    return v;
}
inline Value number(int n) {
    Value v;
    v.type = Value::Type::Number;
    v.scalar = std::to_string(n);
    return v;
}
inline Value number(double n) {
    Value v;
    v.type = Value::Type::Number;
    std::ostringstream stream;
    stream.imbue(std::locale::classic());
    stream << std::setprecision(17) << n;
    v.scalar = stream.str();
    return v;
}
inline Value string(const std::string& s) {
    Value v;
    v.type = Value::Type::String;
    v.scalar = s;
    return v;
}
inline const Value* field(const Value& objectValue, const char* key) {
    if (objectValue.type != Value::Type::Object) return nullptr;
    const auto it = objectValue.object.find(key);
    return it == objectValue.object.end() ? nullptr : &it->second;
}
inline bool asBoolean(const Value* v, bool fallback) {
    return v && v->type == Value::Type::Boolean ? v->scalar == "true" : fallback;
}
inline bool asNumber(const Value* v, double& n) {
    if (!v || v->type != Value::Type::Number) return false;
    std::istringstream stream(v->scalar);
    stream.imbue(std::locale::classic());
    stream >> n;
    return !stream.fail() && stream.eof() && std::isfinite(n);
}
inline bool asInteger(const Value* v, int& n) {
    double d = 0;
    if (!asNumber(v, d) || std::trunc(d) != d ||
        d < static_cast<double>(std::numeric_limits<int>::min()) ||
        d > static_cast<double>(std::numeric_limits<int>::max())) return false;
    n = static_cast<int>(d);
    return true;
}
inline std::wstring toWide(const std::string& utf8) {
    if (utf8.empty()) return {};
    const int length = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS,
        utf8.data(), static_cast<int>(utf8.size()), nullptr, 0);
    if (!length) return {};
    std::wstring result(static_cast<size_t>(length), L'\0');
    if (!MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, utf8.data(),
        static_cast<int>(utf8.size()), &result[0], length)) return {};
    return result;
}
inline std::string toUtf8(const std::wstring& wide) {
    if (wide.empty()) return {};
    const int length = WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS,
        wide.data(), static_cast<int>(wide.size()), nullptr, 0, nullptr, nullptr);
    if (!length) return {};
    std::string result(static_cast<size_t>(length), '\0');
    if (!WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, wide.data(),
        static_cast<int>(wide.size()), &result[0], length, nullptr, nullptr)) return {};
    return result;
}

class Parser {
public:
    explicit Parser(const std::string& source) : source_(source) {}
    bool parse(Value& result) {
        skip();
        return value(result, 0) && (skip(), position_ == source_.size());
    }
private:
    const std::string& source_;
    size_t position_ = 0;
    void skip() {
        while (position_ < source_.size() &&
            (source_[position_] == ' ' || source_[position_] == '\n' ||
             source_[position_] == '\r' || source_[position_] == '\t')) ++position_;
    }
    bool take(char ch) {
        skip();
        if (position_ == source_.size() || source_[position_] != ch) return false;
        ++position_;
        return true;
    }
    bool literal(const char* word) {
        const size_t length = strlen(word);
        if (source_.compare(position_, length, word) != 0) return false;
        position_ += length;
        return true;
    }
    bool hex(unsigned& code) {
        if (source_.size() - position_ < 4) return false;
        code = 0;
        for (int i = 0; i < 4; ++i) {
            const char ch = source_[position_++];
            unsigned digit = 0;
            if (ch >= '0' && ch <= '9') digit = static_cast<unsigned>(ch - '0');
            else if (ch >= 'a' && ch <= 'f') digit = static_cast<unsigned>(ch - 'a' + 10);
            else if (ch >= 'A' && ch <= 'F') digit = static_cast<unsigned>(ch - 'A' + 10);
            else return false;
            code = code * 16 + digit;
        }
        return true;
    }
    static void appendCodepoint(std::string& output, unsigned code) {
        if (code <= 0x7F) output += static_cast<char>(code);
        else if (code <= 0x7FF) {
            output += static_cast<char>(0xC0 | (code >> 6));
            output += static_cast<char>(0x80 | (code & 0x3F));
        } else if (code <= 0xFFFF) {
            output += static_cast<char>(0xE0 | (code >> 12));
            output += static_cast<char>(0x80 | ((code >> 6) & 0x3F));
            output += static_cast<char>(0x80 | (code & 0x3F));
        } else {
            output += static_cast<char>(0xF0 | (code >> 18));
            output += static_cast<char>(0x80 | ((code >> 12) & 0x3F));
            output += static_cast<char>(0x80 | ((code >> 6) & 0x3F));
            output += static_cast<char>(0x80 | (code & 0x3F));
        }
    }
    bool quoted(std::string& output) {
        if (!take('"')) return false;
        while (position_ < source_.size()) {
            const unsigned char ch = static_cast<unsigned char>(source_[position_++]);
            if (ch == '"') return true;
            if (ch < 0x20) return false;
            if (ch != '\\') { output += static_cast<char>(ch); continue; }
            if (position_ == source_.size()) return false;
            switch (source_[position_++]) {
            case '"': output += '"'; break;
            case '\\': output += '\\'; break;
            case '/': output += '/'; break;
            case 'b': output += '\b'; break;
            case 'f': output += '\f'; break;
            case 'n': output += '\n'; break;
            case 'r': output += '\r'; break;
            case 't': output += '\t'; break;
            case 'u': {
                unsigned code = 0;
                if (!hex(code)) return false;
                if (code >= 0xD800 && code <= 0xDBFF) {
                    if (source_.size() - position_ < 2 ||
                        source_[position_++] != '\\' || source_[position_++] != 'u')
                        return false;
                    unsigned low = 0;
                    if (!hex(low) || low < 0xDC00 || low > 0xDFFF) return false;
                    code = 0x10000 + ((code - 0xD800) << 10) + low - 0xDC00;
                } else if (code >= 0xDC00 && code <= 0xDFFF) return false;
                appendCodepoint(output, code);
                break;
            }
            default: return false;
            }
        }
        return false;
    }
    bool numeric(std::string& output) {
        const size_t start = position_;
        if (position_ < source_.size() && source_[position_] == '-') ++position_;
        if (position_ == source_.size()) return false;
        if (source_[position_] == '0') ++position_;
        else {
            if (source_[position_] < '1' || source_[position_] > '9') return false;
            while (position_ < source_.size() &&
                source_[position_] >= '0' && source_[position_] <= '9') ++position_;
        }
        if (position_ < source_.size() && source_[position_] == '.') {
            ++position_;
            const size_t digits = position_;
            while (position_ < source_.size() &&
                source_[position_] >= '0' && source_[position_] <= '9') ++position_;
            if (position_ == digits) return false;
        }
        if (position_ < source_.size() &&
            (source_[position_] == 'e' || source_[position_] == 'E')) {
            ++position_;
            if (position_ < source_.size() &&
                (source_[position_] == '+' || source_[position_] == '-')) ++position_;
            const size_t digits = position_;
            while (position_ < source_.size() &&
                source_[position_] >= '0' && source_[position_] <= '9') ++position_;
            if (position_ == digits) return false;
        }
        output = source_.substr(start, position_ - start);
        return true;
    }
    bool value(Value& result, unsigned depth) {
        if (depth > 32) return false;
        skip();
        if (position_ == source_.size()) return false;
        const char ch = source_[position_];
        if (ch == '{') {
            ++position_;
            result = object();
            if (take('}')) return true;
            do {
                std::string key;
                if (!quoted(key) || !take(':')) return false;
                Value child;
                if (!value(child, depth + 1)) return false;
                if (!result.object.emplace(std::move(key), std::move(child)).second)
                    return false;
                if (take('}')) return true;
            } while (take(','));
            return false;
        }
        if (ch == '[') {
            ++position_;
            result.type = Value::Type::Array;
            if (take(']')) return true;
            do {
                Value child;
                if (!value(child, depth + 1)) return false;
                result.array.push_back(std::move(child));
                if (take(']')) return true;
            } while (take(','));
            return false;
        }
        if (ch == '"') {
            result.type = Value::Type::String;
            return quoted(result.scalar);
        }
        if (literal("true")) { result = boolean(true); return true; }
        if (literal("false")) { result = boolean(false); return true; }
        if (literal("null")) { result = Value{}; return true; }
        result.type = Value::Type::Number;
        return numeric(result.scalar);
    }
};
inline void quote(std::string& out, const std::string& value) {
    static constexpr char hex[] = "0123456789abcdef";
    out += '"';
    for (unsigned char ch : value) {
        switch (ch) {
        case '"': out += "\\\""; break;
        case '\\': out += "\\\\"; break;
        case '\b': out += "\\b"; break;
        case '\f': out += "\\f"; break;
        case '\n': out += "\\n"; break;
        case '\r': out += "\\r"; break;
        case '\t': out += "\\t"; break;
        default:
            if (ch < 0x20) {
                out += "\\u00";
                out += hex[ch >> 4];
                out += hex[ch & 15];
            } else out += static_cast<char>(ch);
        }
    }
    out += '"';
}
inline void serialize(std::string& out, const Value& v) {
    switch (v.type) {
    case Value::Type::Null: out += "null"; break;
    case Value::Type::Boolean:
    case Value::Type::Number: out += v.scalar; break;
    case Value::Type::String: quote(out, v.scalar); break;
    case Value::Type::Object: {
        out += '{';
        bool first = true;
        for (const auto& entry : v.object) {
            if (!first) out += ',';
            first = false;
            quote(out, entry.first);
            out += ':';
            serialize(out, entry.second);
        }
        out += '}';
        break;
    }
    case Value::Type::Array: {
        out += '[';
        bool first = true;
        for (const auto& entry : v.array) {
            if (!first) out += ',';
            first = false;
            serialize(out, entry);
        }
        out += ']';
        break;
    }
    }
}
inline std::wstring path() {
    wchar_t base[MAX_PATH]{};
    if (FAILED(SHGetFolderPathW(nullptr, CSIDL_LOCAL_APPDATA, nullptr,
                                SHGFP_TYPE_CURRENT, base))) return {};
    return std::wstring(base) + L"\\DisplayPilot\\settings.json";
}
enum class ReadResult { Missing, Valid, Invalid };
inline ReadResult read(Value& doc) {
    const auto file = path();
    if (file.empty()) return ReadResult::Invalid;
    HANDLE h = CreateFileW(file.c_str(), GENERIC_READ, FILE_SHARE_READ,
        nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (h == INVALID_HANDLE_VALUE)
        return GetLastError() == ERROR_FILE_NOT_FOUND ?
            ReadResult::Missing : ReadResult::Invalid;
    LARGE_INTEGER size{};
    const bool sized = GetFileSizeEx(h, &size) != FALSE;
    if (!sized || size.QuadPart <= 0 || size.QuadPart > 1048576) {
        CloseHandle(h);
        return ReadResult::Invalid;
    }
    std::string data(static_cast<size_t>(size.QuadPart), '\0');
    DWORD received = 0;
    const bool ok = ReadFile(h, &data[0], static_cast<DWORD>(data.size()),
                             &received, nullptr) != FALSE;
    CloseHandle(h);
    if (!ok || received != data.size()) return ReadResult::Invalid;
    if (data.size() >= 3 && static_cast<unsigned char>(data[0]) == 0xEF &&
        static_cast<unsigned char>(data[1]) == 0xBB &&
        static_cast<unsigned char>(data[2]) == 0xBF) data.erase(0, 3);
    if (!Parser(data).parse(doc) || doc.type != Value::Type::Object)
        return ReadResult::Invalid;
    return ReadResult::Valid;
}
inline bool write(const Value& doc) {
    const auto file = path();
    if (file.empty() || doc.type != Value::Type::Object) return false;
    const auto slash = file.find_last_of(L'\\');
    if (slash == std::wstring::npos) return false;
    const auto directory = file.substr(0, slash);
    if (!CreateDirectoryW(directory.c_str(), nullptr) &&
        GetLastError() != ERROR_ALREADY_EXISTS) return false;
    std::string data;
    serialize(data, doc);
    const auto temporary = file + L".tmp";
    HANDLE h = CreateFileW(temporary.c_str(), GENERIC_WRITE, 0, nullptr,
        CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (h == INVALID_HANDLE_VALUE) return false;
    DWORD written = 0;
    const bool ok = WriteFile(h, data.data(), static_cast<DWORD>(data.size()),
        &written, nullptr) != FALSE && written == data.size() &&
        FlushFileBuffers(h) != FALSE;
    CloseHandle(h);
    if (!ok) { DeleteFileW(temporary.c_str()); return false; }
    if (!MoveFileExW(temporary.c_str(), file.c_str(),
        MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH)) {
        DeleteFileW(temporary.c_str());
        return false;
    }
    return true;
}
inline bool supported(const Value& doc) {
    int version = 0;
    return asInteger(field(doc, "version"), version) && version >= 1 && version <= 4;
}
inline Value newDocument() {
    Value doc = object();
    doc.object["version"] = number(4);
    doc.object["lightMinutes"] = number(420);
    doc.object["darkMinutes"] = number(1140);
    doc.object["automationEnabled"] = boolean(false);
    doc.object["reduceBrightness"] = boolean(false);
    doc.object["brightnessReductionActive"] = boolean(false);
    doc.object["brightnessRestoreValues"] = object();
    doc.object["scheduleMode"] = number(0);
    doc.object["latitude"] = Value{};
    doc.object["longitude"] = Value{};
    doc.object["locationLabel"] = Value{};
    return doc;
}
inline bool editable(Value& doc) {
    const auto result = read(doc);
    if (result == ReadResult::Missing) {
        doc = newDocument();
        return true;
    }
    return result == ReadResult::Valid && supported(doc);
}
} // namespace json_settings
} // namespace displaypilot
