// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DisplayPilot.Windows.Scheduling;

/// <summary>
/// Wraps a one-shot Windows thread-pool timer that is armed with an absolute UTC due time.
/// </summary>
public sealed partial class WindowsBoundaryTimer : IDisposable
{
    private const uint CoalescingWindowMilliseconds = 1000;
    private readonly object _syncRoot = new();
    private GCHandle _selfHandle;
    private nint _timer;
    private bool _disposed;
    private bool _isArmed;
    private DateTimeOffset? _dueTime;

    public WindowsBoundaryTimer()
    {
        _selfHandle = GCHandle.Alloc(this);

        unsafe
        {
            _timer = CreateThreadpoolTimer(
                &TimerCallback,
                GCHandle.ToIntPtr(_selfHandle),
                0);
        }

        if (_timer == 0)
        {
            _selfHandle.Free();
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }

    public event EventHandler? Elapsed;

    public bool IsArmed
    {
        get
        {
            lock (_syncRoot)
            {
                return _isArmed;
            }
        }
    }

    public DateTimeOffset? DueTime
    {
        get
        {
            lock (_syncRoot)
            {
                return _dueTime;
            }
        }
    }

    public void Arm(DateTimeOffset dueTime)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var fileTime = dueTime.UtcDateTime.ToFileTimeUtc();
        var nativeDueTime = new NativeFileTime
        {
            LowDateTime = unchecked((uint)fileTime),
            HighDateTime = unchecked((uint)(fileTime >> 32)),
        };

        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            unsafe
            {
                SetThreadpoolTimer(
                    _timer,
                    &nativeDueTime,
                    periodMilliseconds: 0,
                    windowLengthMilliseconds: CoalescingWindowMilliseconds);
            }

            _dueTime = dueTime;
            _isArmed = true;
        }
    }

    public void Cancel()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            unsafe
            {
                SetThreadpoolTimer(
                    _timer,
                    dueTime: null,
                    periodMilliseconds: 0,
                    windowLengthMilliseconds: 0);
            }

            _dueTime = null;
            _isArmed = false;
        }
    }

    public void Dispose()
    {
        nint timer;

        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            timer = _timer;
            _timer = 0;
            _dueTime = null;
            _isArmed = false;
        }

        unsafe
        {
            SetThreadpoolTimer(
                timer,
                dueTime: null,
                periodMilliseconds: 0,
                windowLengthMilliseconds: 0);
        }

        WaitForThreadpoolTimerCallbacks(timer, cancelPendingCallbacks: true);
        CloseThreadpoolTimer(timer);
        _selfHandle.Free();
        GC.SuppressFinalize(this);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void TimerCallback(nint instance, nint context, nint timer)
    {
        _ = instance;
        _ = timer;

        var boundaryTimer = (WindowsBoundaryTimer?)GCHandle.FromIntPtr(context).Target;
        boundaryTimer?.OnElapsed();
    }

    private void OnElapsed()
    {
        EventHandler? handler;

        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _dueTime = null;
            _isArmed = false;
            handler = Elapsed;
        }

        handler?.Invoke(this, EventArgs.Empty);
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static unsafe partial nint CreateThreadpoolTimer(
        delegate* unmanaged[Stdcall]<nint, nint, nint, void> callback,
        nint context,
        nint callbackEnvironment);

    [LibraryImport("kernel32.dll")]
    private static unsafe partial void SetThreadpoolTimer(
        nint timer,
        NativeFileTime* dueTime,
        uint periodMilliseconds,
        uint windowLengthMilliseconds);

    [LibraryImport("kernel32.dll")]
    private static partial void WaitForThreadpoolTimerCallbacks(
        nint timer,
        [MarshalAs(UnmanagedType.Bool)] bool cancelPendingCallbacks);

    [LibraryImport("kernel32.dll")]
    private static partial void CloseThreadpoolTimer(nint timer);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeFileTime
    {
        internal uint LowDateTime;
        internal uint HighDateTime;
    }
}
