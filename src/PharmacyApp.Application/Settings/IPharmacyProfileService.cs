namespace PharmacyApp.Application.Settings;

/// <summary>
/// Service for persisting and retrieving store profile and preferences.
/// </summary>
public interface IPharmacyProfileService
{
    Task<PharmacyProfileDto> GetProfileAsync(CancellationToken cancellationToken = default);
    Task SaveProfileAsync(PharmacyProfileDto profile, CancellationToken cancellationToken = default);
}
