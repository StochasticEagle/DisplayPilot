using System.Threading;
namespace DisplayPilot.Windows.Scheduling;

public sealed class WindowsBoundaryTimer : IDisposable
{
    private readonly object _sync = new();
    private readonly Timer _timer;
    private bool _disposed;
    private DateTimeOffset? _due;
    public event EventHandler? Elapsed;

    public WindowsBoundaryTimer()
        => _timer = new Timer(OnTick, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

    public bool IsArmed { get { lock (_sync) return _due is not null; } }
    public DateTimeOffset? DueTime { get { lock (_sync) return _due; } }

    public void Arm(DateTimeOffset dueTime)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _due = dueTime;
            var delay = dueTime - DateTimeOffset.UtcNow;
            if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
            _timer.Change(delay, Timeout.InfiniteTimeSpan);
        }
    }

    public void Cancel()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _due = null;
            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }

    private void OnTick(object? _)
    {
        EventHandler? callback;
        lock (_sync)
        {
            if (_disposed || _due is null) return;
            if (DateTimeOffset.UtcNow < _due.Value)
            {
                _timer.Change(_due.Value - DateTimeOffset.UtcNow, Timeout.InfiniteTimeSpan);
                return;
            }
            _due = null;
            callback = Elapsed;
        }
        callback?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            _due = null;
            _timer.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
