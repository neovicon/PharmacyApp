using PharmacyApp.Application.Common;

namespace PharmacyApp.Application.Authentication;

/// <summary>
/// Enforces sensible password security standards for local accounts.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    public static Result Validate(string? password, string? confirmPassword = null)
    {
        if (string.IsNullOrEmpty(password))
            return Result.Failure("Password cannot be empty.");

        if (password.Length < MinLength)
            return Result.Failure($"Password must be at least {MinLength} characters long.");

        if (password.Length > MaxLength)
            return Result.Failure($"Password cannot exceed {MaxLength} characters.");

        bool hasLetter = password.Any(char.IsLetter);
        bool hasDigitOrSymbol = password.Any(c => char.IsDigit(c) || char.IsPunctuation(c) || char.IsSymbol(c));

        if (!hasLetter || !hasDigitOrSymbol)
            return Result.Failure("Password must contain at least one letter and one number or special character.");

        if (confirmPassword is not null && !string.Equals(password, confirmPassword, StringComparison.Ordinal))
            return Result.Failure("Passwords do not match.");

        return Result.Success();
    }
}
