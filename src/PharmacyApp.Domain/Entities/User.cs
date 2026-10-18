using PharmacyApp.Domain.Common;
using PharmacyApp.Domain.Enums;

namespace PharmacyApp.Domain.Entities;

/// <summary>
/// Represents a user account authorized to access the system.
/// Sensitive credentials are never stored in plaintext.
/// </summary>
public sealed class User : EntityBase
{
    public string Username { get; private set; }
    public string DisplayName { get; private set; }
    public string PasswordHash { get; private set; }
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }

    // Private constructor for factory and persistence mapping
    private User(
        Guid id,
        string username,
        string displayName,
        string passwordHash,
        UserRole role,
        bool isActive,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        uint version)
    {
        Id = id;
        Username = username;
        DisplayName = displayName;
        PasswordHash = passwordHash;
        Role = role;
        IsActive = isActive;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        Version = version;
    }

    /// <summary>
    /// Factory method to create a new user entity.
    /// </summary>
    public static User Create(
        string username,
        string displayName,
        string passwordHash,
        UserRole role,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username cannot be empty.", nameof(username));

        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name cannot be empty.", nameof(displayName));

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash cannot be empty.", nameof(passwordHash));

        return new User(
            id: Guid.NewGuid(),
            username: username.Trim().ToLowerInvariant(),
            displayName: displayName.Trim(),
            passwordHash: passwordHash,
            role: role,
            isActive: true,
            createdAt: now,
            updatedAt: now,
            version: 1);
    }

    /// <summary>
    /// Factory method used when hydrating an existing user from persistent storage.
    /// </summary>
    public static User Load(
        Guid id,
        string username,
        string displayName,
        string passwordHash,
        UserRole role,
        bool isActive,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        uint version)
    {
        return new User(id, username, displayName, passwordHash, role, isActive, createdAt, updatedAt, version);
    }

    public void UpdatePassword(string newPasswordHash, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new ArgumentException("Password hash cannot be empty.", nameof(newPasswordHash));

        PasswordHash = newPasswordHash;
        UpdatedAt = now;
        Version++;
    }

    public void Deactivate(DateTimeOffset now)
    {
        IsActive = false;
        UpdatedAt = now;
        Version++;
    }

    public void Activate(DateTimeOffset now)
    {
        IsActive = true;
        UpdatedAt = now;
        Version++;
    }
}
