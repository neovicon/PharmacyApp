using PharmacyApp.Application.Abstractions;

namespace PharmacyApp.Infrastructure.Security;

/// <summary>
/// Monitors user inactivity using a configurable timeout threshold.
/// Uses <see cref="IDateTimeProvider"/> to allow deterministic unit testing.
/// </summary>
public sealed class IdleTimerService : IIdleTimerService
{
    private readonly object _lock = new();
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly Timer? _timer;

    private DateTimeOffset _lastActivityUtc;
    private bool _isRunning;
    private TimeSpan _inactivityTimeout = TimeSpan.FromMinutes(15);
    private bool _disposed;

    public TimeSpan InactivityTimeout
    {
        get { lock (_lock) return _inactivityTimeout; }
        set
        {
            if (value <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(value), "Inactivity timeout must be positive.");

            lock (_lock) _inactivityTimeout = value;
        }
    }

    public bool IsRunning
    {
        get { lock (_lock) return _isRunning; }
    }

    public event EventHandler? InactivityDetected;

    public IdleTimerService(IDateTimeProvider dateTimeProvider, bool enableAutoTimer = true)
    {
        _dateTimeProvider = dateTimeProvider;
        _lastActivityUtc = _dateTimeProvider.UtcNow;

        if (enableAutoTimer)
        {
            // Evaluate inactivity every 10 seconds in production
            _timer = new Timer(_ => CheckInactivity(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }

    public void Start()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _isRunning = true;
            _lastActivityUtc = _dateTimeProvider.UtcNow;
            _timer?.Change(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _isRunning = false;
            _timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }

    public void RecordActivity()
    {
        lock (_lock)
        {
            _lastActivityUtc = _dateTimeProvider.UtcNow;
        }
    }

    public void CheckInactivity()
    {
        bool triggered = false;
        lock (_lock)
        {
            if (!_isRunning || _disposed)
                return;

            DateTimeOffset now = _dateTimeProvider.UtcNow;
            if (now - _lastActivityUtc >= _inactivityTimeout)
            {
                triggered = true;
                _isRunning = false;
                _timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            }
        }

        if (triggered)
        {
            InactivityDetected?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            _isRunning = false;
            _timer?.Dispose();
        }
    }
}
