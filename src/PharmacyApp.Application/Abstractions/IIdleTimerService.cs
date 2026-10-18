namespace PharmacyApp.Application.Abstractions;

/// <summary>
/// Monitors user inactivity and raises an event when the configured idle duration elapses.
/// Designed for deterministic unit testing without real-time delays.
/// </summary>
public interface IIdleTimerService : IDisposable
{
    /// <summary>
    /// Configured threshold after which an idle session should lock.
    /// Default is 15 minutes.
    /// </summary>
    TimeSpan InactivityTimeout { get; set; }

    bool IsRunning { get; }

    void Start();
    void Stop();
    void RecordActivity();

    /// <summary>
    /// Evaluates current inactivity against the threshold.
    /// Can be invoked directly by tests or periodically by a timer.
    /// </summary>
    void CheckInactivity();

    event EventHandler? InactivityDetected;
}
