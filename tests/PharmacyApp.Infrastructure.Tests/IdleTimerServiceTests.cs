using PharmacyApp.Application.Abstractions;
using PharmacyApp.Infrastructure.Security;
using Xunit;

namespace PharmacyApp.Infrastructure.Tests;

public class IdleTimerServiceTests
{
    private readonly ControllableDateTimeProvider _timeProvider;
    private readonly IdleTimerService _idleTimer;

    public IdleTimerServiceTests()
    {
        _timeProvider = new ControllableDateTimeProvider(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero));
        // Disable internal background timer for deterministic manual evaluation
        _idleTimer = new IdleTimerService(_timeProvider, enableAutoTimer: false);
    }

    [Fact]
    public void DefaultInactivityTimeout_IsFifteenMinutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(15), _idleTimer.InactivityTimeout);
    }

    [Fact]
    public void CheckInactivity_WhenNotRunning_DoesNotTriggerEvent()
    {
        bool triggered = false;
        _idleTimer.InactivityDetected += (_, _) => triggered = true;

        // Advance 20 minutes past default timeout
        _timeProvider.Advance(TimeSpan.FromMinutes(20));
        _idleTimer.CheckInactivity();

        Assert.False(triggered);
    }

    [Fact]
    public void CheckInactivity_WhenWithinTimeout_DoesNotTrigger()
    {
        bool triggered = false;
        _idleTimer.InactivityDetected += (_, _) => triggered = true;

        _idleTimer.Start();
        _timeProvider.Advance(TimeSpan.FromMinutes(14));
        _idleTimer.CheckInactivity();

        Assert.False(triggered);
        Assert.True(_idleTimer.IsRunning);
    }

    [Fact]
    public void CheckInactivity_WhenTimeoutExceeded_TriggersEventAndStops()
    {
        bool triggered = false;
        _idleTimer.InactivityDetected += (_, _) => triggered = true;

        _idleTimer.Start();
        _timeProvider.Advance(TimeSpan.FromMinutes(15));
        _idleTimer.CheckInactivity();

        Assert.True(triggered);
        Assert.False(_idleTimer.IsRunning);
    }

    [Fact]
    public void RecordActivity_ResetsInactivityTimer()
    {
        bool triggered = false;
        _idleTimer.InactivityDetected += (_, _) => triggered = true;

        _idleTimer.Start();
        // 10 minutes pass
        _timeProvider.Advance(TimeSpan.FromMinutes(10));
        _idleTimer.CheckInactivity();
        Assert.False(triggered);

        // User moves mouse or types key
        _idleTimer.RecordActivity();

        // Another 10 minutes pass (total 20 min from start, but only 10 min since last activity)
        _timeProvider.Advance(TimeSpan.FromMinutes(10));
        _idleTimer.CheckInactivity();
        Assert.False(triggered);

        // 6 more minutes pass (16 minutes since last activity) -> triggers
        _timeProvider.Advance(TimeSpan.FromMinutes(6));
        _idleTimer.CheckInactivity();
        Assert.True(triggered);
    }

    [Fact]
    public void Stop_HaltsInactivityTracking()
    {
        bool triggered = false;
        _idleTimer.InactivityDetected += (_, _) => triggered = true;

        _idleTimer.Start();
        _idleTimer.Stop();

        _timeProvider.Advance(TimeSpan.FromMinutes(30));
        _idleTimer.CheckInactivity();

        Assert.False(triggered);
        Assert.False(_idleTimer.IsRunning);
    }

    [Fact]
    public void InactivityTimeout_InvalidValue_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _idleTimer.InactivityTimeout = TimeSpan.Zero);
        Assert.Throws<ArgumentOutOfRangeException>(() => _idleTimer.InactivityTimeout = TimeSpan.FromMinutes(-5));
    }
}

internal sealed class ControllableDateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow { get; set; }

    public ControllableDateTimeProvider(DateTimeOffset initialTime)
    {
        UtcNow = initialTime;
    }

    public void Advance(TimeSpan duration)
    {
        UtcNow = UtcNow.Add(duration);
    }
}
