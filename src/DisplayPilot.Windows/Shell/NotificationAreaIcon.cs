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
    private const uint NotifyIconSetFocus = 0x00000003;
    private const uint NotifyIconSetVersion = 0x00000004;
    private const uint NotifyIconVersion4 = 4;
    private const uint NotifyIconMessage = 0x00000001;
    private const uint NotifyIconImage = 0x00000002;
    private const uint NotifyIconTip = 0x00000004;
    private const uint NotifyIconShowTip = 0x00000080;
    private const uint NotifySelect = 0x0400;
    private const uint NotifyKeySelect = 0x0401;
    private const uint ContextMenu = 0x007B;
    private const uint MenuString = 0x00000000;
    private const uint MenuSeparator = 0x00000800;
    private const uint TrackRightButton = 0x00000002;
    private const uint TrackReturnCommand = 0x00000100;
    private const uint TrackNoNotify = 0x00000080;
    private const uint OpenCommand = 1;
    private const uint AdvancedCommand = 2;
    private const uint ExitCommand = 3;
    private const uint DefaultApplicationIcon = 32512;
    private const uint ExtendedWindowStyleToolWindow = 0x00000080;
    private const uint WindowStylePopup = 0x80000000;
    private const nuint SubclassIdentifier = 0x44504E41;

    private readonly object _syncRoot = new();
    private readonly nint _ownerWindow;
    private readonly nint _messageWindow;
    private readonly uint _taskbarCreatedMessage;
    private GCHandle _selfHandle;
    private bool _disposed;
    private bool _iconAdded;
    private long _callbackCount;
    private uint _lastNotificationCode;
    private DateTimeOffset? _lastCallbackUtc;
    private uint _lastMenuCommand;

    public NotificationAreaIcon(nint window)
    {
        if (window == 0)
        {
            throw new ArgumentException("A valid window handle is required.", nameof(window));
        }

        _ownerWindow = window;
        _taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");
        if (_taskbarCreatedMessage == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        _messageWindow = CreateWindowEx(
            ExtendedWindowStyleToolWindow,
            "STATIC",
            "DisplayPilot.NotificationArea",
            WindowStylePopup,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0);
        if (_messageWindow == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        _selfHandle = GCHandle.Alloc(this);

        unsafe
        {
            if (!SetWindowSubclass(
                    _messageWindow,
                    &SubclassProcedure,
                    SubclassIdentifier,
                    unchecked((nuint)GCHandle.ToIntPtr(_selfHandle))))
            {
                var error = Marshal.GetLastPInvokeError();
                _selfHandle.Free();
                _ = DestroyWindow(_messageWindow);
                throw new Win32Exception(error);
            }
        }

        try
        {
            AddIcon();
        }
        catch (Win32Exception)
        {
            unsafe
            {
                _ = RemoveWindowSubclass(_messageWindow, &SubclassProcedure, SubclassIdentifier);
            }

            _selfHandle.Free();
            _ = DestroyWindow(_messageWindow);
            throw;
        }
    }

    public event EventHandler? PrimaryInvoked;

    public event EventHandler? OpenInvoked;

    public event EventHandler? AdvancedInvoked;

    public event EventHandler? ExitInvoked;

    public static uint ActivationGuardDurationMilliseconds =>
        Math.Max(GetDoubleClickTime(), 1u);

    public bool TryBringWindowToForeground()
    {
        return !_disposed && SetForegroundWindow(_ownerWindow);
    }

    public NotificationAreaIconDiagnostics GetDiagnostics()
    {
        lock (_syncRoot)
        {
            return new NotificationAreaIconDiagnostics(
                _callbackCount,
                _lastNotificationCode,
                _lastCallbackUtc,
                _lastMenuCommand);
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

            unsafe
            {
                _ = RemoveWindowSubclass(_messageWindow, &SubclassProcedure, SubclassIdentifier);
            }

            _selfHandle.Free();
            _ = DestroyWindow(_messageWindow);
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
            Icon = LoadIcon(0, unchecked((nint)DefaultApplicationIcon)),
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

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static nint SubclassProcedure(
        nint window,
        uint message,
        nuint wParam,
        nint lParam,
        nuint subclassIdentifier,
        nuint referenceData)
    {
        _ = subclassIdentifier;
        var handle = GCHandle.FromIntPtr(unchecked((nint)referenceData));
        if (handle.Target is not NotificationAreaIcon icon)
        {
            return DefSubclassProc(window, message, wParam, lParam);
        }

        if (message == icon._taskbarCreatedMessage)
        {
            icon.RestoreAfterExplorerRestart();
            return 0;
        }

        if (message != CallbackMessage)
        {
            return DefSubclassProc(window, message, wParam, lParam);
        }

        var notification = unchecked((uint)(long)lParam) & 0xffff;
        icon.RecordNotification(notification);
        if (notification is NotifySelect or NotifyKeySelect)
        {
            icon.PrimaryInvoked?.Invoke(icon, EventArgs.Empty);
        }
        else if (notification == ContextMenu)
        {
            icon.ShowContextMenu(wParam);
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

    private void ShowContextMenu(nuint packedCoordinates)
    {
        var menu = CreatePopupMenu();
        if (menu == 0)
        {
            return;
        }

        uint command = 0;
        try
        {
            _ = AppendMenu(menu, MenuString, OpenCommand, "Open DisplayPilot");
            _ = AppendMenu(menu, MenuString, AdvancedCommand, "Advanced");
            _ = AppendMenu(menu, MenuSeparator, 0, string.Empty);
            _ = AppendMenu(menu, MenuString, ExitCommand, "Exit");
            _ = SetForegroundWindow(_ownerWindow);

            var x = (int)unchecked((short)((ulong)packedCoordinates & 0xffff));
            var y = (int)unchecked((short)(((ulong)packedCoordinates >> 16) & 0xffff));
            if (x == 0 && y == 0 && GetCursorPosition(out var cursor))
            {
                x = cursor.X;
                y = cursor.Y;
            }

            command = TrackPopupMenu(
                menu,
                TrackRightButton | TrackReturnCommand | TrackNoNotify,
                x,
                y,
                0,
                _ownerWindow,
                0);
            RecordMenuCommand(command);
            switch (command)
            {
                case OpenCommand:
                    OpenInvoked?.Invoke(this, EventArgs.Empty);
                    break;
                case AdvancedCommand:
                    AdvancedInvoked?.Invoke(this, EventArgs.Empty);
                    break;
                case ExitCommand:
                    ExitInvoked?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }
        finally
        {
            _ = DestroyMenu(menu);
            if (command == 0)
            {
                SetFocusToNotificationArea();
            }
        }
    }

    private void SetFocusToNotificationArea()
    {
        if (_disposed)
        {
            return;
        }

        unsafe
        {
            var data = CreateIconData();
            _ = ShellNotifyIcon(NotifyIconSetFocus, &data);
        }
    }

    private void RecordNotification(uint notificationCode)
    {
        lock (_syncRoot)
        {
            _callbackCount++;
            _lastNotificationCode = notificationCode;
            _lastCallbackUtc = DateTimeOffset.UtcNow;
        }
    }

    private void RecordMenuCommand(uint command)
    {
        lock (_syncRoot)
        {
            _lastMenuCommand = command;
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

    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint RegisterWindowMessage(string message);

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

    [LibraryImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool SetWindowSubclass(
        nint window,
        delegate* unmanaged[Stdcall]<nint, uint, nuint, nint, nuint, nuint, nint> subclassProcedure,
        nuint subclassIdentifier,
        nuint referenceData);

    [LibraryImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool RemoveWindowSubclass(
        nint window,
        delegate* unmanaged[Stdcall]<nint, uint, nuint, nint, nuint, nuint, nint> subclassProcedure,
        nuint subclassIdentifier);

    [LibraryImport("comctl32.dll")]
    private static partial nint DefSubclassProc(nint window, uint message, nuint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    private static partial nint CreatePopupMenu();

    [LibraryImport("user32.dll", EntryPoint = "AppendMenuW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AppendMenu(nint menu, uint flags, nuint identifier, string text);

    [LibraryImport("user32.dll", EntryPoint = "TrackPopupMenuEx")]
    private static partial uint TrackPopupMenu(
        nint menu,
        uint flags,
        int x,
        int y,
        int reserved,
        nint window,
        nint parameters);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyMenu(nint menu);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(nint window);

    [LibraryImport("user32.dll")]
    private static partial uint GetDoubleClickTime();

    [LibraryImport("user32.dll", EntryPoint = "GetCursorPos")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPosition(out NativePoint point);

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
}

public readonly record struct NotificationAreaIconDiagnostics(
    long CallbackCount,
    uint LastNotificationCode,
    DateTimeOffset? LastCallbackUtc,
    uint LastMenuCommand);
