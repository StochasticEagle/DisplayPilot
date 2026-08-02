// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DisplayPilot.Windows.Shell;

public sealed partial class NotificationAreaIcon : IDisposable
{
    private const uint IconIdentifier = 1;
    private const uint CallbackMessage = 0x8000 + 42;
    private const uint NotifyIconAdd = 0x00000000;
    private const uint NotifyIconDelete = 0x00000002;
    private const uint NotifyIconSetVersion = 0x00000004;
    private const uint NotifyIconVersion4 = 4;
    private const uint NotifyIconMessage = 0x00000001;
    private const uint NotifyIconImage = 0x00000002;
    private const uint NotifyIconTip = 0x00000004;
    private const uint NotifyIconShowTip = 0x00000080;
    private const uint NotifySelect = 0x0400;
    private const uint NotifyKeySelect = 0x0401;
    private const uint ContextMenu = 0x007B;
    private const uint SessionChangeMessage = 0x02B1;
    private const uint RawInputMessage = 0x00FF;
    private const uint RawInputDataCommand = 0x10000003;
    private const uint RawInputTypeHid = 2;
    private const uint RawInputSink = 0x00000100;
    private const uint RawInputDeviceNotify = 0x00002000;
    private const ushort ConsumerUsagePage = 0x000C;
    private const ushort ConsumerControlUsage = 0x0001;
    private const ushort BrightnessIncrementUsage = 0x006F;
    private const ushort BrightnessDecrementUsage = 0x0070;
    private const uint ConsoleConnect = 0x1;
    private const uint ConsoleDisconnect = 0x2;
    private const uint RemoteConnect = 0x3;
    private const uint RemoteDisconnect = 0x4;
    private const uint SessionLogon = 0x5;
    private const uint SessionLogoff = 0x6;
    private const uint SessionLock = 0x7;
    private const uint SessionUnlock = 0x8;
    private const uint SessionDesktopReady = 0xF;
    private const uint NotifyForThisSession = 0;
    private const uint AdvancedCommand = 1;
    private const uint ExitCommand = 2;
    private const uint DefaultApplicationIcon = 32512;
    private const uint LoadDefaultSize = 0x00000040;
    private const uint ExtendedWindowStyleToolWindow = 0x00000080;
    private const uint WindowStylePopup = 0x80000000;
    private const uint NonClientCreate = 0x0081;
    private const uint NonClientDestroy = 0x0082;
    private const int NotificationHistoryLimit = 12;
    private const int WindowUserData = -21;
    private const string NotificationWindowClassName = "DisplayPilot.NotificationAreaWindow";

    private readonly object _syncRoot = new();
    private readonly nint _ownerWindow;
    private readonly nint _moduleHandle;
    private readonly nint _messageWindow;
    private readonly nint _iconHandle;
    private readonly bool _ownsIconHandle;
    private readonly uint _taskbarCreatedMessage;
    private GCHandle _selfHandle;
    private bool _windowClassRegistered;
    private bool _disposed;
    private bool _iconAdded;
    private bool _sessionNotificationsRegistered;
    private long _callbackCount;
    private uint _lastNotificationCode;
    private DateTimeOffset? _lastCallbackUtc;
    private uint _lastMenuCommand;
    private readonly Queue<uint> _recentNotificationCodes = new();
    private long _contextMenuRequestCount;
    private string _lastContextMenuStage = "None";
    private int _lastContextMenuError;
    private bool _rawBrightnessInputRegistered;
    private int _rawBrightnessInputError;
    private long _rawBrightnessInputCount;

