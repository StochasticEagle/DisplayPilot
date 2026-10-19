#pragma once
#include "display.hpp"
#include <windows.h>
#include <condition_variable>
#include <deque>
#include <mutex>
#include <thread>
#include <string>
#include <new>

namespace displaypilot {
enum class ControlOperation { Brightness, Contrast, Color, Rotation };
struct ControlResult {
    std::wstring key;
    ControlOperation operation = ControlOperation::Brightness;
    unsigned value = 0;
    bool succeeded = false;
    bool synchronizeShell = false;
};

// Serializes verified monitor writes off the UI thread. The UI owns monitor
// inventory and settings: the worker owns only immutable monitor snapshots.
class AsyncControls {
public:
    static constexpr UINT completionMessage = WM_APP + 72;
    AsyncControls() = default;
    AsyncControls(const AsyncControls&) = delete;
    AsyncControls& operator=(const AsyncControls&) = delete;
    ~AsyncControls() { stop(); }

    bool start(HWND owner) {
        std::lock_guard<std::mutex> guard(mutex_);
        if (worker_.joinable() || !owner) return false;
        owner_ = owner;
        stopping_ = false;
        try { worker_ = std::thread([this] { run(); }); }
        catch (...) { return false; }
        return true;
    }
    bool enqueue(const Monitor& monitor, const std::wstring& key,
                 ControlOperation kind, unsigned value, bool shellSync=false) {
        std::lock_guard<std::mutex> guard(mutex_);
        if (!worker_.joinable() || stopping_) return false;
        for (auto& queued : jobs_) {
            if (queued.key==key && queued.kind==kind) {
                queued.monitor=monitor;
                queued.value=value;
                queued.shellSync=queued.shellSync || shellSync;
                signal_.notify_one();
                return true;
            }
        }
        jobs_.push_back(Job{monitor,key,kind,value,shellSync});
        signal_.notify_one();
        return true;
    }
    void stop() {
        {
            std::lock_guard<std::mutex> guard(mutex_);
            if (!worker_.joinable()) return;
            stopping_ = true;
            jobs_.clear();
        }
        signal_.notify_all();
        worker_.join();
    }
    void drain() {
        std::unique_lock<std::mutex> lock(mutex_);
        idle_.wait(lock,[this]{return jobs_.empty() && !busy_;});
    }
private:
    struct Job {
        Monitor monitor;
        std::wstring key;
        ControlOperation kind;
        unsigned value;
        bool shellSync;
    };
    void run() {
        const HRESULT com=CoInitializeEx(nullptr,COINIT_MULTITHREADED);
        for (;;) {
            Job job{};
            {
                std::unique_lock<std::mutex> lock(mutex_);
                signal_.wait(lock,[this]{return stopping_ || !jobs_.empty();});
                if(stopping_) break;
                job=std::move(jobs_.front());
                jobs_.pop_front();
                busy_=true;
            }
            bool success=false;
            switch(job.kind) {
            case ControlOperation::Brightness:
                success=setBrightness(job.monitor,job.value); break;
            case ControlOperation::Contrast:
                success=setContrast(job.monitor,job.value); break;
            case ControlOperation::Color:
                success=setColorPreset(job.monitor,job.value); break;
            case ControlOperation::Rotation:
                success=setRotation(job.monitor,job.value); break;
            }
            auto* result=new (std::nothrow) ControlResult{
                job.key,job.kind,job.value,success,job.shellSync};
            if(result && !PostMessageW(owner_,completionMessage,0,
                                     reinterpret_cast<LPARAM>(result)))
                delete result;
            {
                std::lock_guard<std::mutex> guard(mutex_);
                busy_=false;
            }
            idle_.notify_all();
        }
        if(SUCCEEDED(com)) CoUninitialize();
        {
            std::lock_guard<std::mutex> guard(mutex_);
            busy_=false;
        }
        idle_.notify_all();
    }
    HWND owner_ = nullptr;
    std::mutex mutex_;
    std::condition_variable signal_;
    std::condition_variable idle_;
    std::deque<Job> jobs_;
    std::thread worker_;
    bool stopping_ = false;
    bool busy_ = false;
};
}
