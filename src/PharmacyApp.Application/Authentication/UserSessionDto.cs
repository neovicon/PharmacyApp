using PharmacyApp.Domain.Enums;

namespace PharmacyApp.Application.Authentication;

/// <summary>
/// Immutable snapshot representing an authenticated user's session identity.
/// Contains no passwords or credential hashes.
/// </summary>
public sealed record UserSessionDto(
    Guid UserId,
    string Username,
    string DisplayName,
    UserRole Role);
