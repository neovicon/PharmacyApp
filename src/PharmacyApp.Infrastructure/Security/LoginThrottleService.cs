using PharmacyApp.Application.Abstractions;
using Serilog;

namespace PharmacyApp.Infrastructure.Security;

/// <summary>
/// In-memory, thread-safe implementation of <see cref="ILoginThrottleService"/>.
/// Applies progressive cooldown delays to the workstation after repeated failed login attempts,
/// protecting all local accounts against automated brute-force attacks without permanent lockouts.
/// 
/// Lifecycle & Persistence:
/// State is maintained in-memory for the running application instance. On process restart,
/// the counter resets. In a local desktop environment, OS process termination and .NET/Avalonia
/// bootstrap time impose their own natural physical latency, preventing rapid restart bypass
/// while guaranteeing legitimate pharmacy operators are never locked out across reboots.
/// </summary>
public sealed class LoginThrottleService : ILoginThrottleService
{
    public const int MaxCooldownSeconds = 60;
    public static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(15);

    private readonly object _lock = new();
    private readonly IDateTimeProvider _dateTimeProvider;

    private int _failedAttempts;
    private DateTimeOffset? _lockoutEndUtc;
    private DateTimeOffset? _lastFailedAttemptUtc;

    public int FailedAttempts
    {
        get { lock (_lock) return _failedAttempts; }
    }

    public LoginThrottleService(IDateTimeProvider dateTimeProvider)
    {
        _dateTimeProvider = dateTimeProvider;
    }

    public bool IsThrottled(out TimeSpan retryAfter)
    {
        lock (_lock)
        {
            DateTimeOffset now = _dateTimeProvider.UtcNow;

            // Reset stale attempts if outside the sliding failure window
            if (_lastFailedAttemptUtc.HasValue && now - _lastFailedAttemptUtc.Value > FailureWindow)
            {
                _failedAttempts = 0;
                _lockoutEndUtc = null;
                retryAfter = TimeSpan.Zero;
                return false;
            }

            if (_lockoutEndUtc.HasValue)
            {
                if (now < _lockoutEndUtc.Value)
                {
                    retryAfter = _lockoutEndUtc.Value - now;
                    return true;
                }

                // Cooldown period has elapsed
                _lockoutEndUtc = null;
            }

            retryAfter = TimeSpan.Zero;
            return false;
        }
    }

    public void RecordFailedAttempt()
    {
        lock (_lock)
        {
            DateTimeOffset now = _dateTimeProvider.UtcNow;

            // Reset stale attempts if outside the sliding failure window
            if (_lastFailedAttemptUtc.HasValue && now - _lastFailedAttemptUtc.Value > FailureWindow)
            {
                _failedAttempts = 0;
                _lockoutEndUtc = null;
            }

            _failedAttempts++;
            _lastFailedAttemptUtc = now;

            TimeSpan cooldown = CalculateCooldown(_failedAttempts);
            if (cooldown > TimeSpan.Zero)
            {
                _lockoutEndUtc = now + cooldown;
                Log.Warning(
                    "Workstation login cooldown activated. ConsecutiveFailures={Failures}, CooldownSeconds={Seconds}",
                    _failedAttempts,
                    cooldown.TotalSeconds);
            }
            else
            {
                _lockoutEndUtc = null;
            }
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _failedAttempts = 0;
            _lockoutEndUtc = null;
            _lastFailedAttemptUtc = null;
        }
    }

    public static TimeSpan CalculateCooldown(int failedAttempts)
    {
        return failedAttempts switch
        {
            <= 2 => TimeSpan.Zero,
            3 => TimeSpan.FromSeconds(5),
            4 => TimeSpan.FromSeconds(15),
            5 => TimeSpan.FromSeconds(30),
            _ => TimeSpan.FromSeconds(MaxCooldownSeconds)
        };
    }
}
