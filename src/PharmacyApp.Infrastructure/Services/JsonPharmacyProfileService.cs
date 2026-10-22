using System.Text.Json;
using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Settings;

namespace PharmacyApp.Infrastructure.Services;

/// <summary>
/// JSON file-backed implementation of <see cref="IPharmacyProfileService"/>.
/// Persists store profile settings to store_profile.json in the data directory.
/// </summary>
public sealed class JsonPharmacyProfileService : IPharmacyProfileService
{
    private readonly IAppPathProvider _pathProvider;
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public JsonPharmacyProfileService(IAppPathProvider pathProvider)
    {
        _pathProvider = pathProvider;
        _filePath = Path.Combine(_pathProvider.DataDirectory, "store_profile.json");
    }

    public async Task<PharmacyProfileDto> GetProfileAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_filePath))
            {
                var defaultProfile = new PharmacyProfileDto();
                await SaveInternalAsync(defaultProfile, cancellationToken);
                return defaultProfile;
            }

            string json = await File.ReadAllTextAsync(_filePath, cancellationToken);
            var profile = JsonSerializer.Deserialize<PharmacyProfileDto>(json, JsonOptions);
            return profile ?? new PharmacyProfileDto();
        }
        catch
        {
            return new PharmacyProfileDto();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveProfileAsync(PharmacyProfileDto profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            await SaveInternalAsync(profile, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task SaveInternalAsync(PharmacyProfileDto profile, CancellationToken cancellationToken)
    {
        string? dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string json = JsonSerializer.Serialize(profile, JsonOptions);
        await File.WriteAllTextAsync(_filePath, json, cancellationToken);
    }
}
