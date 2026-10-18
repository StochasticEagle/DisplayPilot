#pragma once
#include <windows.h>
#include <wbemidl.h>
#include <string>
#include <vector>

namespace displaypilot {
struct BrightnessKeyEvent {
    std::wstring instance;
    unsigned percent = 0;
};

// Non-blocking WMI brightness event stream, polled from the sole Win32 message
// loop. No asynchronous callback, worker, service, or auxiliary process.
class BrightnessEvents {
public:
    BrightnessEvents() = default;
    BrightnessEvents(const BrightnessEvents&) = delete;
    BrightnessEvents& operator=(const BrightnessEvents&) = delete;
    ~BrightnessEvents();

    bool start();
    void stop();
    bool active() const { return events_ != nullptr; }
    bool poll(std::vector<BrightnessKeyEvent>& changes);
private:
    IWbemServices* services_ = nullptr;
    IEnumWbemClassObject* events_ = nullptr;
};
}
