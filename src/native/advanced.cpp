#include "advanced.hpp"
#include "ui_platform.hpp"
#include <commctrl.h>
#include <winver.h>
#include <vector>
#include <algorithm>
#include <cmath>
#include <cwchar>
#include <string>
#include <cstring>

namespace displaypilot {
namespace {
constexpr int themeLightId=100, themeDarkId=101, scheduleId=110, reduceId=111;
constexpr int modeId=112, lightId=113, darkId=114, latitudeId=115;
constexpr int longitudeId=116, labelId=117, saveId=118, startupId=119;
constexpr int diagnosticsId=120, brandIconId=121;
constexpr int automaticLocationId=122, updateLocationId=123;
constexpr int appIconId=1;
constexpr int monitorStart=2000;
int toInt(float n) { return static_cast<int>(std::lround(n)); }
std::wstring read(HWND window) {
    if (!window) return {};
    const int len = GetWindowTextLengthW(window);
    std::wstring value(static_cast<size_t>(len + 1), L'\0');
    GetWindowTextW(window, &value[0], len + 1);
    value.resize(static_cast<size_t>(len));
    return value;
}
std::wstring formatMinute(unsigned minute) {
    wchar_t buffer[16]{};
    swprintf_s(buffer, L"%02u:%02u", minute / 60, minute % 60);
    return buffer;
}
bool parseMinute(const std::wstring& input, unsigned& out) {
    unsigned hours = 0, minutes = 0; wchar_t trailing = L'\0';
    if (swscanf_s(input.c_str(), L"%u:%u%c", &hours, &minutes, &trailing,
                  static_cast<unsigned>(1)) != 2 ||
        hours > 23 || minutes > 59) return false;
    out = hours * 60 + minutes;
    return true;
}
bool readDecimal(HWND edit, double& number, double range) {
    const auto value = read(edit);
    if (value.empty()) return false;
    wchar_t* last = nullptr;
    const double parsed = wcstod(value.c_str(), &last);
    if (!last || last == value.c_str() || *last != L'\0' || !std::isfinite(parsed) ||
        std::abs(parsed) > range) return false;
    number = parsed;
    return true;
}
std::wstring executableVersion() {
    wchar_t executable[MAX_PATH]{};
    if (!GetModuleFileNameW(nullptr, executable, MAX_PATH)) return L"Version unavailable";
    DWORD unused = 0;
    const DWORD bytes = GetFileVersionInfoSizeW(executable, &unused);
    if (!bytes) return L"Version unavailable";
    std::vector<BYTE> data(bytes);
    if (!GetFileVersionInfoW(executable, 0, bytes, data.data()))
        return L"Version unavailable";
    VS_FIXEDFILEINFO* info = nullptr;
    UINT length = 0;
    if (!VerQueryValueW(data.data(), L"\\", reinterpret_cast<void**>(&info), &length) ||
        !info || length < sizeof(VS_FIXEDFILEINFO) ||
        info->dwSignature != 0xFEEF04BD) return L"Version unavailable";
    std::wstring version = std::to_wstring(HIWORD(info->dwProductVersionMS)) + L"." +
        std::to_wstring(LOWORD(info->dwProductVersionMS)) + L"." +
        std::to_wstring(HIWORD(info->dwProductVersionLS));
    const auto revision = LOWORD(info->dwProductVersionLS);
    if (revision) version += L"." + std::to_wstring(revision);
    return L"Version " + version;
}
bool startupEnabled() {
    HKEY key=nullptr;
    if (RegOpenKeyExW(HKEY_CURRENT_USER,
            L"Software\\Microsoft\\Windows\\CurrentVersion\\Run", 0,
            KEY_QUERY_VALUE, &key) != ERROR_SUCCESS) return false;
    DWORD type=0, size=0;
    const bool enabled = RegQueryValueExW(key,L"DisplayPilot",nullptr,
                      &type,nullptr,&size)==ERROR_SUCCESS && type==REG_SZ;
    RegCloseKey(key);
    return enabled;
}
void setStartup(bool enabled) {
    HKEY key = nullptr;
    if (RegCreateKeyExW(HKEY_CURRENT_USER,
        L"Software\\Microsoft\\Windows\\CurrentVersion\\Run", 0, nullptr, 0,
        KEY_SET_VALUE, nullptr, &key, nullptr) != ERROR_SUCCESS) return;
    if (!enabled) RegDeleteValueW(key, L"DisplayPilot");
    else {
        std::wstring executable(32768, L'\0');
        DWORD bytes = GetModuleFileNameW(nullptr, &executable[0],
                                          static_cast<DWORD>(executable.size()));
        if (bytes && bytes < executable.size()) {
            executable.resize(bytes);
            executable = L"\"" + executable + L"\"";
            RegSetValueExW(key, L"DisplayPilot", 0, REG_SZ,
                reinterpret_cast<const BYTE*>(executable.c_str()),
                static_cast<DWORD>((executable.size()+1)*sizeof(wchar_t)));
        }
    }
    RegCloseKey(key);
}
}

AdvancedWindow::~AdvancedWindow() {
    if (font_) DeleteObject(font_);
    if (titleFont_) DeleteObject(titleFont_);
}

bool AdvancedWindow::create(HINSTANCE instance) {
    instance_ = instance;
    INITCOMMONCONTROLSEX controls{};
    controls.dwSize = sizeof(controls);
    controls.dwICC = ICC_BAR_CLASSES;
    if (!InitCommonControlsEx(&controls)) return false;
    WNDCLASSEXW cls{};
    cls.cbSize=sizeof(cls);
    cls.hInstance=instance;
    cls.lpfnWndProc=procedure;
    cls.hCursor=LoadCursorW(nullptr,IDC_ARROW);
    cls.hbrBackground=reinterpret_cast<HBRUSH>(COLOR_WINDOW+1);
    appIcon_ = static_cast<HICON>(LoadImageW(instance, MAKEINTRESOURCEW(appIconId),
        IMAGE_ICON, 0, 0, LR_DEFAULTSIZE | LR_SHARED));
    cls.hIcon = appIcon_;
    cls.hIconSm = static_cast<HICON>(LoadImageW(instance, MAKEINTRESOURCEW(appIconId),
        IMAGE_ICON, GetSystemMetrics(SM_CXSMICON), GetSystemMetrics(SM_CYSMICON), LR_SHARED));
    cls.lpszClassName=L"DisplayPilotAdvanced";
    if (!RegisterClassExW(&cls) && GetLastError()!=ERROR_CLASS_ALREADY_EXISTS)
        return false;
    window_=CreateWindowExW(0,cls.lpszClassName,L"DisplayPilot - Advanced",
             WS_OVERLAPPEDWINDOW|WS_VSCROLL, CW_USEDEFAULT,CW_USEDEFAULT,
             860,690,nullptr,nullptr,instance,this);
    if (!window_) return false;
    SendMessageW(window_, WM_SETICON, ICON_BIG, reinterpret_cast<LPARAM>(cls.hIcon));
    SendMessageW(window_, WM_SETICON, ICON_SMALL, reinterpret_cast<LPARAM>(cls.hIconSm));
    applyNativeWindowChrome(window_);
    HDC dc=GetDC(window_);
    dpi_ = dc ? GetDeviceCaps(dc, LOGPIXELSY) : 96;
    if (dc) ReleaseDC(window_,dc);
    updateFonts();
    return true;
}
void AdvancedWindow::updateFonts() {
    NONCLIENTMETRICSW metrics{};
    metrics.cbSize = sizeof(metrics);
    HFONT normal = nullptr;
    LOGFONTW title{};
    if (SystemParametersInfoW(SPI_GETNONCLIENTMETRICS, sizeof(metrics), &metrics, 0)) {
        normal = CreateFontIndirectW(&metrics.lfMessageFont);
        title = metrics.lfMessageFont;
    } else {
        wcscpy_s(title.lfFaceName, L"Segoe UI");
    }
    if (!normal) normal = CreateFontW(-MulDiv(9, dpi_, 72), 0, 0, 0, FW_NORMAL,
        FALSE, FALSE, FALSE, DEFAULT_CHARSET, OUT_DEFAULT_PRECIS,
        CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_SWISS,
        L"Segoe UI");
    title.lfHeight = -MulDiv(14, dpi_, 72);
    title.lfWeight = FW_SEMIBOLD;
    HFONT heading = CreateFontIndirectW(&title);
    if (normal) {
        for (auto* child : children_) {
            if (IsWindow(child))
                SendMessageW(child, WM_SETFONT, reinterpret_cast<WPARAM>(normal), TRUE);
        }
        HFONT old = font_;
        font_ = normal;
        if (old) DeleteObject(old);
    }
    if (heading) {
        if (brandName_ && IsWindow(brandName_))
            SendMessageW(brandName_, WM_SETFONT, reinterpret_cast<WPARAM>(heading), TRUE);
        HFONT old = titleFont_;
        titleFont_ = heading;
        if (old) DeleteObject(old);
    }
}

HWND AdvancedWindow::add(const wchar_t* kind,const std::wstring& caption,DWORD style,int id) {
    HWND child=CreateWindowExW(0,kind,caption.c_str(),
        WS_CHILD|WS_VISIBLE|style,0,0,10,10,window_,
        reinterpret_cast<HMENU>(static_cast<INT_PTR>(id)),instance_,nullptr);
    if (child) {
        SendMessageW(child,WM_SETFONT,reinterpret_cast<WPARAM>(font_),TRUE);
        children_.push_back(child);
    }
    return child;
}
bool AdvancedWindow::visible() const {
    return window_ && IsWindowVisible(window_);
}
void AdvancedWindow::show(std::vector<Monitor>* monitors,Settings* settings,bool appearance) {
    monitors_=monitors;
    settings_=settings;
    themesAvailable_=appearance;
    rebuild();
    ShowWindow(window_,SW_SHOWNORMAL);
    SetForegroundWindow(window_);
}
void AdvancedWindow::hide() {
    if (window_) ShowWindow(window_,SW_HIDE);
}
void AdvancedWindow::refresh() {
    if (visible()) rebuild();
}
void AdvancedWindow::rebuild() {
    if (!window_) return;
    for (auto* child: children_) DestroyWindow(child);
    children_.clear();
    rows_.clear();
    scheduleCheckbox_=brightnessCheckbox_=mode_=lightTime_=darkTime_=nullptr;
    latitude_=longitude_=label_=startup_=status_=save_=diagnostics_=nullptr;
    automaticLocation_=updateLocation_=nullptr;
    displaysHeading_=scheduleHeading_=integrationHeading_=nullptr;
    brandIcon_=brandName_=brandVersion_=nullptr;
    lightCaption_=darkCaption_=latitudeCaption_=longitudeCaption_=locationCaption_=nullptr;
    if (!monitors_ || !settings_) return;
    brandIcon_=add(L"STATIC",L"",SS_OWNERDRAW,brandIconId);
    brandName_=add(L"STATIC",L"DisplayPilot",SS_LEFT,0);
    brandVersion_=add(L"STATIC",executableVersion(),SS_LEFT,0);
    if (brandName_ && titleFont_)
        SendMessageW(brandName_, WM_SETFONT, reinterpret_cast<WPARAM>(titleFont_), TRUE);
    displaysHeading_=add(L"STATIC",L"Displays",SS_LEFT,0);
    for (unsigned i=0;i<monitors_->size();++i) {
        auto& m=(*monitors_)[i];
        RowControls row;
        row.label=add(L"STATIC",m.name,SS_LEFT,static_cast<int>(monitorStart+i*10+9));
        if (m.brightnessAvailable) {
            row.members.push_back(add(L"STATIC",L"Brightness",0,0));
            row.brightness=add(TRACKBAR_CLASSW,L"",TBS_HORZ|TBS_NOTICKS|WS_TABSTOP,
                    static_cast<int>(monitorStart+i*10));
            if(row.brightness) {
                SendMessageW(row.brightness,TBM_SETRANGE,TRUE,MAKELPARAM(0,100));
                SendMessageW(row.brightness,TBM_SETPOS,TRUE,m.brightness);
            }
        }
        if (m.contrastAvailable) {
            row.members.push_back(add(L"STATIC",L"Contrast",0,0));
            row.contrast=add(TRACKBAR_CLASSW,L"",TBS_HORZ|TBS_NOTICKS|WS_TABSTOP,
                    static_cast<int>(monitorStart+i*10+1));
            if(row.contrast) {
                SendMessageW(row.contrast,TBM_SETRANGE,TRUE,MAKELPARAM(0,100));
                SendMessageW(row.contrast,TBM_SETPOS,TRUE,m.contrast);
            }
        }
        if (m.colorAvailable) {
            row.members.push_back(add(L"STATIC",L"Color preset",0,0));
            row.color=add(L"COMBOBOX",L"",CBS_DROPDOWNLIST|WS_VSCROLL|WS_TABSTOP,
                    static_cast<int>(monitorStart+i*10+2));
            for(unsigned p=0;p<m.colorPresets.size();++p) {
                auto text=colorPresetLabel(m.colorPresets[p]);
                auto idx=SendMessageW(row.color,CB_ADDSTRING,0,
                                      reinterpret_cast<LPARAM>(text.c_str()));
                SendMessageW(row.color,CB_SETITEMDATA,idx,m.colorPresets[p]);
                if(m.colorPresets[p]==m.colorPreset)
                    SendMessageW(row.color,CB_SETCURSEL,idx,0);
            }
            row.colorApply=add(L"BUTTON",L"Apply",BS_PUSHBUTTON|WS_TABSTOP,
                    static_cast<int>(monitorStart+i*10+3));
        }
        if (m.rotationAvailable) {
            row.members.push_back(add(L"STATIC",L"Orientation",0,0));
            row.rotation=add(L"COMBOBOX",L"",CBS_DROPDOWNLIST|WS_VSCROLL|WS_TABSTOP,
                    static_cast<int>(monitorStart+i*10+4));
            for(unsigned p=0;p<4;++p) {
                auto text=rotationLabel(p);
                auto idx=SendMessageW(row.rotation,CB_ADDSTRING,0,
                                      reinterpret_cast<LPARAM>(text.c_str()));
                SendMessageW(row.rotation,CB_SETITEMDATA,idx,p);
                if(p==m.rotation) SendMessageW(row.rotation,CB_SETCURSEL,idx,0);
            }
            row.rotationApply=add(L"BUTTON",L"Apply",BS_PUSHBUTTON|WS_TABSTOP,
                    static_cast<int>(monitorStart+i*10+5));
        }
        rows_.push_back(std::move(row));
    }
    {
        scheduleHeading_=add(L"STATIC",L"Appearance and scheduling",SS_LEFT,0);
        if (themesAvailable_) {
            add(L"BUTTON",L"Light mode",BS_PUSHBUTTON|WS_TABSTOP,themeLightId);
            add(L"BUTTON",L"Dark mode",BS_PUSHBUTTON|WS_TABSTOP,themeDarkId);
        }
        scheduleCheckbox_=add(L"BUTTON",L"Enable automatic schedule",
                               BS_AUTOCHECKBOX|WS_TABSTOP,scheduleId);
        brightnessCheckbox_=add(L"BUTTON",L"Reduce brightness during dark hours",
                               BS_AUTOCHECKBOX|WS_TABSTOP,reduceId);
        mode_=add(L"COMBOBOX",L"",CBS_DROPDOWNLIST|WS_VSCROLL|WS_TABSTOP,modeId);
        SendMessageW(mode_,CB_ADDSTRING,0,reinterpret_cast<LPARAM>(L"Fixed times"));
        SendMessageW(mode_,CB_ADDSTRING,0,reinterpret_cast<LPARAM>(L"Sunrise / sunset"));
        SendMessageW(mode_,CB_SETCURSEL,
            settings_->scheduleMode==ScheduleMode::Solar?1:0,0);
        lightCaption_=add(L"STATIC",L"Light at",SS_LEFT,0);
        darkCaption_=add(L"STATIC",L"Dark at",SS_LEFT,0);
        latitudeCaption_=add(L"STATIC",L"Latitude",SS_LEFT,0);
        longitudeCaption_=add(L"STATIC",L"Longitude",SS_LEFT,0);
        locationCaption_=add(L"STATIC",L"Location",SS_LEFT,0);
        lightTime_=add(L"EDIT",formatMinute(settings_->lightMinute),
                       WS_BORDER|ES_AUTOHSCROLL|WS_TABSTOP,lightId);
        darkTime_=add(L"EDIT",formatMinute(settings_->darkMinute),
                      WS_BORDER|ES_AUTOHSCROLL|WS_TABSTOP,darkId);
        latitude_=add(L"EDIT",std::to_wstring(settings_->latitude),
                       WS_BORDER|ES_AUTOHSCROLL|WS_TABSTOP,latitudeId);
        longitude_=add(L"EDIT",std::to_wstring(settings_->longitude),
                        WS_BORDER|ES_AUTOHSCROLL|WS_TABSTOP,longitudeId);
        label_=add(L"EDIT",settings_->locationLabel,
                   WS_BORDER|ES_AUTOHSCROLL|WS_TABSTOP,labelId);
        automaticLocation_=add(L"BUTTON",L"Automatically update location when changed",
            BS_AUTOCHECKBOX|WS_TABSTOP,automaticLocationId);
        SendMessageW(automaticLocation_, BM_SETCHECK,
            settings_->automaticLocation ? BST_CHECKED : BST_UNCHECKED, 0);
        updateLocation_=add(L"BUTTON",L"Update location now",
            BS_PUSHBUTTON|WS_TABSTOP,updateLocationId);
        save_=add(L"BUTTON",L"Save schedule",BS_PUSHBUTTON|WS_TABSTOP,saveId);
        SendMessageW(scheduleCheckbox_,BM_SETCHECK,
                     settings_->scheduleEnabled?BST_CHECKED:BST_UNCHECKED,0);
        SendMessageW(brightnessCheckbox_,BM_SETCHECK,
                     settings_->reduceBrightness?BST_CHECKED:BST_UNCHECKED,0);
    }
    integrationHeading_=add(L"STATIC",L"Windows integration",SS_LEFT,0);
    startup_=add(L"BUTTON",L"Start when signing into Windows",
                   BS_AUTOCHECKBOX|WS_TABSTOP,startupId);
    SendMessageW(startup_,BM_SETCHECK,
                 startupEnabled()?BST_CHECKED:BST_UNCHECKED,0);
    diagnostics_=add(L"BUTTON",L"Copy diagnostics",BS_PUSHBUTTON|WS_TABSTOP,
                     diagnosticsId);
    status_=add(L"STATIC",L"",SS_LEFT,0);
    scroll_=0;
    layout();
}
void AdvancedWindow::layout() {
    if (!window_) return;
    RECT bounds{};
    GetClientRect(window_, &bounds);
    const int width = bounds.right;
    const int height = bounds.bottom;
    const int margin = px(24);
    const int gap = px(12);
    const int full = std::max(px(250), width - margin * 2);
    const int labelWidth = px(125);
    const int fieldX = margin + labelWidth;
    const int fieldWidth = std::max(px(100), full - labelWidth - px(84));
    int y = px(18) - scroll_;
    auto place = [](HWND child, int x, int top, int w, int h) {
        if (child) MoveWindow(child, x, top, w, h, TRUE);
    };
    auto heading = [&](HWND child) {
        place(child, margin, y, full, px(28));
        y += px(36);
    };
    const int iconSize = px(32);
    const int textX = margin + iconSize + px(12);
    place(brandIcon_, margin, y + px(4), iconSize, iconSize);
    place(brandName_, textX, y, std::max(0, full - (textX - margin)), px(27));
    place(brandVersion_, textX, y + px(28),
          std::max(0, full - (textX - margin)), px(22));
    y += px(64);
    heading(displaysHeading_);
    if (rows_.empty()) {
        y += px(10);
    }
    for (auto& row : rows_) {
        place(row.label, margin, y, full, px(27));
        y += px(31);
        int member = 0;
        auto control = [&](HWND child, HWND apply) {
            if (!child) return;
            HWND label = member < static_cast<int>(row.members.size())
                ? row.members[static_cast<size_t>(member++)] : nullptr;
            place(label, margin + px(4), y + px(5), labelWidth - px(8), px(24));
            place(child, fieldX, y, fieldWidth, apply ? px(240) : px(32));
            if (apply) place(apply, fieldX + fieldWidth + gap, y, px(68), px(29));
            y += px(39);
        };
        control(row.brightness, nullptr);
        control(row.contrast, nullptr);
        control(row.color, row.colorApply);
        control(row.rotation, row.rotationApply);
        y += px(17);
    }
    y += px(8);
    heading(scheduleHeading_);
    if (themesAvailable_) {
        place(GetDlgItem(window_, themeLightId), margin, y, px(132), px(31));
        place(GetDlgItem(window_, themeDarkId), margin + px(145), y, px(132), px(31));
        y += px(43);
    }
    place(scheduleCheckbox_, margin, y, full, px(27));
    y += px(31);
    place(brightnessCheckbox_, margin, y, full, px(27));
    y += px(40);

    place(mode_, margin, y, px(184), px(250));
    place(lightCaption_, margin + px(199), y + px(5), px(64), px(24));
    place(lightTime_, margin + px(266), y, px(73), px(27));
    place(darkCaption_, margin + px(351), y + px(5), px(62), px(24));
    place(darkTime_, margin + px(415), y, px(73), px(27));
    y += px(42);

    place(latitudeCaption_, margin, y, px(72), px(24));
    place(latitude_, margin + px(76), y, px(95), px(27));
    place(longitudeCaption_, margin + px(187), y, px(78), px(24));
    place(longitude_, margin + px(268), y, px(95), px(27));
    y += px(39);
    place(locationCaption_, margin, y + px(4), px(72), px(24));
    place(label_, margin + px(76), y, std::max(px(180), full - px(76)), px(27));
    y += px(44);

    place(automaticLocation_, margin, y, std::min(full, px(370)), px(28));
    y += px(34);
    place(updateLocation_, margin, y, px(170), px(32));
    y += px(42);
    place(save_, margin, y, px(170), px(32));
    y += px(57);
    heading(integrationHeading_);
    place(startup_, margin, y, std::min(full, px(340)), px(30));
    place(diagnostics_, margin + px(350), y, px(160), px(30));
    y += px(46);
    place(status_, margin, y, full, px(48));
    y += px(60);

    contentHeight_ = y + scroll_;
    SCROLLINFO info{};
    info.cbSize = sizeof(info);
    info.fMask = SIF_RANGE | SIF_PAGE | SIF_POS;
    info.nMin = 0;
    info.nMax = std::max(0, contentHeight_ - 1);
    info.nPage = static_cast<UINT>(std::max(0, height));
    info.nPos = scroll_;
    SetScrollInfo(window_, SB_VERT, &info, TRUE);
}
void AdvancedWindow::scrollBy(int difference) {
    RECT bounds{};GetClientRect(window_,&bounds);
    scroll_=std::max(0,std::min(
        std::max(0,contentHeight_-static_cast<int>(bounds.bottom)),scroll_+difference));
    layout();
}
void AdvancedWindow::onScroll(HWND control) {
    const int id=GetDlgCtrlID(control);
    if(id<monitorStart||!monitors_)return;
    const unsigned index=static_cast<unsigned>((id-monitorStart)/10);
    const unsigned field=static_cast<unsigned>((id-monitorStart)%10);
    if(index>=monitors_->size())return;
    const unsigned value=static_cast<unsigned>(SendMessageW(control,TBM_GETPOS,0,0));
    if(field==0 && brightnessChanged)brightnessChanged(index,value);
    else if(field==1 && contrastChanged)contrastChanged(index,value);
}
bool AdvancedWindow::saveInput() {
    if(!settings_)return false;
    Settings next=*settings_;
    next.scheduleEnabled=SendMessageW(scheduleCheckbox_,BM_GETCHECK,0,0)==BST_CHECKED;
    next.reduceBrightness=SendMessageW(brightnessCheckbox_,BM_GETCHECK,0,0)==BST_CHECKED;
    next.automaticLocation=SendMessageW(automaticLocation_,BM_GETCHECK,0,0)==BST_CHECKED;
    next.scheduleMode=SendMessageW(mode_,CB_GETCURSEL,0,0)==1?
        ScheduleMode::Solar:ScheduleMode::Fixed;
    if(!parseMinute(read(lightTime_),next.lightMinute)||
       !parseMinute(read(darkTime_),next.darkMinute)) {
        SetWindowTextW(status_,L"Invalid time: enter HH:MM (24-hour clock).");
        return false;
    }
    const auto latitudeText=read(latitude_);
    const auto longitudeText=read(longitude_);
    if (!latitudeText.empty() && !longitudeText.empty()) {
        if(!readDecimal(latitude_,next.latitude,90)||
           !readDecimal(longitude_,next.longitude,180)) {
            SetWindowTextW(status_,L"Latitude must be -90..90 and longitude -180..180.");
            return false;
        }
        next.locationConfigured=true;
    } else next.locationConfigured=false;
    next.locationLabel=read(label_);
    if(settingsChanged)settingsChanged(next);
    SetWindowTextW(status_,L"Schedule and location settings saved.");
    return true;
}
void AdvancedWindow::copyDiagnostics() {
    std::wstring report=L"DisplayPilot diagnostics\r\n";
    report+=L"Process: native Win32\r\n";
    if (monitors_) {
        report+=L"Monitor count: "+std::to_wstring(monitors_->size())+L"\r\n";
        for(const auto& m:*monitors_) {
            report+=m.name+L" | "+m.deviceName+L" | brightness: ";
            report+=m.brightnessAvailable?std::to_wstring(m.brightness)+L"%":L"unavailable";
            report+=L" | contrast: ";
            report+=m.contrastAvailable?std::to_wstring(m.contrast)+L"%":L"unavailable";
            report+=L" | rotation: "+rotationLabel(m.rotation)+L"\r\n";
        }
    }
    if(settings_) {
        report+=L"Scheduled automation: ";
        report+=settings_->scheduleEnabled?L"enabled":L"disabled";
        report+=L"\r\nSchedule mode: ";
        report+=settings_->scheduleMode==ScheduleMode::Solar?L"sunrise/sunset":L"fixed";
        report+=L"\r\nCoordinates available: ";
        report+=settings_->locationConfigured?L"yes":L"no";
        report+=L"\r\nAutomatic location: ";
        report+=settings_->automaticLocation?L"enabled":L"disabled";
        report+=L"\r\nScheduled brightness reduction: ";
        report+=settings_->reduceBrightness?L"enabled":L"disabled";
        SYSTEMTIME now{};
        GetLocalTime(&now);
        const auto next=evaluateSchedule(*settings_,now);
        report+=L"\r\nSchedule evaluation: ";
        report+=next.valid?(next.dark?L"dark":L"light"):L"unavailable";
        if(next.valid) {
            report+=L"\r\nNext schedule evaluation in milliseconds: ";
            report+=std::to_wstring(next.millisecondsUntilChange);
        }
        report+=L"\r\n";
    }
    if(!OpenClipboard(window_))return;
    EmptyClipboard();
    HGLOBAL buffer=GlobalAlloc(GMEM_MOVEABLE,(report.size()+1)*sizeof(wchar_t));
    if(buffer) {
        void* data=GlobalLock(buffer);
        if(data) {
            memcpy(data,report.c_str(),(report.size()+1)*sizeof(wchar_t));
            GlobalUnlock(buffer);
            if(SetClipboardData(CF_UNICODETEXT,buffer))buffer=nullptr;
        }
        if(buffer)GlobalFree(buffer);
    }
    CloseClipboard();
    SetWindowTextW(status_,L"Diagnostics copied to clipboard (coordinates excluded).");
}
void AdvancedWindow::onCommand(WORD id,WORD notification) {
    if(id==startupId && notification==BN_CLICKED) {
        setStartup(SendMessageW(startup_,BM_GETCHECK,0,0)==BST_CHECKED);
        SetWindowTextW(status_,L"Windows sign-in preference updated.");
    } else if(id==saveId && notification==BN_CLICKED) {
        saveInput();
    } else if(id==updateLocationId && notification==BN_CLICKED) {
        if (locationUpdateRequested) {
            const bool success = locationUpdateRequested();
            SetWindowTextW(status_, success ?
                L"Location update received. Processing valid coordinates." :
                L"Location unavailable or permission denied. Saved coordinates unchanged.");
        }
    } else if(id==diagnosticsId && notification==BN_CLICKED) {
        copyDiagnostics();
    } else if(id==themeLightId && notification==BN_CLICKED && themeChanged) {
        themeChanged(false);
    } else if(id==themeDarkId && notification==BN_CLICKED && themeChanged) {
        themeChanged(true);
    } else if(id>=monitorStart && monitors_ && notification==BN_CLICKED) {
        const unsigned index=(id-monitorStart)/10;
        const unsigned op=(id-monitorStart)%10;
        if(index>=rows_.size())return;
        auto& row=rows_[index];
        if(op==3 && row.color && colorChanged) {
            const LRESULT choice=SendMessageW(row.color,CB_GETCURSEL,0,0);
            if(choice!=CB_ERR)
                colorChanged(index,static_cast<unsigned>(SendMessageW(
                    row.color,CB_GETITEMDATA,choice,0)));
        } else if(op==5 && row.rotation && rotationChanged) {
            const LRESULT choice=SendMessageW(row.rotation,CB_GETCURSEL,0,0);
            if(choice!=CB_ERR)
                rotationChanged(index,static_cast<unsigned>(SendMessageW(
                    row.rotation,CB_GETITEMDATA,choice,0)));
        }
    }
}
LRESULT CALLBACK AdvancedWindow::procedure(HWND hwnd,UINT msg,WPARAM w,LPARAM l) {
    auto* self=reinterpret_cast<AdvancedWindow*>(GetWindowLongPtrW(hwnd,GWLP_USERDATA));
    if(msg==WM_NCCREATE) {
        self=static_cast<AdvancedWindow*>(reinterpret_cast<CREATESTRUCTW*>(l)->lpCreateParams);
        SetWindowLongPtrW(hwnd,GWLP_USERDATA,reinterpret_cast<LONG_PTR>(self));
    }
    if(!self)return DefWindowProcW(hwnd,msg,w,l);
    switch(msg) {
    case WM_CTLCOLORSTATIC:
        // The Advanced window uses COLOR_WINDOW; native STATIC labels otherwise
        // receive a COLOR_BTNFACE background, leaving gray rectangles behind text.
        SetBkMode(reinterpret_cast<HDC>(w), TRANSPARENT);
        SetTextColor(reinterpret_cast<HDC>(w), GetSysColor(COLOR_WINDOWTEXT));
        return reinterpret_cast<LRESULT>(GetSysColorBrush(COLOR_WINDOW));
    case WM_DRAWITEM: {
        const auto* item = reinterpret_cast<const DRAWITEMSTRUCT*>(l);
        if (item && item->CtlID == brandIconId && self->appIcon_) {
            FillRect(item->hDC, &item->rcItem,
                     reinterpret_cast<HBRUSH>(COLOR_WINDOW + 1));
            const int size = self->px(32);
            DrawIconEx(item->hDC, item->rcItem.left, item->rcItem.top,
                       self->appIcon_, size, size, 0, nullptr, DI_NORMAL);
            return TRUE;
        }
        return DefWindowProcW(hwnd, msg, w, l);
    }
    case WM_DPICHANGED: {
        const RECT* suggested = reinterpret_cast<const RECT*>(l);
        if (suggested)
            SetWindowPos(hwnd, nullptr, suggested->left, suggested->top,
                suggested->right - suggested->left, suggested->bottom - suggested->top,
                SWP_NOZORDER | SWP_NOACTIVATE);
        self->dpi_ = HIWORD(w);
        self->updateFonts();
        self->layout();
        return 0;
    }
    case WM_COMMAND:self->onCommand(LOWORD(w),HIWORD(w));return 0;
    case WM_HSCROLL:
        if(l)self->onScroll(reinterpret_cast<HWND>(l));
        return 0;
    case WM_VSCROLL:
        if(!l) {
            switch(LOWORD(w)) {
            case SB_LINEUP:self->scrollBy(-28);break;
            case SB_LINEDOWN:self->scrollBy(28);break;
            case SB_PAGEUP:self->scrollBy(-250);break;
            case SB_PAGEDOWN:self->scrollBy(250);break;
            case SB_THUMBPOSITION:
            case SB_THUMBTRACK:self->scrollBy(HIWORD(w)-self->scroll_);break;
            }
        }
        return 0;
    case WM_MOUSEWHEEL:self->scrollBy(-GET_WHEEL_DELTA_WPARAM(w)/WHEEL_DELTA*self->px(48));return 0;
    case WM_SIZE:self->layout();return 0;
    case WM_GETMINMAXINFO: {
        auto* limits = reinterpret_cast<MINMAXINFO*>(l);
        limits->ptMinTrackSize.x = self->px(680);
        limits->ptMinTrackSize.y = self->px(430);
        return 0;
    }
    case WM_CLOSE:self->hide();return 0;
    case WM_DESTROY:self->window_=nullptr;return 0;
    default:return DefWindowProcW(hwnd,msg,w,l);
    }
}
}
