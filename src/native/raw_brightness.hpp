#pragma once
#include <windows.h>
#include <vector>
#include <cstdint>
#include <cstring>
namespace displaypilot {
// Raw HID Consumer Control brightness usages (0x0C:0x6F/0x70).
// Never requests device input or changes keyboard behavior.
inline int rawBrightnessDelta(LPARAM lparam) {
    UINT bytes=0;
    const UINT header=sizeof(RAWINPUTHEADER);
    if(GetRawInputData(reinterpret_cast<HRAWINPUT>(lparam),RID_INPUT,nullptr,&bytes,header)!=0 ||
       bytes<header+sizeof(RAWHID) || bytes>65536) return 0;
    std::vector<BYTE> buffer(bytes);
    if(GetRawInputData(reinterpret_cast<HRAWINPUT>(lparam),RID_INPUT,buffer.data(),
                       &bytes,header)!=bytes || bytes<header+sizeof(RAWHID)) return 0;
    const auto* raw=reinterpret_cast<const RAWINPUT*>(buffer.data());
    if(raw->header.dwType!=RIM_TYPEHID) return 0;
    const DWORD length=raw->data.hid.dwSizeHid, count=raw->data.hid.dwCount;
    const size_t offset=offsetof(RAWINPUT,data.hid.bRawData);
    if(!length || !count || offset>bytes ||
       static_cast<size_t>(length)*count>bytes-offset) return 0;
    const BYTE* reports=raw->data.hid.bRawData;
    for(DWORD n=0;n<count;++n) {
        const BYTE* report=reports+static_cast<size_t>(n)*length;
        for(DWORD i=0;i+1<length;++i) {
            const unsigned usage=static_cast<unsigned>(report[i]) |
                                  (static_cast<unsigned>(report[i+1])<<8);
            if(usage==0x006f) return 10;
            if(usage==0x0070) return -10;
        }
    }
    return 0;
}
inline bool registerBrightnessKeys(HWND owner) {
    RAWINPUTDEVICE device{};
    device.usUsagePage=0x0c;
    device.usUsage=0x01;
    device.dwFlags=RIDEV_INPUTSINK|RIDEV_DEVNOTIFY;
    device.hwndTarget=owner;
    return RegisterRawInputDevices(&device,1,sizeof(device))!=FALSE;
}
inline void unregisterBrightnessKeys() {
    RAWINPUTDEVICE device{};
    device.usUsagePage=0x0c;
    device.usUsage=0x01;
    device.dwFlags=RIDEV_REMOVE;
    RegisterRawInputDevices(&device,1,sizeof(device));
}
}
