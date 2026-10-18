// SPDX-License-Identifier: Unlicense
using WmiLight;
using DisplayPilot.Display.Wmi;

namespace DisplayPilot.Display.Interop;

/// <summary>
/// Receives native WMI brightness-key change notifications. The WMI connection
/// and subscription remain alive only while the watcher is active.
/// </summary>
public sealed class WindowsWmiBrightnessEventWatcher : IDisposable
{
    private const string Scope = @"root\WMI";
    private const string Query = "SELECT Active, Brightness, InstanceName FROM WmiMonitorBrightnessEvent";
    private WmiConnection? _connection;
    private WmiEventSubscription? _subscription;
    private bool _disposed;

    public event EventHandler<WmiBrightnessChangedEventArgs>? BrightnessChanged;

    public bool IsActive => _subscription is not null;

    public int ErrorCode { get; private set; }

    public string? ErrorMessage { get; private set; }

    public bool TryStart()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_subscription is not null)
        {
            return true;
        }

        var candidate = new WmiConnection(Scope);
        try
        {
            var subscription = candidate.CreateEventSubscription(
                Query,
                value => BrightnessChanged?.Invoke(
                    this,
                    new WmiBrightnessChangedEventArgs(
                        value.GetPropertyValue<string>("InstanceName") ?? string.Empty,
                        value.GetPropertyValue<byte>("Brightness"),
                        value.GetPropertyValue<bool>("Active"))));
            _connection = candidate;
            _subscription = subscription;
            ErrorCode = 0;
            ErrorMessage = null;
            return true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            ErrorCode = exception.HResult;
            ErrorMessage = exception.Message;
            candidate.Dispose();
            return false;
        }
    }

    public void Stop()
    {
        var subscription = _subscription;
        var connection = _connection;
        _subscription = null;
        _connection = null;
        subscription?.Dispose();
        connection?.Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        GC.SuppressFinalize(this);
    }
}
