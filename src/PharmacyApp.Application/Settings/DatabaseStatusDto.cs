namespace PharmacyApp.Application.Settings;

/// <summary>
/// Status and health diagnostics for the underlying SQLite database and backup store.
/// </summary>
public sealed record DatabaseStatusDto(
    string DatabasePath,
    long DatabaseSizeBytes,
    string JournalMode,
    bool ForeignKeysEnabled,
    int BackupCount,
    long TotalBackupSizeBytes,
    DateTimeOffset? LatestBackupTime,
    IReadOnlyList<BackupInfoDto> Backups);

/// <summary>
/// Metadata for an individual SQLite backup file.
/// </summary>
public sealed record BackupInfoDto(
    string FileName,
    string FilePath,
    long SizeBytes,
    DateTimeOffset CreatedAt);
