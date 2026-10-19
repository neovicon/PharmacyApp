using PharmacyApp.Application.Authentication.Commands;
using PharmacyApp.Domain.Entities;
using PharmacyApp.Domain.Enums;
using Xunit;

namespace PharmacyApp.Application.Tests;

public class LoginCommandHandlerTests
{
    private readonly TestUserRepository _userRepository = new();
    private readonly TestPasswordHasher _passwordHasher = new();
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _handler = new LoginCommandHandler(_userRepository, _passwordHasher);
    }

    [Fact]
    public async Task HandleAsync_WithValidCredentials_ReturnsUserSessionDto()
    {
        var now = DateTimeOffset.UtcNow;
        var user = User.Create("pharmacist", "Jane Doe", _passwordHasher.HashPassword("CorrectPass1!"), UserRole.Pharmacist, now);
        await _userRepository.AddAsync(user);

        var command = new LoginCommand("pharmacist", "CorrectPass1!");
        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(user.Id, result.Value.UserId);
        Assert.Equal("pharmacist", result.Value.Username);
        Assert.Equal("Jane Doe", result.Value.DisplayName);
        Assert.Equal(UserRole.Pharmacist, result.Value.Role);
    }

    [Fact]
    public async Task HandleAsync_WithUnknownUsername_ReturnsGenericFailure()
    {
        var command = new LoginCommand("nonexistent_user", "AnyPassword123!");
        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("Invalid username or password.", result.Error);
    }

    [Fact]
    public async Task HandleAsync_WithWrongPassword_ReturnsGenericFailure()
    {
        var now = DateTimeOffset.UtcNow;
        var user = User.Create("admin", "Admin", _passwordHasher.HashPassword("RealPass123!"), UserRole.Admin, now);
        await _userRepository.AddAsync(user);

        var command = new LoginCommand("admin", "WrongPassword123!");
        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("Invalid username or password.", result.Error);
    }

    [Fact]
    public async Task HandleAsync_WithInactiveUser_ReturnsGenericFailure()
    {
        var now = DateTimeOffset.UtcNow;
        var user = User.Create("disabled_user", "Disabled", _passwordHasher.HashPassword("Pass123!"), UserRole.Cashier, now);
        user.Deactivate(now);
        await _userRepository.AddAsync(user);

        var command = new LoginCommand("disabled_user", "Pass123!");
        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("Invalid username or password.", result.Error);
    }

    [Theory]
    [InlineData("", "ValidPass1!")]
    [InlineData("   ", "ValidPass1!")]
    [InlineData("admin", "")]
    [InlineData("admin", null)]
    public async Task HandleAsync_WithEmptyCredentials_ReturnsGenericFailure(string username, string? password)
    {
        var command = new LoginCommand(username, password!);
        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("Invalid username or password.", result.Error);
    }

    [Fact]
    public async Task HandleAsync_WithOversizedPassword_ReturnsGenericFailureWithoutAttemptingVerification()
    {
        string oversizedPassword = new string('x', 200);
        var command = new LoginCommand("admin", oversizedPassword);
        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("Invalid username or password.", result.Error);
    }
}
