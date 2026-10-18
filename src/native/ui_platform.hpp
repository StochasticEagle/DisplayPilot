#pragma once
#include <windows.h>

namespace displaypilot {
enum class WindowsPresentation { Aero, Fluent, Modern };

inline WindowsPresentation windowsPresentation() {
    struct Version {
        ULONG size;
        ULONG major;
        ULONG minor;
        ULONG build;
        ULONG platform;
        wchar_t servicePack[128];
        USHORT servicePackMajor;
        USHORT servicePackMinor;
        USHORT suiteMask;
        UCHAR productType;
        UCHAR reserved;
    };
    using VersionFunction = LONG (WINAPI*)(Version*);
    const HMODULE module = GetModuleHandleW(L"ntdll.dll");
    const auto query = module ? reinterpret_cast<VersionFunction>(
        GetProcAddress(module, "RtlGetVersion")) : nullptr;
    Version version{};
    version.size = sizeof(version);
    if (query && query(&version) == 0) {
        if (version.major >= 10 && version.build >= 22000)
            return WindowsPresentation::Modern;
        if (version.major >= 10) return WindowsPresentation::Fluent;
    }
    return WindowsPresentation::Aero;
}

inline void applyNativeWindowChrome(HWND window) {
    if (windowsPresentation() != WindowsPresentation::Modern) return;
    const HMODULE module = LoadLibraryW(L"dwmapi.dll");
    if (!module) return;
    using AttributeFunction = HRESULT (WINAPI*)(HWND, DWORD, LPCVOID, DWORD);
    const auto setAttribute = reinterpret_cast<AttributeFunction>(
        GetProcAddress(module, "DwmSetWindowAttribute"));
    if (setAttribute) {
        constexpr DWORD cornerPreference = 33;
        constexpr DWORD round = 2;
        setAttribute(window, cornerPreference, &round, sizeof(round));
    }
    FreeLibrary(module);
}
}
