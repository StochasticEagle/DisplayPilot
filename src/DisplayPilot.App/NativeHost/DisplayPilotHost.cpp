// SPDX-License-Identifier: Unlicense
// Framework-dependent .NET 10 launcher; the host/runtime remain system-installed.
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <objbase.h>
#include <string>
#include <tuple>
#include <vector>
#include <cwchar>
#include <cstdint>

namespace {
using hostfxr_handle = void*;
struct hostfxr_initialize_parameters {
    size_t size;
    const wchar_t* host_path;
    const wchar_t* dotnet_root;
};
using initialize_fn = int(__cdecl*)(const wchar_t*, const hostfxr_initialize_parameters*, hostfxr_handle*);
using get_delegate_fn = int(__cdecl*)(hostfxr_handle, int, void**);
using set_property_fn = int(__cdecl*)(hostfxr_handle, const wchar_t*, const wchar_t*);
using close_fn = int(__cdecl*)(hostfxr_handle);
using load_assembly_fn = int(__cdecl*)(const wchar_t*, const wchar_t*, const wchar_t*, const wchar_t*, void*, void**);
using managed_main_fn = int(__cdecl*)(void*, int);

void ReportFailure(const wchar_t* description, int code = 0) {
    std::wstring message = description;
    if (code != 0) {
        wchar_t details[40]{};
        swprintf_s(details, L"\nHRESULT: 0x%08X", static_cast<unsigned>(code));
        message += details;
    }
    MessageBoxW(nullptr, message.c_str(), L"DisplayPilot startup", MB_OK | MB_ICONERROR);
}

std::wstring ReadEnvironment(const wchar_t* name) {
    DWORD length = GetEnvironmentVariableW(name, nullptr, 0);
    if (length == 0) return {};
    std::wstring result(length, L'\0');
    DWORD actual = GetEnvironmentVariableW(name, result.data(), length);
    if (actual == 0 || actual >= length) return {};
    result.resize(actual);
    return result;
}

std::wstring ReadDotnetInstallLocation() {
    constexpr wchar_t key[] = L"SOFTWARE\\dotnet\\Setup\\InstalledVersions\\x64";
    DWORD size = 0;
    if (RegGetValueW(HKEY_LOCAL_MACHINE, key, L"InstallLocation",
                    RRF_RT_REG_SZ | RRF_SUBKEY_WOW6464KEY, nullptr, nullptr, &size) != ERROR_SUCCESS) {
        return {};
    }
    std::wstring result(size / sizeof(wchar_t), L'\0');
    if (RegGetValueW(HKEY_LOCAL_MACHINE, key, L"InstallLocation",
                     RRF_RT_REG_SZ | RRF_SUBKEY_WOW6464KEY, nullptr,
                     result.data(), &size) != ERROR_SUCCESS) {
        return {};
    }
    result.resize(wcslen(result.c_str()));
    return result;
}

std::wstring Join(const std::wstring& lhs, const wchar_t* rhs) {
    return lhs + (lhs.empty() || lhs.back() == L'\\' ? L"" : L"\\") + rhs;
}

bool IsFile(const std::wstring& file) {
    DWORD attributes = GetFileAttributesW(file.c_str());
    return attributes != INVALID_FILE_ATTRIBUTES && !(attributes & FILE_ATTRIBUTE_DIRECTORY);
}

std::tuple<unsigned long, unsigned long, unsigned long> Version(const std::wstring& s) {
    const wchar_t* current = s.c_str();
    wchar_t* end = nullptr;
    unsigned long major = wcstoul(current, &end, 10);
    if (end == current || *end != L'.') return {};
    current = end + 1;
    unsigned long minor = wcstoul(current, &end, 10);
    if (end == current || *end != L'.') return {};
    current = end + 1;
    unsigned long patch = wcstoul(current, &end, 10);
    if (end == current || *end != L'\0') return {};
    return {major, minor, patch};
}

std::wstring LatestVersion(const std::wstring& folder, unsigned long major) {
    WIN32_FIND_DATAW data{};
    HANDLE find = FindFirstFileW(Join(folder, L"*").c_str(), &data);
    if (find == INVALID_HANDLE_VALUE) return {};
    std::wstring best;
    std::tuple<unsigned long, unsigned long, unsigned long> selected{};
    do {
        if (!(data.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY)) continue;
        std::wstring name = data.cFileName;
        auto version = Version(name);
        if (std::get<0>(version) != major || version <= selected) continue;
        selected = version;
        best = name;
    } while (FindNextFileW(find, &data));
    FindClose(find);
    return best;
}

std::wstring ExecutablePath() {
    std::wstring path(32768, L'\0');
    DWORD size = GetModuleFileNameW(nullptr, path.data(), static_cast<DWORD>(path.size()));
    if (size == 0 || size >= path.size()) return {};
    path.resize(size);
    return path;
}

int Launch(const std::wstring& dotnet, const std::wstring& executable) {
    // The Desktop Runtime's installed configuration references Microsoft.NETCore.App 10.
    std::wstring desktopRoot = Join(dotnet, L"shared\\Microsoft.WindowsDesktop.App");
    std::wstring desktopVersion = LatestVersion(desktopRoot, 10);
    std::wstring fxrRoot = Join(dotnet, L"host\\fxr");
    std::wstring fxrVersion = LatestVersion(fxrRoot, 10);
    if (desktopVersion.empty() || fxrVersion.empty()) {
        ReportFailure(L".NET 10 Desktop Runtime (x64) is not installed.");
        return 1;
    }
    std::wstring config = Join(Join(desktopRoot, desktopVersion), L"Microsoft.WindowsDesktop.App.runtimeconfig.json");
    std::wstring fxrPath = Join(Join(fxrRoot, fxrVersion), L"hostfxr.dll");
    if (!IsFile(config) || !IsFile(fxrPath)) {
        ReportFailure(L".NET Desktop Runtime installation is incomplete.");
        return 1;
    }

    HMODULE fxr = LoadLibraryExW(fxrPath.c_str(), nullptr,
                                LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
    if (!fxr) {
        ReportFailure(L"Failed to load the installed .NET runtime host.", static_cast<int>(GetLastError()));
        return 1;
    }
    auto initialize = reinterpret_cast<initialize_fn>(GetProcAddress(fxr, "hostfxr_initialize_for_runtime_config"));
    auto get_delegate = reinterpret_cast<get_delegate_fn>(GetProcAddress(fxr, "hostfxr_get_runtime_delegate"));
    auto set_property = reinterpret_cast<set_property_fn>(GetProcAddress(fxr, "hostfxr_set_runtime_property_value"));
    auto close = reinterpret_cast<close_fn>(GetProcAddress(fxr, "hostfxr_close"));
    if (!initialize || !get_delegate || !set_property || !close) {
        ReportFailure(L"The installed .NET runtime host is missing required hosting APIs.");
        FreeLibrary(fxr);
        return 1;
    }

    hostfxr_initialize_parameters parameters{sizeof(parameters), executable.c_str(), dotnet.c_str()};
    hostfxr_handle context = nullptr;
    int code = initialize(config.c_str(), &parameters, &context);
    if (code < 0 || !context) {
        ReportFailure(L"Failed to initialize the installed .NET 10 runtime.", code);
        FreeLibrary(fxr);
        return 1;
    }

    const std::wstring directory = executable.substr(0, executable.find_last_of(L"\\/"));
    code = set_property(context, L"APP_PATHS", directory.c_str());
    if (code < 0) {
        ReportFailure(L"Failed to configure DisplayPilot assembly search paths.", code);
        close(context);
        FreeLibrary(fxr);
        return 1;
    }

    load_assembly_fn load_assembly = nullptr;
    // 3 = hdt_load_assembly_and_get_function_pointer.
    code = get_delegate(context, 3, reinterpret_cast<void**>(&load_assembly));
    if (code < 0 || !load_assembly) {
        ReportFailure(L"Failed to initialize the .NET entry point loader.", code);
        close(context);
        FreeLibrary(fxr);
        return 1;
    }

    managed_main_fn main = nullptr;
    std::wstring assembly = Join(directory, L"DisplayPilot.dll");
    code = load_assembly(assembly.c_str(), L"DisplayPilot.App.Program, DisplayPilot", L"NativeMain",
                         reinterpret_cast<const wchar_t*>(static_cast<intptr_t>(-1)), nullptr,
                         reinterpret_cast<void**>(&main));
    if (code < 0 || !main) {
        ReportFailure(L"Failed to load DisplayPilot.dll from the installation directory.", code);
        close(context);
        FreeLibrary(fxr);
        return 1;
    }

    int result = main(nullptr, 0);
    close(context);
    FreeLibrary(fxr);
    return result;
}
} // namespace

int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR, int) {
    // WinUI needs an STA thread.
    HRESULT hr = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
    if (FAILED(hr)) {
        ReportFailure(L"Failed to initialize the WinUI STA thread.", hr);
        return 1;
    }
    std::wstring executable = ExecutablePath();
    if (executable.empty()) {
        ReportFailure(L"Cannot locate the DisplayPilot executable.");
        CoUninitialize();
        return 1;
    }
    std::vector<std::wstring> candidates;
    auto env64 = ReadEnvironment(L"DOTNET_ROOT_X64");
    auto env = ReadEnvironment(L"DOTNET_ROOT");
    auto registry = ReadDotnetInstallLocation();
    auto programFiles = ReadEnvironment(L"ProgramW6432");
    if (!env64.empty()) candidates.push_back(env64);
    if (!env.empty()) candidates.push_back(env);
    if (!registry.empty()) candidates.push_back(registry);
    if (!programFiles.empty()) candidates.push_back(Join(programFiles, L"dotnet"));

    int result = 1;
    bool found = false;
    for (const auto& candidate : candidates) {
        if (LatestVersion(Join(candidate, L"shared\\Microsoft.WindowsDesktop.App"), 10).empty()) continue;
        found = true;
        result = Launch(candidate, executable);
        break;
    }
    if (!found) ReportFailure(L".NET 10 Desktop Runtime (x64) is required. Install it using DisplayPilot Setup.");
    CoUninitialize();
    return result;
}
