using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Authentication.Commands;
using PharmacyApp.Domain.Entities;
using PharmacyApp.Domain.Enums;
using Xunit;

namespace PharmacyApp.Application.Tests;

public class CreateInitialAdminCommandHandlerTests
{
    private readonly TestUserRepository _userRepository = new();
    private readonly TestPasswordHasher _passwordHasher = new();
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTimeOffset(2026, 10, 18, 10, 0, 0, TimeSpan.Zero));
    private readonly CreateInitialAdminCommandHandler _handler;

    public CreateInitialAdminCommandHandlerTests()
    {
        _handler = new CreateInitialAdminCommandHandler(_userRepository, _passwordHasher, _dateTimeProvider);
    }

    [Fact]
    public async Task HandleAsync_WhenNoUsersExist_CreatesAdminAndReturnsId()
    {
        var command = new CreateInitialAdminCommand("Admin", "Main Administrator", "AdminPass123!", "AdminPass123!");

        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value);

        var createdUser = await _userRepository.GetByIdAsync(result.Value);
        Assert.NotNull(createdUser);
        Assert.Equal("admin", createdUser.Username);
        Assert.Equal("Main Administrator", createdUser.DisplayName);
        Assert.Equal(UserRole.Admin, createdUser.Role);
        Assert.True(createdUser.IsActive);
        Assert.Equal("hashed_AdminPass123!", createdUser.PasswordHash);
    }

    [Fact]
    public async Task HandleAsync_WhenUsersAlreadyExist_RejectsWithFailure()
    {
        // Arrange existing user
        var existingUser = User.Create("existing", "Existing User", "hash", UserRole.Admin, _dateTimeProvider.UtcNow);
        await _userRepository.AddAsync(existingUser);

        var command = new CreateInitialAdminCommand("secondadmin", "Second Admin", "AdminPass123!", "AdminPass123!");

        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Contains("Initial setup has already been completed", result.Error);
    }

    [Theory]
    [InlineData("", "Display", "ValidPass1!")]
    [InlineData("  ", "Display", "ValidPass1!")]
    [InlineData("ab", "Display", "ValidPass1!")] // < 3 characters
    public async Task HandleAsync_InvalidUsername_ReturnsFailure(string username, string displayName, string password)
    {
        var command = new CreateInitialAdminCommand(username, displayName, password, password);

        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Contains("Username", result.Error);
    }

    [Fact]
    public async Task HandleAsync_EmptyDisplayName_ReturnsFailure()
    {
        var command = new CreateInitialAdminCommand("adminuser", "   ", "ValidPass1!", "ValidPass1!");

        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("Display name is required.", result.Error);
    }

    [Fact]
    public async Task HandleAsync_MismatchedPasswords_ReturnsFailure()
    {
        var command = new CreateInitialAdminCommand("adminuser", "Administrator", "ValidPass1!", "DifferentPass2@");

        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("Passwords do not match.", result.Error);
    }

    [Fact]
    public async Task HandleAsync_WeakPassword_ReturnsFailure()
    {
        var command = new CreateInitialAdminCommand("adminuser", "Administrator", "short", "short");

        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Contains("at least 8 characters", result.Error);
    }
}

public class TestUserRepository : IUserRepository
{
    private readonly List<User> _users = new();

    public Task<bool> HasAnyUsersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.Count > 0);

    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)));

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.FirstOrDefault(u => u.Id == id));

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        _users.Add(user);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        var index = _users.FindIndex(u => u.Id == user.Id);
        if (index >= 0)
        {
            _users[index] = user;
        }
        return Task.CompletedTask;
    }
}

public class TestPasswordHasher : IPasswordHasher
{
    public string HashPassword(string password) => $"hashed_{password}";

    public bool VerifyPassword(string password, string passwordHash) =>
        passwordHash == $"hashed_{password}";
}

public class TestDateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow { get; set; }

    public TestDateTimeProvider(DateTimeOffset initialTime)
    {
        UtcNow = initialTime;
    }
}
