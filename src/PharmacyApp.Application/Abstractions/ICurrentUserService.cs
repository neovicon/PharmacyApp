using PharmacyApp.Domain.Enums;

namespace PharmacyApp.Application.Abstractions;

/// <summary>
/// Provides the identity of the currently authenticated user to application
/// services and future audit infrastructure.
///
/// Implementation is registered by the Desktop layer; the Application layer
/// depends only on this interface.
///
/// MILESTONE 0.5: This interface is defined here now so that future handlers
/// can depend on it without breaking changes. A real implementation backed by
/// secure authentication will be provided in Milestone 0.5.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>Unique identifier of the authenticated user.</summary>
    Guid UserId { get; }

    /// <summary>Login username.</summary>
    string Username { get; }

    /// <summary>Human-readable display name.</summary>
    string DisplayName { get; }

    /// <summary>Role controlling feature access.</summary>
    UserRole Role { get; }

    /// <summary>
    /// True when a user is actively authenticated.
    /// Business operations must not proceed when false.
    /// Enforced by the shell in Milestone 0.5.
    /// </summary>
    bool IsAuthenticated { get; }
}
