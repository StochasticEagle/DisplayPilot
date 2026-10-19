#pragma once
#include <windows.h>
#include <locationapi.h>
#include <atomic>
#include <cmath>
#include <new>

namespace displaypilot {
struct LocationCoordinates { double latitude = 0.0; double longitude = 0.0; };

// Windows Location API callbacks are asynchronous. Transfer data to the UI thread,
// never access application settings or UI controls inside a COM callback.
class LocationUpdates final : public ILocationEvents {
public:
    static constexpr UINT message = WM_APP + 61;
    explicit LocationUpdates(HWND owner) : owner_(owner) {}

    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** result) override {
        if (!result) return E_POINTER;
        *result = nullptr;
        if (riid != IID_IUnknown && riid != IID_ILocationEvents) return E_NOINTERFACE;
        *result = static_cast<ILocationEvents*>(this);
        AddRef();
        return S_OK;
    }
    ULONG STDMETHODCALLTYPE AddRef() override { return ++references_; }
    ULONG STDMETHODCALLTYPE Release() override {
        const ULONG count = --references_;
        if (!count) delete this;
        return count;
    }
    HRESULT STDMETHODCALLTYPE OnLocationChanged(REFIID kind, ILocationReport* report) override {
        if (kind != IID_ILatLongReport || !report) return S_OK;
        postCoordinates(report, false);
        return S_OK;
    }
    void postCoordinates(ILocationReport* report, bool manual) {
        ILatLongReport* coordinates = nullptr;
        if (FAILED(report->QueryInterface(IID_ILatLongReport,
                        reinterpret_cast<void**>(&coordinates)))) return;
        double latitude = 0, longitude = 0;
        const bool valid = SUCCEEDED(coordinates->GetLatitude(&latitude)) &&
            SUCCEEDED(coordinates->GetLongitude(&longitude)) &&
            std::isfinite(latitude) && std::isfinite(longitude) &&
            std::abs(latitude) <= 90 && std::abs(longitude) <= 180;
        coordinates->Release();
        if (!valid) return;
        auto* next = new (std::nothrow) LocationCoordinates{latitude, longitude};
        if (next && !PostMessageW(owner_, message, manual ? 1 : 0, reinterpret_cast<LPARAM>(next)))
            delete next;
    }
    HRESULT STDMETHODCALLTYPE OnStatusChanged(REFIID, LOCATION_REPORT_STATUS) override {
        return S_OK;
    }

    // Register only while solar scheduling and automatic updates are enabled.
    bool start() {
        if (service_) return true;
        ILocation* service = nullptr;
        if (FAILED(CoCreateInstance(CLSID_Location, nullptr, CLSCTX_INPROC_SERVER,
                         IID_ILocation, reinterpret_cast<void**>(&service)))) return false;
        if (FAILED(service->RegisterForReport(this, IID_ILatLongReport, 0))) {
            service->Release();
            return false;
        }
        service_ = service;
        return true;
    }
    void stop() {
        if (!service_) return;
        service_->UnregisterForReport(IID_ILatLongReport);
        service_->Release();
        service_ = nullptr;
    }

    // Explicit user action: a single report request, never a timer or loop.
    bool updateNow() {
        ILocation* temporary = nullptr;
        ILocation* service = service_;
        if (!service) {
            if (FAILED(CoCreateInstance(CLSID_Location, nullptr, CLSCTX_INPROC_SERVER,
                         IID_ILocation, reinterpret_cast<void**>(&temporary)))) return false;
            service = temporary;
        }
        ILocationReport* report = nullptr;
        HRESULT result = service->GetReport(IID_ILatLongReport, &report);
        if (SUCCEEDED(result) && report) {
            postCoordinates(report, true);
            report->Release();
        }
        if (temporary) temporary->Release();
        return SUCCEEDED(result) && report != nullptr;
    }
private:
    ~LocationUpdates() { stop(); }
    std::atomic<ULONG> references_{1};
    HWND owner_ = nullptr;
    ILocation* service_ = nullptr;
};
} // namespace displaypilot