    public NotificationAreaIcon(nint window, byte[]? iconData = null)
    {
        if (window == 0)
        {
            throw new ArgumentException("A valid window handle is required.", nameof(window));
        }

        _ownerWindow = window;
        _moduleHandle = GetModuleHandle(null);
        if (_moduleHandle == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        _taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");
        if (_taskbarCreatedMessage == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        RegisterNotificationWindowClass();
        _selfHandle = GCHandle.Alloc(this);
        _messageWindow = CreateWindowEx(
            ExtendedWindowStyleToolWindow,
            NotificationWindowClassName,
            "DisplayPilot.NotificationArea",
            WindowStylePopup,
            0,
            0,
            0,
            0,
            0,
            0,
            _moduleHandle,
            GCHandle.ToIntPtr(_selfHandle));
        if (_messageWindow == 0)
        {
            var error = Marshal.GetLastPInvokeError();
            _selfHandle.Free();
            UnregisterNotificationWindowClass();
            throw new Win32Exception(error);
        }

        try
        {
            _sessionNotificationsRegistered = WtsRegisterSessionNotification(
                _messageWindow,
                NotifyForThisSession);
            RegisterBrightnessRawInput();
            var loadedIcon = LoadNotificationIcon(iconData);
            _iconHandle = loadedIcon.Handle;
            _ownsIconHandle = loadedIcon.OwnsHandle;
            AddIcon();
        }
        catch (Win32Exception)
        {
            _ = DestroyWindow(_messageWindow);
            if (_ownsIconHandle && _iconHandle != 0)
            {
                _ = DestroyIcon(_iconHandle);
            }

            _selfHandle.Free();
            UnregisterNotificationWindowClass();
            throw;
        }
    }

    public event EventHandler? PrimaryInvoked;

    public event EventHandler? ContextMenuInvoked;

    public event EventHandler? AdvancedInvoked;

    public event EventHandler? ExitInvoked;

    public event EventHandler<bool>? SessionActivityChanged;

    public event EventHandler<BrightnessKeyEventArgs>? BrightnessKeyInvoked;

    public static uint ActivationGuardDurationMilliseconds =>
        Math.Max(GetDoubleClickTime(), 1u);

    public bool TryBringWindowToForeground()
    {
        return !_disposed && SetForegroundWindow(_ownerWindow);
    }

    public static bool TryGetCursorMonitorDeviceName(out string gdiDeviceName)
    {
        gdiDeviceName = GetCursorMonitorDeviceName();
        return !string.IsNullOrWhiteSpace(gdiDeviceName);
    }

    public void InvokeContextMenuCommand(NotificationAreaMenuCommand command)
    {
        if (_disposed)
        {
            return;
        }

        HandleMenuCommand((uint)command);
    }

    public NotificationAreaIconDiagnostics GetDiagnostics()
    {
        lock (_syncRoot)
        {
            return new NotificationAreaIconDiagnostics(
                _callbackCount,
                _lastNotificationCode,
                _lastCallbackUtc,
                _lastMenuCommand,
                _contextMenuRequestCount,
                _lastContextMenuStage,
                _lastContextMenuError,
                _rawBrightnessInputRegistered,
                _rawBrightnessInputError,
                _rawBrightnessInputCount,
                _recentNotificationCodes.ToArray());
        }
    }

    public bool TryGetBounds(out NotificationAreaBounds bounds)
    {
        var identifier = new NotifyIconIdentifier
        {
            CbSize = (uint)Marshal.SizeOf<NotifyIconIdentifier>(),
            Window = _messageWindow,
            IconIdentifier = IconIdentifier,
        };

        if (ShellNotifyIconGetRect(in identifier, out var nativeBounds) != 0)
        {
            bounds = default;
            return false;
        }

        bounds = new NotificationAreaBounds(
            nativeBounds.Left,
            nativeBounds.Top,
            nativeBounds.Right,
            nativeBounds.Bottom);
        return true;
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            DeleteIcon();
            if (_sessionNotificationsRegistered)
            {
                _ = WtsUnRegisterSessionNotification(_messageWindow);
                _sessionNotificationsRegistered = false;
            }

            _ = DestroyWindow(_messageWindow);
            if (_ownsIconHandle && _iconHandle != 0)
            {
                _ = DestroyIcon(_iconHandle);
            }

            _selfHandle.Free();
            UnregisterNotificationWindowClass();
        }

        GC.SuppressFinalize(this);
    }

    private void AddIcon()
    {
        unsafe
        {
            var data = CreateIconData();
            if (!ShellNotifyIcon(NotifyIconAdd, &data))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            _iconAdded = true;
            data.TimeoutOrVersion = NotifyIconVersion4;
            if (!ShellNotifyIcon(NotifyIconSetVersion, &data))
            {
                DeleteIcon();
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }
        }
    }

    private void DeleteIcon()
    {
        if (!_iconAdded)
        {
            return;
        }

        unsafe
        {
            var data = CreateIconData();
            _ = ShellNotifyIcon(NotifyIconDelete, &data);
        }

        _iconAdded = false;
    }

    private unsafe NotifyIconData CreateIconData()
    {
        var data = new NotifyIconData
        {
            CbSize = (uint)sizeof(NotifyIconData),
            Window = _messageWindow,
            IconIdentifier = IconIdentifier,
            Flags = NotifyIconMessage | NotifyIconImage | NotifyIconTip | NotifyIconShowTip,
            CallbackMessage = CallbackMessage,
            Icon = _iconHandle,
        };
        if (data.Icon == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        const string tooltip = "DisplayPilot";
        for (var index = 0; index < tooltip.Length; index++)
        {
            data.Tip[index] = tooltip[index];
        }

        data.Tip[tooltip.Length] = '\0';
        return data;
    }

    private unsafe (nint Handle, bool OwnsHandle) LoadNotificationIcon(byte[]? iconData)
    {
        if (TryGetLargestIconImage(iconData, out var imageOffset, out var imageLength))
        {
            fixed (byte* data = &iconData![imageOffset])
            {
                var resourceIcon = CreateIconFromResourceEx(
                    data,
                    (uint)imageLength,
                    true,
                    0x00030000,
                    0,
                    0,
                    LoadDefaultSize);
                if (resourceIcon != 0)
                {
                    return (resourceIcon, true);
                }
            }
        }

        var applicationIcon = LoadIcon(
            _moduleHandle,
            unchecked((nint)DefaultApplicationIcon));
        applicationIcon = applicationIcon != 0
            ? applicationIcon
            : LoadIcon(0, unchecked((nint)DefaultApplicationIcon));
        if (applicationIcon == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        return (applicationIcon, false);
    }

    private static bool TryGetLargestIconImage(
        byte[]? iconData,
        out int imageOffset,
        out int imageLength)
    {
        imageOffset = 0;
        imageLength = 0;
        if (iconData is null ||
            iconData.Length < 22 ||
            BitConverter.ToUInt16(iconData, 0) != 0 ||
            BitConverter.ToUInt16(iconData, 2) != 1)
        {
            return false;
        }

        var imageCount = BitConverter.ToUInt16(iconData, 4);
        var bestArea = -1;
        for (var index = 0; index < imageCount; index++)
        {
            var entryOffset = 6 + (index * 16);
            if (entryOffset + 16 > iconData.Length)
            {
                return false;
            }

            var width = iconData[entryOffset] == 0 ? 256 : iconData[entryOffset];
            var height = iconData[entryOffset + 1] == 0 ? 256 : iconData[entryOffset + 1];
            var candidateLength = checked((int)BitConverter.ToUInt32(iconData, entryOffset + 8));
            var candidateOffset = checked((int)BitConverter.ToUInt32(iconData, entryOffset + 12));
            if (candidateLength <= 0 ||
                candidateOffset < 0 ||
                candidateOffset > iconData.Length - candidateLength)
            {
                return false;
            }

            var area = width * height;
            if (area > bestArea)
            {
                bestArea = area;
                imageOffset = candidateOffset;
                imageLength = candidateLength;
            }
        }

        return bestArea >= 0;
    }

    private unsafe void RegisterNotificationWindowClass()
    {
        fixed (char* className = NotificationWindowClassName)
        {
            var windowClass = new WindowClassEx
            {
                CbSize = (uint)sizeof(WindowClassEx),
                WindowProcedure = (nint)(delegate* unmanaged[Stdcall]<nint, uint, nuint, nint, nint>)&NotificationWindowProcedure,
                Instance = _moduleHandle,
                ClassName = className,
            };
            if (RegisterClassEx(&windowClass) == 0)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }
        }

        _windowClassRegistered = true;
    }

    private void UnregisterNotificationWindowClass()
    {
        if (!_windowClassRegistered)
        {
            return;
        }

        _ = UnregisterClass(NotificationWindowClassName, _moduleHandle);
        _windowClassRegistered = false;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static nint NotificationWindowProcedure(
        nint window,
        uint message,
        nuint wParam,
        nint lParam)
    {
        var referenceData = message == NonClientCreate
            ? Marshal.ReadIntPtr(lParam)
            : GetWindowLongPtr(window, WindowUserData);
        if (message == NonClientCreate)
        {
            _ = SetWindowLongPtr(window, WindowUserData, referenceData);
        }

        if (referenceData == 0)
        {
            return DefWindowProc(window, message, wParam, lParam);
        }

        var handle = GCHandle.FromIntPtr(referenceData);
        if (handle.Target is not NotificationAreaIcon icon)
        {
            return DefWindowProc(window, message, wParam, lParam);
        }

        var result = icon.ProcessWindowMessage(window, message, wParam, lParam);
        if (message == NonClientDestroy)
        {
            _ = SetWindowLongPtr(window, WindowUserData, 0);
        }

        return result;
    }

    private nint ProcessWindowMessage(nint window, uint message, nuint wParam, nint lParam)
    {
        if (message == _taskbarCreatedMessage)
        {
            RestoreAfterExplorerRestart();
            return 0;
        }

        if (message == SessionChangeMessage)
        {
            var sessionEvent = unchecked((uint)wParam);
            if (sessionEvent is ConsoleDisconnect or RemoteDisconnect or SessionLogoff or SessionLock)
            {
                SessionActivityChanged?.Invoke(this, false);
            }
            else if (sessionEvent is ConsoleConnect or RemoteConnect or SessionLogon or SessionUnlock or SessionDesktopReady)
            {
                SessionActivityChanged?.Invoke(this, true);
            }

            return 0;
        }

        if (message == RawInputMessage)
        {
            ProcessRawInput(lParam);
            return DefWindowProc(window, message, wParam, lParam);
        }

        if (message != CallbackMessage)
        {
            return DefWindowProc(window, message, wParam, lParam);
        }

        var notification = unchecked((uint)(long)lParam) & 0xffff;
        RecordNotification(notification);
        if (notification is NotifySelect or NotifyKeySelect)
        {
            PrimaryInvoked?.Invoke(this, EventArgs.Empty);
        }
        else if (notification == ContextMenu)
        {
            RequestContextMenu();
        }

        return 0;
    }

    private void RegisterBrightnessRawInput()
    {
        var device = new RawInputDevice
        {
            UsagePage = ConsumerUsagePage,
            Usage = ConsumerControlUsage,
            Flags = RawInputSink | RawInputDeviceNotify,
            TargetWindow = _messageWindow,
        };
        _rawBrightnessInputRegistered = RegisterRawInputDevices(
            in device,
            1,
            (uint)Marshal.SizeOf<RawInputDevice>());
        _rawBrightnessInputError = _rawBrightnessInputRegistered
            ? 0
            : Marshal.GetLastPInvokeError();
    }

    private void ProcessRawInput(nint rawInputHandle)
    {
        var headerSize = (uint)Marshal.SizeOf<RawInputHeader>();
        uint bufferSize = 0;
        if (GetRawInputData(
                rawInputHandle,
                RawInputDataCommand,
                0,
                ref bufferSize,
                headerSize) != 0 || bufferSize < headerSize + 8)
        {
            return;
        }

        var buffer = Marshal.AllocHGlobal(checked((int)bufferSize));
        try
        {
            var copiedSize = bufferSize;
            if (GetRawInputData(
                    rawInputHandle,
                    RawInputDataCommand,
                    buffer,
                    ref copiedSize,
                    headerSize) != copiedSize)
            {
                return;
            }

            var header = Marshal.PtrToStructure<RawInputHeader>(buffer);
            if (header.Type != RawInputTypeHid)
            {
                return;
            }

            var hidOffset = checked((int)headerSize);
            var reportSize = Marshal.ReadInt32(buffer, hidOffset);
            var reportCount = Marshal.ReadInt32(buffer, hidOffset + 4);
            if (reportSize <= 0 || reportCount <= 0)
            {
                return;
            }

            var reportBytes = checked(reportSize * reportCount);
            if ((uint)(hidOffset + 8 + reportBytes) > copiedSize)
            {
                return;
            }

            var reports = new byte[reportBytes];
            Marshal.Copy(buffer + hidOffset + 8, reports, 0, reports.Length);
            for (var reportIndex = 0; reportIndex < reportCount; reportIndex++)
            {
                var reportStart = reportIndex * reportSize;
                for (var offset = 0; offset + 1 < reportSize; offset++)
                {
                    var usage = (ushort)(reports[reportStart + offset] |
                        (reports[reportStart + offset + 1] << 8));
                    if (usage is BrightnessIncrementUsage or BrightnessDecrementUsage)
                    {
                        _rawBrightnessInputCount++;
                        BrightnessKeyInvoked?.Invoke(
                            this,
                            new BrightnessKeyEventArgs(
                                usage == BrightnessIncrementUsage
                                    ? BrightnessKeyDirection.Increase
                                    : BrightnessKeyDirection.Decrease,
                                GetCursorMonitorDeviceName()));
                        return;
                    }
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static unsafe string GetCursorMonitorDeviceName()
    {
        if (!GetCursorPos(out var point))
        {
            return string.Empty;
        }

        var monitor = MonitorFromPoint(point, 2);
        if (monitor == 0)
        {
            return string.Empty;
        }

        var info = new MonitorInfoEx
        {
            Size = (uint)sizeof(MonitorInfoEx),
        };
        if (!GetMonitorInfo(monitor, ref info))
        {
            return string.Empty;
        }

        char* device = info.Device;
        return new string(device).TrimEnd('\0');
    }

    private void RestoreAfterExplorerRestart()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _iconAdded = false;
            AddIcon();
        }
    }

    private void RequestContextMenu()
    {
        RecordContextMenuStage("WinUI menu requested", requestStarted: true);
        ContextMenuInvoked?.Invoke(this, EventArgs.Empty);
    }

    private void HandleMenuCommand(uint command)
    {
        RecordMenuCommand(command);
        RecordContextMenuStage("Command selected");
        switch (command)
        {
            case AdvancedCommand:
                AdvancedInvoked?.Invoke(this, EventArgs.Empty);
                break;
            case ExitCommand:
                ExitInvoked?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    private void RecordNotification(uint notificationCode)
    {
        lock (_syncRoot)
        {
            _callbackCount++;
            _lastNotificationCode = notificationCode;
            _lastCallbackUtc = DateTimeOffset.UtcNow;
            _recentNotificationCodes.Enqueue(notificationCode);
            while (_recentNotificationCodes.Count > NotificationHistoryLimit)
            {
                _ = _recentNotificationCodes.Dequeue();
            }
        }
    }

    private void RecordMenuCommand(uint command)
    {
        lock (_syncRoot)
        {
            _lastMenuCommand = command;
        }
    }

    private void RecordContextMenuStage(
        string stage,
        int errorCode = 0,
        bool requestStarted = false)
    {
        lock (_syncRoot)
        {
            if (requestStarted)
            {
                _contextMenuRequestCount++;
            }

            _lastContextMenuStage = stage;
            _lastContextMenuError = errorCode;
        }
    }

    [LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool ShellNotifyIcon(uint message, NotifyIconData* data);

    [LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconGetRect")]
    private static partial int ShellNotifyIconGetRect(
        in NotifyIconIdentifier identifier,
        out NativeRect iconLocation);

    [LibraryImport("user32.dll", EntryPoint = "LoadIconW")]
    private static partial nint LoadIcon(nint instance, nint iconName);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static unsafe partial nint CreateIconFromResourceEx(
        byte* resourceBits,
        uint resourceSize,
        [MarshalAs(UnmanagedType.Bool)] bool icon,
        uint version,
        int width,
        int height,
        uint flags);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyIcon(nint icon);

    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint RegisterWindowMessage(string message);

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial nint GetModuleHandle(string? moduleName);

    [LibraryImport("user32.dll", EntryPoint = "RegisterClassExW", SetLastError = true)]
    private static unsafe partial ushort RegisterClassEx(WindowClassEx* windowClass);

    [LibraryImport("user32.dll", EntryPoint = "UnregisterClassW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterClass(string className, nint instance);

    [LibraryImport("user32.dll", EntryPoint = "CreateWindowExW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial nint CreateWindowEx(
        uint extendedStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        nint parent,
        nint menu,
        nint instance,
        nint parameter);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyWindow(nint window);

    [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
    private static partial nint DefWindowProc(nint window, uint message, nuint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static partial nint GetWindowLongPtr(nint window, int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static partial nint SetWindowLongPtr(nint window, int index, nint newValue);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(nint window);

    [LibraryImport("user32.dll")]
    private static partial uint GetDoubleClickTime();

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out NativePoint point);

    [LibraryImport("user32.dll")]
    private static partial nint MonitorFromPoint(NativePoint point, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfo(nint monitor, ref MonitorInfoEx monitorInfo);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterRawInputDevices(
        in RawInputDevice devices,
        uint deviceCount,
        uint structureSize);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial uint GetRawInputData(
        nint rawInput,
        uint command,
        nint data,
        ref uint size,
        uint headerSize);

    [LibraryImport("wtsapi32.dll", EntryPoint = "WTSRegisterSessionNotification", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool WtsRegisterSessionNotification(nint window, uint flags);

    [LibraryImport("wtsapi32.dll", EntryPoint = "WTSUnRegisterSessionNotification")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool WtsUnRegisterSessionNotification(nint window);

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct WindowClassEx
    {
        internal uint CbSize;
        internal uint Style;
        internal nint WindowProcedure;
        internal int ClassExtraBytes;
        internal int WindowExtraBytes;
        internal nint Instance;
        internal nint Icon;
        internal nint Cursor;
        internal nint BackgroundBrush;
        internal char* MenuName;
        internal char* ClassName;
        internal nint SmallIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NotifyIconIdentifier
    {
        internal uint CbSize;
        internal nint Window;
        internal uint IconIdentifier;
        internal Guid ItemGuid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct NotifyIconData
    {
        internal uint CbSize;
        internal nint Window;
        internal uint IconIdentifier;
        internal uint Flags;
        internal uint CallbackMessage;
        internal nint Icon;
        internal fixed char Tip[128];
        internal uint State;
        internal uint StateMask;
        internal fixed char Info[256];
        internal uint TimeoutOrVersion;
        internal fixed char InfoTitle[64];
        internal uint InfoFlags;
        internal Guid ItemGuid;
        internal nint BalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        internal int X;
        internal int Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private unsafe struct MonitorInfoEx
    {
        internal uint Size;
        internal NativeRect Monitor;
        internal NativeRect Work;
        internal uint Flags;
        internal fixed char Device[32];
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDevice
    {
        internal ushort UsagePage;
        internal ushort Usage;
        internal uint Flags;
        internal nint TargetWindow;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputHeader
    {
        internal uint Type;
        internal uint Size;
        internal nint Device;
        internal nuint WParam;
    }

}

public enum NotificationAreaMenuCommand : uint
{
    Advanced = 1,
    Exit = 2,
}

public enum BrightnessKeyDirection
{
    Decrease = -1,
    Increase = 1,
}

public sealed class BrightnessKeyEventArgs(
    BrightnessKeyDirection direction,
    string gdiDeviceName) : EventArgs
{
    public BrightnessKeyDirection Direction { get; } = direction;

    public string GdiDeviceName { get; } = gdiDeviceName;
}

public readonly record struct NotificationAreaIconDiagnostics(
    long CallbackCount,
    uint LastNotificationCode,
    DateTimeOffset? LastCallbackUtc,
    uint LastMenuCommand,
    long ContextMenuRequestCount,
    string LastContextMenuStage,
    int LastContextMenuError,
    bool RawBrightnessInputRegistered,
    int RawBrightnessInputError,
    long RawBrightnessInputCount,
    IReadOnlyList<uint> RecentNotificationCodes);
