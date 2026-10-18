#include "wmi_events.hpp"
#include <oleauto.h>
#include <algorithm>

namespace displaypilot {
namespace {
class Bstr {
public:
    explicit Bstr(const wchar_t* value) : value_(SysAllocString(value)) {}
    ~Bstr() { SysFreeString(value_); }
    operator BSTR() const { return value_; }
private:
    BSTR value_;
};
class Variant {
public:
    VARIANT value{};
    Variant() { VariantInit(&value); }
    ~Variant() { VariantClear(&value); }
};
}
BrightnessEvents::~BrightnessEvents() { stop(); }
void BrightnessEvents::stop() {
    if (events_) {
        events_->Release();
        events_ = nullptr;
    }
    if (services_) {
        services_->Release();
        services_ = nullptr;
    }
}
bool BrightnessEvents::start() {
    if (events_) return true;
    stop();
    IWbemLocator* locator = nullptr;
    if (FAILED(CoCreateInstance(CLSID_WbemLocator,nullptr,CLSCTX_INPROC_SERVER,
                                IID_IWbemLocator,reinterpret_cast<void**>(&locator))))
        return false;
    Bstr space(L"ROOT\\WMI");
    const HRESULT connect = locator->ConnectServer(space,nullptr,nullptr,nullptr,
                                                    0,nullptr,nullptr,&services_);
    locator->Release();
    if (FAILED(connect) || !services_) { stop(); return false; }
    if (FAILED(CoSetProxyBlanket(services_, RPC_C_AUTHN_WINNT, RPC_C_AUTHZ_NONE,
               nullptr, RPC_C_AUTHN_LEVEL_CALL, RPC_C_IMP_LEVEL_IMPERSONATE,
               nullptr, EOAC_NONE))) {
        stop();
        return false;
    }
    Bstr language(L"WQL");
    Bstr query(L"SELECT * FROM WmiMonitorBrightnessEvent");
    if (FAILED(services_->ExecNotificationQuery(language,query,
          WBEM_FLAG_RETURN_IMMEDIATELY | WBEM_FLAG_FORWARD_ONLY,
          nullptr,&events_)) || !events_) {
        stop();
        return false;
    }
    return true;
}
bool BrightnessEvents::poll(std::vector<BrightnessKeyEvent>& changes) {
    if (!events_) return false;
    // Calling Next with a zero timeout never waits for a missing event;
    // cap the queue drain to keep display input and painting responsive.
    for (unsigned i=0; i<16; ++i) {
        IWbemClassObject* row = nullptr;
        ULONG received = 0;
        const HRESULT status = events_->Next(0,1,&row,&received);
        if (!received || status == WBEM_S_TIMEDOUT) {
            if (row) row->Release();
            break;
        }
        if (FAILED(status)) {
            if (row) row->Release();
            stop();
            return false;
        }
        if (!row) break;
        Variant instance, brightness, active;
        bool valid = SUCCEEDED(row->Get(L"InstanceName",0,
                   &instance.value,nullptr,nullptr)) &&
                   instance.value.vt == VT_BSTR && instance.value.bstrVal &&
                   SUCCEEDED(row->Get(L"Brightness",0,
                   &brightness.value,nullptr,nullptr));
        const bool activeOK = FAILED(row->Get(L"Active",0,&active.value,nullptr,nullptr)) ||
                active.value.vt != VT_BOOL || active.value.boolVal != VARIANT_FALSE;
        if (valid && activeOK) {
            unsigned level = 101;
            if (brightness.value.vt == VT_UI1) level = brightness.value.bVal;
            if (brightness.value.vt == VT_I4 && brightness.value.lVal >= 0)
                level = static_cast<unsigned>(brightness.value.lVal);
            if (level <= 100)
                changes.push_back({instance.value.bstrVal, level});
        }
        row->Release();
    }
    return true;
}
}
