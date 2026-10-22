using PharmacyApp.Domain.Entities;
using PharmacyApp.Domain.Enums;

namespace PharmacyApp.Application.Users;

/// <summary>
/// Data transfer object for user accounts, omitting credentials.
/// </summary>
public sealed record UserDto(
    Guid Id,
    string Username,
    string DisplayName,
    UserRole Role,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint Version)
{
    public static UserDto FromDomain(User user) => new(
        user.Id,
        user.Username,
        user.DisplayName,
        user.Role,
        user.IsActive,
        user.CreatedAt,
        user.UpdatedAt,
        user.Version);
}
