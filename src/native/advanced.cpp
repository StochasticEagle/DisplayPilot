#include "advanced.hpp"
#include <commctrl.h>
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
constexpr int diagnosticsId=120;
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
}

bool AdvancedWindow::create(HINSTANCE instance) {
    instance_ = instance;
    INITCOMMONCONTROLSEX controls{};
    controls.dwSize = sizeof(controls);
    controls.dwICC = ICC_BAR_CLASSES | ICC_STANDARD_CLASSES;
    if (!InitCommonControlsEx(&controls)) return false;
    WNDCLASSEXW cls{};
    cls.cbSize=sizeof(cls);
    cls.hInstance=instance;
    cls.lpfnWndProc=procedure;
    cls.hCursor=LoadCursorW(nullptr,IDC_ARROW);
    cls.hbrBackground=reinterpret_cast<HBRUSH>(COLOR_WINDOW+1);
    cls.lpszClassName=L"DisplayPilotAdvanced";
    if (!RegisterClassExW(&cls) && GetLastError()!=ERROR_CLASS_ALREADY_EXISTS)
        return false;
    window_=CreateWindowExW(0,cls.lpszClassName,L"DisplayPilot - Advanced",
             WS_OVERLAPPEDWINDOW|WS_VSCROLL, CW_USEDEFAULT,CW_USEDEFAULT,
             860,690,nullptr,nullptr,instance,this);
    if (!window_) return false;
    HDC dc=GetDC(window_);
    const int size=dc ? -MulDiv(10,GetDeviceCaps(dc,LOGPIXELSY),72):-13;
    if (dc) ReleaseDC(window_,dc);
    font_=CreateFontW(size,0,0,0,FW_NORMAL,FALSE,FALSE,FALSE,DEFAULT_CHARSET,
          OUT_DEFAULT_PRECIS,CLIP_DEFAULT_PRECIS,CLEARTYPE_QUALITY,
          DEFAULT_PITCH|FF_SWISS,L"Segoe UI");
    return true;
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
    if (!monitors_ || !settings_) return;
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
    if (themesAvailable_) {
        add(L"BUTTON",L"Light mode",BS_PUSHBUTTON|WS_TABSTOP,themeLightId);
        add(L"BUTTON",L"Dark mode",BS_PUSHBUTTON|WS_TABSTOP,themeDarkId);
        scheduleCheckbox_=add(L"BUTTON",L"Enable automatic schedule",
                               BS_AUTOCHECKBOX|WS_TABSTOP,scheduleId);
        brightnessCheckbox_=add(L"BUTTON",L"Reduce brightness during dark hours",
                               BS_AUTOCHECKBOX|WS_TABSTOP,reduceId);
        mode_=add(L"COMBOBOX",L"",CBS_DROPDOWNLIST|WS_VSCROLL|WS_TABSTOP,modeId);
        SendMessageW(mode_,CB_ADDSTRING,0,reinterpret_cast<LPARAM>(L"Fixed times"));
        SendMessageW(mode_,CB_ADDSTRING,0,reinterpret_cast<LPARAM>(L"Sunrise / sunset"));
        SendMessageW(mode_,CB_SETCURSEL,
            settings_->scheduleMode==ScheduleMode::Solar?1:0,0);
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
        save_=add(L"BUTTON",L"Save schedule",BS_PUSHBUTTON|WS_TABSTOP,saveId);
        SendMessageW(scheduleCheckbox_,BM_SETCHECK,
                     settings_->scheduleEnabled?BST_CHECKED:BST_UNCHECKED,0);
        SendMessageW(brightnessCheckbox_,BM_SETCHECK,
                     settings_->reduceBrightness?BST_CHECKED:BST_UNCHECKED,0);
    }
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
    GetClientRect(window_,&bounds);
    const int width=bounds.right;
    const int height=bounds.bottom;
    const int left=22, column=170, field=std::max(190,width-250);
    const int full=std::max(320,width-50);
    int y=20-scroll_;
    auto place=[](HWND child,int x,int top,int w,int h) {
        if(child) MoveWindow(child,x,top,w,h,TRUE);
    };
    for (auto& row:rows_) {
        place(row.label,left,y,full,25);
        y+=32;
        int member=0;
        auto control=[&](HWND child,HWND apply) {
            if (!child) return;
            HWND label=member<static_cast<int>(row.members.size())?
                row.members[static_cast<size_t>(member++)]:nullptr;
            place(label,left,y+5,column-10,24);
            place(child,left+column,y,field-85,30);
            if(apply) place(apply,std::max(left+column+field-78,width-98),y,69,28);
            y+=38;
        };
        control(row.brightness,nullptr);
        control(row.contrast,nullptr);
        control(row.color,row.colorApply);
        control(row.rotation,row.rotationApply);
        y+=12;
    }
    if (themesAvailable_) {
        auto find=[this](int id) {
            return GetDlgItem(window_,id);
        };
        place(find(themeLightId),left,y,140,30);
        place(find(themeDarkId),left+155,y,140,30);
        y+=40;
        place(scheduleCheckbox_,left,y,290,26);
        place(brightnessCheckbox_,left+300,y,std::max(270,width-345),26);
        y+=34;
        place(mode_,left,y,200,100);
        place(lightTime_,left+213,y,78,26);
        place(darkTime_,left+302,y,78,26);
        y+=43;
        place(latitude_,left,y,118,26);
        place(longitude_,left+132,y,118,26);
        place(label_,left+266,y,std::max(145,width-325),26);
        y+=42;
        place(save_,left,y,180,31);
        y+=44;
    }
    place(startup_,left,y,310,29);
    place(diagnostics_,left+318,y,165,30);
    y+=45;
    place(status_,left,y,full,50);
    contentHeight_=y+scroll_+60;
    SCROLLINFO info{};
    info.cbSize=sizeof(info);
    info.fMask=SIF_RANGE|SIF_PAGE|SIF_POS;
    info.nMin=0;
    info.nMax=std::max(0,contentHeight_-1);
    info.nPage=static_cast<UINT>(height);
    info.nPos=scroll_;
    SetScrollInfo(window_,SB_VERT,&info,TRUE);
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
    if(!themesAvailable_||!settings_)return false;
    Settings next=*settings_;
    next.scheduleEnabled=SendMessageW(scheduleCheckbox_,BM_GETCHECK,0,0)==BST_CHECKED;
    next.reduceBrightness=SendMessageW(brightnessCheckbox_,BM_GETCHECK,0,0)==BST_CHECKED;
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
    if(next.scheduleEnabled && next.scheduleMode==ScheduleMode::Solar &&
       !next.locationConfigured) {
        SetWindowTextW(status_,L"Sunrise/sunset scheduling requires a valid location.");
        return false;
    }
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
    case WM_MOUSEWHEEL:self->scrollBy(-GET_WHEEL_DELTA_WPARAM(w)/WHEEL_DELTA*48);return 0;
    case WM_SIZE:self->layout();return 0;
    case WM_CLOSE:self->hide();return 0;
    case WM_DESTROY:self->window_=nullptr;return 0;
    default:return DefWindowProcW(hwnd,msg,w,l);
    }
}
}
