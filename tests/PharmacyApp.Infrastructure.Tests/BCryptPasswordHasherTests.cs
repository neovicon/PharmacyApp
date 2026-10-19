using PharmacyApp.Infrastructure.Security;
using Xunit;

namespace PharmacyApp.Infrastructure.Tests;

public class BCryptPasswordHasherTests
{
    private readonly BCryptPasswordHasher _hasher = new();

    [Fact]
    public void HashPassword_GeneratesSaltedHash_WithWorkFactor12()
    {
        const string password = "TestPassword123!";

        string hash = _hasher.HashPassword(password);

        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
        // BCrypt with work factor 12 format: $2a$12$... or $2b$12$... or $2y$12$...
        Assert.StartsWith("$2", hash);
        Assert.Contains("$12$", hash);
    }

    [Fact]
    public void HashPassword_ForSamePasswordTwice_GeneratesDifferentHashesDueToSalting()
    {
        const string password = "ConsistentPassword456#";

        string hash1 = _hasher.HashPassword(password);
        string hash2 = _hasher.HashPassword(password);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ReturnsTrue()
    {
        const string password = "CorrectSecretPassword789$";
        string hash = _hasher.HashPassword(password);

        bool isValid = _hasher.VerifyPassword(password, hash);

        Assert.True(isValid);
    }

    [Fact]
    public void VerifyPassword_WithWrongPassword_ReturnsFalse()
    {
        const string password = "CorrectSecretPassword789$";
        string hash = _hasher.HashPassword(password);

        bool isValid = _hasher.VerifyPassword("IncorrectSecretPassword000%", hash);

        Assert.False(isValid);
    }

    [Theory]
    [InlineData("", "some_hash")]
    [InlineData("password", "")]
    [InlineData("password", "invalid_non_bcrypt_hash")]
    public void VerifyPassword_WithMalformedOrEmptyInputs_ReturnsFalseSafely(string password, string hash)
    {
        bool isValid = _hasher.VerifyPassword(password, hash);

        Assert.False(isValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void HashPassword_WithEmptyOrNull_ThrowsArgumentException(string? password)
    {
        Assert.Throws<ArgumentException>(() => _hasher.HashPassword(password!));
    }

    [Fact]
    public void HashPassword_ExceedingMaxLength_ThrowsArgumentException()
    {
        string oversizedPassword = new string('A', 129);
        var ex = Assert.Throws<ArgumentException>(() => _hasher.HashPassword(oversizedPassword));
        Assert.Contains("cannot exceed 128", ex.Message);
    }

    [Fact]
    public void VerifyPassword_ExceedingMaxLength_ReturnsFalseWithoutException()
    {
        string validHash = _hasher.HashPassword("ValidPassword123!");
        string oversizedPassword = new string('A', 1000);

        bool isValid = _hasher.VerifyPassword(oversizedPassword, validHash);

        Assert.False(isValid);
    }
}
