namespace PharmacyApp.Application.Abstractions;

/// <summary>
/// Monitors and throttles failed login attempts on this workstation
/// to prevent automated brute-force credential stuffing.
/// Enforces progressive, time-bounded cooldowns without permanently locking accounts.
/// </summary>
public interface ILoginThrottleService
{
    /// <summary>
    /// Checks whether login attempts on this workstation are currently throttled.
    /// </summary>
    /// <param name="retryAfter">The remaining cooldown period before a new attempt is permitted.</param>
    /// <returns>True if throttled; false if an attempt is permitted.</returns>
    bool IsThrottled(out TimeSpan retryAfter);

    /// <summary>
    /// Records a failed authentication attempt and activates or advances
    /// the progressive cooldown if the failure threshold is reached.
    /// </summary>
    void RecordFailedAttempt();

    /// <summary>
    /// Clears active cooldowns and resets the failed attempt counter upon successful login.
    /// </summary>
    void Reset();

    /// <summary>
    /// Number of consecutive failed attempts recorded in the current active window.
    /// </summary>
    int FailedAttempts { get; }
}
