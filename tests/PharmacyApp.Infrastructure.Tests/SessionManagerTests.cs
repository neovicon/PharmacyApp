using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Authentication;
using PharmacyApp.Domain.Entities;
using PharmacyApp.Domain.Enums;
using PharmacyApp.Infrastructure.Security;
using Xunit;

namespace PharmacyApp.Infrastructure.Tests;

public class SessionManagerTests
{
    private readonly StubUserRepository _userRepo = new();
    private readonly StubPasswordHasher _hasher = new();
    private readonly SessionManager _sessionManager;

    public SessionManagerTests()
    {
        _sessionManager = new SessionManager(_userRepo, _hasher);
    }

    [Fact]
    public void InitialState_IsUnauthenticated_AndIdentityIsUnavailable()
    {
        Assert.Equal(SessionState.Unauthenticated, _sessionManager.State);
        Assert.False(_sessionManager.IsAuthenticated);
        Assert.Equal(Guid.Empty, _sessionManager.UserId);
        Assert.Equal(string.Empty, _sessionManager.Username);
        Assert.Equal(string.Empty, _sessionManager.DisplayName);
        Assert.Null(_sessionManager.CurrentUser);
    }

    [Fact]
    public void StartSession_TransitionsToActive_AndExposesIdentity()
    {
        var sessionEvents = new List<SessionStateChangedEventArgs>();
        _sessionManager.SessionStateChanged += (_, e) => sessionEvents.Add(e);

        var userId = Guid.NewGuid();
        var userDto = new UserSessionDto(userId, "admin", "Administrator", UserRole.Admin);

        _sessionManager.StartSession(userDto);

        Assert.Equal(SessionState.Active, _sessionManager.State);
        Assert.True(_sessionManager.IsAuthenticated);
        Assert.Equal(userId, _sessionManager.UserId);
        Assert.Equal("admin", _sessionManager.Username);
        Assert.Equal("Administrator", _sessionManager.DisplayName);
        Assert.Equal(UserRole.Admin, _sessionManager.Role);

        Assert.Single(sessionEvents);
        Assert.Equal(SessionState.Active, sessionEvents[0].State);
        Assert.Same(userDto, sessionEvents[0].User);
    }

    [Fact]
    public void LockSession_TransitionsToLocked_AndMasksActiveIdentity()
    {
        var sessionEvents = new List<SessionStateChangedEventArgs>();
        _sessionManager.SessionStateChanged += (_, e) => sessionEvents.Add(e);

        var userId = Guid.NewGuid();
        var userDto = new UserSessionDto(userId, "pharmacist", "Jane Doe", UserRole.Pharmacist);
        _sessionManager.StartSession(userDto);

        _sessionManager.LockSession();

        Assert.Equal(SessionState.Locked, _sessionManager.State);
        Assert.False(_sessionManager.IsAuthenticated); // Identity is not authenticated while locked
        Assert.Equal(Guid.Empty, _sessionManager.UserId); // Masked from ICurrentUserService
        Assert.Equal(string.Empty, _sessionManager.Username);
        Assert.Equal(string.Empty, _sessionManager.DisplayName);

        // But CurrentUser remains available for lock screen display
        Assert.NotNull(_sessionManager.CurrentUser);
        Assert.Equal("pharmacist", _sessionManager.CurrentUser.Username);

        Assert.Equal(2, sessionEvents.Count);
        Assert.Equal(SessionState.Locked, sessionEvents[1].State);
    }

    [Fact]
    public async Task UnlockSessionAsync_WithCorrectPassword_RestoresActiveSession()
    {
        var userId = Guid.NewGuid();
        var domainUser = User.Create("admin", "Admin Name", "secret_hash", UserRole.Admin, DateTimeOffset.UtcNow);
        // Force the ID to match
        _userRepo.UsersById[userId] = domainUser;

        var userDto = new UserSessionDto(userId, "admin", "Admin Name", UserRole.Admin);
        _sessionManager.StartSession(userDto);
        _sessionManager.LockSession();

        var result = await _sessionManager.UnlockSessionAsync("correct_password");

        Assert.True(result.IsSuccess);
        Assert.Equal(SessionState.Active, _sessionManager.State);
        Assert.True(_sessionManager.IsAuthenticated);
        Assert.Equal(userId, _sessionManager.UserId);
    }

    [Fact]
    public async Task UnlockSessionAsync_WithWrongPassword_FailsAndRemainsLocked()
    {
        var userId = Guid.NewGuid();
        var domainUser = User.Create("admin", "Admin Name", "secret_hash", UserRole.Admin, DateTimeOffset.UtcNow);
        _userRepo.UsersById[userId] = domainUser;

        var userDto = new UserSessionDto(userId, "admin", "Admin Name", UserRole.Admin);
        _sessionManager.StartSession(userDto);
        _sessionManager.LockSession();

        var result = await _sessionManager.UnlockSessionAsync("wrong_password");

        Assert.True(result.IsFailure);
        Assert.Equal("Invalid password.", result.Error);
        Assert.Equal(SessionState.Locked, _sessionManager.State);
        Assert.False(_sessionManager.IsAuthenticated);
    }

    [Fact]
    public void TerminateSession_ClearsCurrentUser_AndResetsState()
    {
        var sessionEvents = new List<SessionStateChangedEventArgs>();
        _sessionManager.SessionStateChanged += (_, e) => sessionEvents.Add(e);

        var userId = Guid.NewGuid();
        var userDto = new UserSessionDto(userId, "cashier", "Bob", UserRole.Cashier);
        _sessionManager.StartSession(userDto);

        _sessionManager.TerminateSession();

        Assert.Equal(SessionState.Unauthenticated, _sessionManager.State);
        Assert.False(_sessionManager.IsAuthenticated);
        Assert.Null(_sessionManager.CurrentUser);
        Assert.Equal(Guid.Empty, _sessionManager.UserId);

        Assert.Equal(2, sessionEvents.Count);
        Assert.Equal(SessionState.Unauthenticated, sessionEvents[1].State);
        Assert.Null(sessionEvents[1].User);
    }
}

internal sealed class StubUserRepository : IUserRepository
{
    public Dictionary<Guid, User> UsersById { get; } = new();

    public Task<bool> HasAnyUsersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(UsersById.Count > 0);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        UsersById.TryGetValue(id, out var user);
        return Task.FromResult(user);
    }

    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
        Task.FromResult(UsersById.Values.FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)));

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        UsersById[user.Id] = user;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        UsersById[user.Id] = user;
        return Task.CompletedTask;
    }
}

internal sealed class StubPasswordHasher : IPasswordHasher
{
    public string HashPassword(string password) => $"hashed_{password}";

    public bool VerifyPassword(string password, string passwordHash) =>
        password == "correct_password" || passwordHash == $"hashed_{password}";
}
