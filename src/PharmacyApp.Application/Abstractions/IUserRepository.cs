using PharmacyApp.Domain.Entities;

namespace PharmacyApp.Application.Abstractions;

/// <summary>
/// Abstraction for persistent storage and retrieval of user accounts.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Checks whether any user accounts currently exist in storage.
    /// Used for first-run administrator detection.
    /// </summary>
    Task<bool> HasAnyUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an active user by their login username (case-insensitive).
    /// </summary>
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a user by their unique identifier.
    /// </summary>
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a newly created user account.
    /// </summary>
    Task AddAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing user account.
    /// </summary>
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically persists the initial administrator account if and only if no users exist in storage.
    /// Returns true if the user was successfully created as the first user; false if any user already exists.
    /// </summary>
    Task<bool> TryCreateInitialAdminAsync(User adminUser, CancellationToken cancellationToken = default);
}
