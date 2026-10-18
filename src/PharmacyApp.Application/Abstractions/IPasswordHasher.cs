namespace PharmacyApp.Application.Abstractions;

/// <summary>
/// Provides secure, one-way password hashing and verification.
/// Never stores or logs plaintext passwords.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Computes a secure one-way salted hash from a plaintext password.
    /// </summary>
    string HashPassword(string password);

    /// <summary>
    /// Verifies a plaintext password candidate against an existing hash.
    /// </summary>
    bool VerifyPassword(string password, string passwordHash);
}
