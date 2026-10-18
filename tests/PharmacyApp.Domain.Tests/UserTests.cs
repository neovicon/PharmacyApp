using PharmacyApp.Domain.Entities;
using PharmacyApp.Domain.Enums;
using Xunit;

namespace PharmacyApp.Domain.Tests;

public class UserTests
{
    private readonly DateTimeOffset _fixedTime = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidArguments_InitializesCorrectly()
    {
        var user = User.Create("AdminUser", "Admin Display", "hashed_secret", UserRole.Admin, _fixedTime);

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("adminuser", user.Username); // normalized to lowercase
        Assert.Equal("Admin Display", user.DisplayName);
        Assert.Equal("hashed_secret", user.PasswordHash);
        Assert.Equal(UserRole.Admin, user.Role);
        Assert.True(user.IsActive);
        Assert.Equal(_fixedTime, user.CreatedAt);
        Assert.Equal(_fixedTime, user.UpdatedAt);
        Assert.Equal(1u, user.Version);
    }

    [Theory]
    [InlineData("", "Display", "hash")]
    [InlineData("   ", "Display", "hash")]
    [InlineData(null, "Display", "hash")]
    [InlineData("admin", "", "hash")]
    [InlineData("admin", "   ", "hash")]
    [InlineData("admin", null, "hash")]
    [InlineData("admin", "Display", "")]
    [InlineData("admin", "Display", "   ")]
    [InlineData("admin", "Display", null)]
    public void Create_WithInvalidArguments_ThrowsArgumentException(string? username, string? displayName, string? hash)
    {
        Assert.Throws<ArgumentException>(() =>
            User.Create(username!, displayName!, hash!, UserRole.Admin, _fixedTime));
    }

    [Fact]
    public void UpdatePassword_UpdatesHashAndTimestamp()
    {
        var user = User.Create("pharmacist", "Jane Doe", "old_hash", UserRole.Pharmacist, _fixedTime);
        var updateTime = _fixedTime.AddHours(2);

        user.UpdatePassword("new_hashed_secret", updateTime);

        Assert.Equal("new_hashed_secret", user.PasswordHash);
        Assert.Equal(updateTime, user.UpdatedAt);
        Assert.Equal(2u, user.Version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void UpdatePassword_WithEmptyHash_ThrowsArgumentException(string? invalidHash)
    {
        var user = User.Create("pharmacist", "Jane Doe", "old_hash", UserRole.Pharmacist, _fixedTime);

        Assert.Throws<ArgumentException>(() => user.UpdatePassword(invalidHash!, _fixedTime.AddHours(1)));
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalseAndIncrementsVersion()
    {
        var user = User.Create("cashier", "Bob Smith", "hash", UserRole.Cashier, _fixedTime);
        var deactivateTime = _fixedTime.AddDays(1);

        user.Deactivate(deactivateTime);

        Assert.False(user.IsActive);
        Assert.Equal(deactivateTime, user.UpdatedAt);
        Assert.Equal(2u, user.Version);
    }

    [Fact]
    public void Activate_SetsIsActiveTrueAndIncrementsVersion()
    {
        var user = User.Create("cashier", "Bob Smith", "hash", UserRole.Cashier, _fixedTime);
        user.Deactivate(_fixedTime.AddDays(1));
        var activateTime = _fixedTime.AddDays(2);

        user.Activate(activateTime);

        Assert.True(user.IsActive);
        Assert.Equal(activateTime, user.UpdatedAt);
        Assert.Equal(3u, user.Version);
    }

    [Fact]
    public void Load_RestoresExactState()
    {
        var id = Guid.NewGuid();
        var createdAt = _fixedTime.AddDays(-10);
        var updatedAt = _fixedTime.AddDays(-2);

        var user = User.Load(id, "existing_user", "Existing Name", "existing_hash", UserRole.Pharmacist, false, createdAt, updatedAt, 42);

        Assert.Equal(id, user.Id);
        Assert.Equal("existing_user", user.Username);
        Assert.Equal("Existing Name", user.DisplayName);
        Assert.Equal("existing_hash", user.PasswordHash);
        Assert.Equal(UserRole.Pharmacist, user.Role);
        Assert.False(user.IsActive);
        Assert.Equal(createdAt, user.CreatedAt);
        Assert.Equal(updatedAt, user.UpdatedAt);
        Assert.Equal(42u, user.Version);
    }
}
