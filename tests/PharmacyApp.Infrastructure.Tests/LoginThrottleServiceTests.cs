using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Authentication.Commands;
using PharmacyApp.Domain.Entities;
using PharmacyApp.Domain.Enums;
using PharmacyApp.Infrastructure.Persistence;
using PharmacyApp.Infrastructure.Security;
using Xunit;

namespace PharmacyApp.Infrastructure.Tests;

public class LoginThrottleServiceTests : IDisposable
{
    private readonly ControllableDateTimeProvider _clock;
    private readonly LoginThrottleService _throttleService;

    public LoginThrottleServiceTests()
    {
        _clock = new ControllableDateTimeProvider(new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero));
        _throttleService = new LoginThrottleService(_clock);
    }

    public void Dispose()
    {
    }

    [Fact]
    public void InitialState_IsNotThrottled()
    {
        bool throttled = _throttleService.IsThrottled(out TimeSpan retryAfter);

        Assert.False(throttled);
        Assert.Equal(TimeSpan.Zero, retryAfter);
        Assert.Equal(0, _throttleService.FailedAttempts);
    }

    [Fact]
    public void FreeAttempts_DoNotTriggerCooldown()
    {
        // 1st failed attempt
        _throttleService.RecordFailedAttempt();
        Assert.Equal(1, _throttleService.FailedAttempts);
        Assert.False(_throttleService.IsThrottled(out TimeSpan retryAfter1));
        Assert.Equal(TimeSpan.Zero, retryAfter1);

        // 2nd failed attempt
        _throttleService.RecordFailedAttempt();
        Assert.Equal(2, _throttleService.FailedAttempts);
        Assert.False(_throttleService.IsThrottled(out TimeSpan retryAfter2));
        Assert.Equal(TimeSpan.Zero, retryAfter2);
    }

    [Fact]
    public void CooldownActivation_OnThirdFailedAttempt()
    {
        _throttleService.RecordFailedAttempt();
        _throttleService.RecordFailedAttempt();
        _throttleService.RecordFailedAttempt();

        Assert.Equal(3, _throttleService.FailedAttempts);
        bool throttled = _throttleService.IsThrottled(out TimeSpan retryAfter);

        Assert.True(throttled);
        Assert.True(retryAfter > TimeSpan.FromSeconds(4) && retryAfter <= TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 5)]
    [InlineData(4, 15)]
    [InlineData(5, 30)]
    [InlineData(6, 60)]
    [InlineData(10, 60)] // Capped at 60s max cooldown
    public void DurationProgression_CalculatesExpectedCooldowns(int failedAttempts, int expectedCooldownSeconds)
    {
        TimeSpan cooldown = LoginThrottleService.CalculateCooldown(failedAttempts);
        Assert.Equal(TimeSpan.FromSeconds(expectedCooldownSeconds), cooldown);
    }

    [Fact]
    public void CooldownExpiry_AllowsNextAttemptAfterTimeout()
    {
        _throttleService.RecordFailedAttempt();
        _throttleService.RecordFailedAttempt();
        _throttleService.RecordFailedAttempt(); // 3rd failure: 5s cooldown

        Assert.True(_throttleService.IsThrottled(out _));

        // Advance 4 seconds: still in cooldown
        _clock.Advance(TimeSpan.FromSeconds(4));
        Assert.True(_throttleService.IsThrottled(out TimeSpan remaining));
        Assert.True(remaining <= TimeSpan.FromSeconds(1));

        // Advance past the 5-second mark
        _clock.Advance(TimeSpan.FromSeconds(1.1));
        Assert.False(_throttleService.IsThrottled(out TimeSpan expiredRemaining));
        Assert.Equal(TimeSpan.Zero, expiredRemaining);
    }

    [Fact]
    public void SuccessfulLogin_ResetsCounterAndClearsCooldown()
    {
        _throttleService.RecordFailedAttempt();
        _throttleService.RecordFailedAttempt();
        _throttleService.RecordFailedAttempt();
        Assert.True(_throttleService.IsThrottled(out _));

        // Reset called on successful login
        _throttleService.Reset();

        Assert.Equal(0, _throttleService.FailedAttempts);
        Assert.False(_throttleService.IsThrottled(out _));
    }

    [Fact]
    public void FailureWindowExpiry_ResetsStaleFailedAttempts()
    {
        // 2 failures recorded at 12:00
        _throttleService.RecordFailedAttempt();
        _throttleService.RecordFailedAttempt();
        Assert.Equal(2, _throttleService.FailedAttempts);

        // Advance time by 16 minutes (beyond 15-minute failure window)
        _clock.Advance(TimeSpan.FromMinutes(16));

        // Next failure must reset the stale count and start fresh at 1
        _throttleService.RecordFailedAttempt();
        Assert.Equal(1, _throttleService.FailedAttempts);
        Assert.False(_throttleService.IsThrottled(out _));
    }

    [Fact]
    public async Task ConcurrentFailedAttempts_ThreadSafety()
    {
        const int concurrentCallers = 20;
        var tasks = new Task[concurrentCallers];

        for (int i = 0; i < concurrentCallers; i++)
        {
            tasks[i] = Task.Run(() => _throttleService.RecordFailedAttempt());
        }

        await Task.WhenAll(tasks);

        Assert.Equal(concurrentCallers, _throttleService.FailedAttempts);
        Assert.True(_throttleService.IsThrottled(out TimeSpan remaining));
        Assert.True(remaining > TimeSpan.FromSeconds(50) && remaining <= TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task FullIntegration_ThrottlesWorkstationAcrossDifferentUsernames()
    {
        using var isolatedPaths = new IsolatedTestPathProvider();
        string connectionString = $"Data Source={Path.Combine(isolatedPaths.DataDirectory, "pharmacy.db")};Cache=Shared";
        var dbInit = new SqliteDatabaseInitializer(connectionString);
        await dbInit.InitializeAsync();

        var repo = new SqliteUserRepository(connectionString);
        var hasher = new BCryptPasswordHasher();

        // Seed active user
        string password = "CorrectAdminPass123!";
        var user = User.Create("admin", "Admin", hasher.HashPassword(password), UserRole.Admin, _clock.UtcNow);
        await repo.AddAsync(user);

        var handler = new LoginCommandHandler(repo, hasher, _throttleService);

        // 1. Fail with "admin"
        var r1 = await handler.HandleAsync(new LoginCommand("admin", "wrong1"));
        Assert.True(r1.IsFailure);
        Assert.Equal("Invalid username or password.", r1.Error);

        // 2. Fail with "pharmacist" (different username)
        var r2 = await handler.HandleAsync(new LoginCommand("pharmacist", "wrong2"));
        Assert.True(r2.IsFailure);
        Assert.Equal("Invalid username or password.", r2.Error);

        // 3. Fail with "cashier" (third different username) -> triggers workstation 5s cooldown
        var r3 = await handler.HandleAsync(new LoginCommand("cashier", "wrong3"));
        Assert.True(r3.IsFailure);
        Assert.Equal("Invalid username or password.", r3.Error);

        // 4. Attempt 4 with "manager" (even a 4th username!) is blocked by workstation throttling
        var r4 = await handler.HandleAsync(new LoginCommand("manager", "any"));
        Assert.True(r4.IsFailure);
        Assert.Contains("Too many failed login attempts", r4.Error);
        Assert.Contains("5 seconds", r4.Error);

        // 5. Advance clock past 5 seconds
        _clock.Advance(TimeSpan.FromSeconds(5.5));

        // 6. Valid login with correct credentials succeeds and resets workstation throttle
        var r5 = await handler.HandleAsync(new LoginCommand("admin", password));
        Assert.True(r5.IsSuccess);
        Assert.Equal(0, _throttleService.FailedAttempts);
        Assert.False(_throttleService.IsThrottled(out _));

        // 7. Subsequent single failure is allowed immediately without cooldown
        var r6 = await handler.HandleAsync(new LoginCommand("admin", "wrong_again"));
        Assert.True(r6.IsFailure);
        Assert.Equal("Invalid username or password.", r6.Error);
        Assert.Equal(1, _throttleService.FailedAttempts);
        Assert.False(_throttleService.IsThrottled(out _));
    }
}
