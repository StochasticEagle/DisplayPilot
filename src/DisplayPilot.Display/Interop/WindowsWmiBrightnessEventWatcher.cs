// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using System.Management;
using System.Runtime.InteropServices;
using DisplayPilot.Display.Wmi;

namespace DisplayPilot.Display.Interop;

/// <summary>
/// Observes Windows changes to an integrated panel's brightness. Windows emits
/// these events after a standardized HID brightness key changes the panel.
/// </summary>
public sealed class WindowsWmiBrightnessEventWatcher : IDisposable
{
    private const string Scope = @"root\WMI";
    private const string Query = "SELECT Active, Brightness, InstanceName FROM WmiMonitorBrightnessEvent";
    private ManagementEventWatcher? _watcher;
    private bool _disposed;

    public event EventHandler<WmiBrightnessChangedEventArgs>? BrightnessChanged;

    public bool IsActive => _watcher is not null;

    public int ErrorCode { get; private set; }

    public string? ErrorMessage { get; private set; }

    public bool TryStart()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_watcher is not null)
        {
            return true;
        }

        ManagementEventWatcher? candidate = null;
        try
        {
            candidate = new ManagementEventWatcher(Scope, Query);
            candidate.EventArrived += Watcher_EventArrived;
            candidate.Start();
            _watcher = candidate;
            ErrorCode = 0;
            ErrorMessage = null;
            return true;
        }
        catch (ManagementException exception)
        {
            RecordFailure(exception.HResult, exception.Message);
        }
        catch (COMException exception)
        {
            RecordFailure(exception.HResult, exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            RecordFailure(exception.HResult, exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            RecordFailure(exception.HResult, exception.Message);
        }
        finally
        {
            if (_watcher is null && candidate is not null)
            {
                candidate.EventArrived -= Watcher_EventArrived;
                candidate.Dispose();
            }
        }

        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_watcher is not null)
        {
            _watcher.EventArrived -= Watcher_EventArrived;
            try
            {
                _watcher.Stop();
            }
            catch (ManagementException)
            {
                // The provider may already have stopped during shutdown.
            }

            _watcher.Dispose();
            _watcher = null;
        }

        GC.SuppressFinalize(this);
    }

    private void Watcher_EventArrived(object sender, EventArrivedEventArgs args)
    {
        var value = args.NewEvent;
        BrightnessChanged?.Invoke(this, new WmiBrightnessChangedEventArgs(
            value["InstanceName"] as string ?? string.Empty,
            value["Brightness"] is byte brightness ? brightness : (byte)0,
            value["Active"] is bool active && active));
    }

    private void RecordFailure(int errorCode, string message)
    {
        ErrorCode = errorCode;
        ErrorMessage = message;
        _watcher = null;
    }
}
