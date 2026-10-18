using PharmacyApp.Application.Authentication;
using Xunit;

namespace PharmacyApp.Application.Tests;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("Pass1234", "Pass1234")]
    [InlineData("Super#Secret1", "Super#Secret1")]
    [InlineData("Ph@rmacyApp2026", "Ph@rmacyApp2026")]
    [InlineData("Valid1Pass!", null)]
    public void Validate_ValidPasswords_ReturnsSuccess(string password, string? confirm)
    {
        var result = PasswordPolicy.Validate(password, confirm);
        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_NullOrEmptyPassword_ReturnsFailure(string? password)
    {
        var result = PasswordPolicy.Validate(password);
        Assert.True(result.IsFailure);
        Assert.Equal("Password cannot be empty.", result.Error);
    }

    [Theory]
    [InlineData("Ab1!")]
    [InlineData("Short1")]
    [InlineData("1234567")]
    public void Validate_TooShort_ReturnsFailure(string shortPassword)
    {
        var result = PasswordPolicy.Validate(shortPassword);
        Assert.True(result.IsFailure);
        Assert.Contains("at least 8 characters", result.Error);
    }

    [Fact]
    public void Validate_TooLong_ReturnsFailure()
    {
        string longPassword = new string('A', 125) + "1234!"; // 130 chars
        var result = PasswordPolicy.Validate(longPassword);
        Assert.True(result.IsFailure);
        Assert.Contains("cannot exceed 128 characters", result.Error);
    }

    [Fact]
    public void Validate_OnlyLetters_ReturnsFailure()
    {
        var result = PasswordPolicy.Validate("LettersOnlyPassword");
        Assert.True(result.IsFailure);
        Assert.Contains("at least one letter and one number or special character", result.Error);
    }

    [Fact]
    public void Validate_OnlyDigitsAndSymbols_ReturnsFailure()
    {
        var result = PasswordPolicy.Validate("123456789!@#$");
        Assert.True(result.IsFailure);
        Assert.Contains("at least one letter and one number or special character", result.Error);
    }

    [Fact]
    public void Validate_MismatchedConfirmation_ReturnsFailure()
    {
        var result = PasswordPolicy.Validate("ValidPass123", "DifferentPass123");
        Assert.True(result.IsFailure);
        Assert.Equal("Passwords do not match.", result.Error);
    }
}
