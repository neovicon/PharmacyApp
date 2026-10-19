using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Authentication;

namespace PharmacyApp.Infrastructure.Security;

/// <summary>
/// BCrypt password hasher with work factor 12.
/// Uses salted, one-way cryptographic hashing to prevent rainbow table attacks.
/// </summary>
public sealed class BCryptPasswordHasher : IPasswordHasher
{
    // Work factor 12 is recommended for modern systems: takes ~250-300ms per hash,
    // which effectively throttles offline brute-force attempts without noticeably slowing desktop login.
    public const int WorkFactor = 12;

    public string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password cannot be null or empty.", nameof(password));

        if (password.Length > PasswordPolicy.MaxLength)
            throw new ArgumentException($"Password cannot exceed {PasswordPolicy.MaxLength} characters.", nameof(password));

        return BCrypt.Net.BCrypt.EnhancedHashPassword(password, WorkFactor);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(passwordHash))
            return false;

        if (password.Length > PasswordPolicy.MaxLength)
            return false;

        try
        {
            return BCrypt.Net.BCrypt.EnhancedVerify(password, passwordHash);
        }
        catch
        {
            return false;
        }
    }
}
