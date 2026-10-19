#pragma once
#include <windows.h>
#include <powrprof.h>
namespace displaypilot {
// Windows shell brightness slider is backed by the active power plan.
// Synchronize it only after a confirmed keyboard brightness change.
inline bool syncWindowsBrightness(unsigned percent) {
    static const GUID video = {0x7516b95f,0xf776,0x4464,{0x8c,0x53,0x06,0x16,0x7f,0x40,0xcc,0x99}};
    static const GUID brightness = {0xaded5e82,0xb909,0x4619,{0x99,0x49,0xf5,0xd7,0x1d,0xac,0x0b,0xcb}};
    GUID* scheme=nullptr;
    if(PowerGetActiveScheme(nullptr,&scheme)!=ERROR_SUCCESS || !scheme) return false;
    SYSTEM_POWER_STATUS status{};
    if(!GetSystemPowerStatus(&status)) {
        LocalFree(scheme);
        return false;
    }
    percent=percent>100?100:percent;
    const DWORD result=status.ACLineStatus==0 ?
        PowerWriteDCValueIndex(nullptr,scheme,&video,&brightness,percent) :
        PowerWriteACValueIndex(nullptr,scheme,&video,&brightness,percent);
    const bool success=result==ERROR_SUCCESS &&
        PowerSetActiveScheme(nullptr,scheme)==ERROR_SUCCESS;
    LocalFree(scheme);
    return success;
}
}
