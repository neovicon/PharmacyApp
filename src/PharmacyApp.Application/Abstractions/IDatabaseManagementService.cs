using PharmacyApp.Application.Settings;

namespace PharmacyApp.Application.Abstractions;

/// <summary>
/// Service providing database diagnostics and WAL-safe manual backups.
/// </summary>
public interface IDatabaseManagementService
{
    /// <summary>
    /// Retrieves file size, journal mode, foreign key enforcement, and backup history.
    /// </summary>
    Task<DatabaseStatusDto> GetDatabaseStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a verified, WAL-safe SQLite backup into the backups directory.
    /// </summary>
    Task<BackupInfoDto> CreateBackupAsync(CancellationToken cancellationToken = default);
}
