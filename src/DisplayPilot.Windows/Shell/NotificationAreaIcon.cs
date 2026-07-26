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
    private const uint AdvancedCommand = 1;
    private const uint ExitCommand = 2;
    private const uint DefaultApplicationIcon = 32512;
    private const uint ImageIcon = 1;
    private const uint LoadFromFile = 0x00000010;
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
    private long _callbackCount;
    private uint _lastNotificationCode;
    private DateTimeOffset? _lastCallbackUtc;
    private uint _lastMenuCommand;
    private readonly Queue<uint> _recentNotificationCodes = new();
    private long _contextMenuRequestCount;
    private string _lastContextMenuStage = "None";
    private int _lastContextMenuError;

    public NotificationAreaIcon(nint window, string? iconPath = null)
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
            var loadedIcon = LoadNotificationIcon(iconPath);
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

    public static uint ActivationGuardDurationMilliseconds =>
        Math.Max(GetDoubleClickTime(), 1u);

    public bool TryBringWindowToForeground()
    {
        return !_disposed && SetForegroundWindow(_ownerWindow);
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

    private (nint Handle, bool OwnsHandle) LoadNotificationIcon(string? iconPath)
    {
        if (!string.IsNullOrWhiteSpace(iconPath))
        {
            var fileIcon = LoadImage(
                0,
                iconPath,
                ImageIcon,
                0,
                0,
                LoadFromFile | LoadDefaultSize);
            if (fileIcon != 0)
            {
                return (fileIcon, true);
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

    [LibraryImport("user32.dll", EntryPoint = "LoadImageW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial nint LoadImage(
        nint instance,
        string name,
        uint type,
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

}

public enum NotificationAreaMenuCommand : uint
{
    Advanced = 1,
    Exit = 2,
}

public readonly record struct NotificationAreaIconDiagnostics(
    long CallbackCount,
    uint LastNotificationCode,
    DateTimeOffset? LastCallbackUtc,
    uint LastMenuCommand,
    long ContextMenuRequestCount,
    string LastContextMenuStage,
    int LastContextMenuError,
    IReadOnlyList<uint> RecentNotificationCodes);
