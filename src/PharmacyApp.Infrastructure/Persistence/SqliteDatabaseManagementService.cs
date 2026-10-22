using Microsoft.Data.Sqlite;
using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Settings;

namespace PharmacyApp.Infrastructure.Persistence;

/// <summary>
/// SQLite implementation of <see cref="IDatabaseManagementService"/>.
/// Provides database diagnostics and triggers WAL-safe verified backups.
/// </summary>
public sealed class SqliteDatabaseManagementService : IDatabaseManagementService
{
    private readonly string _connectionString;
    private readonly IAppPathProvider _pathProvider;
    private readonly ISqliteBackupService _backupService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public SqliteDatabaseManagementService(
        string connectionString,
        IAppPathProvider pathProvider,
        ISqliteBackupService backupService,
        IDateTimeProvider dateTimeProvider)
    {
        _connectionString = connectionString;
        _pathProvider = pathProvider;
        _backupService = backupService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<DatabaseStatusDto> GetDatabaseStatusAsync(CancellationToken cancellationToken = default)
    {
        var builder = new SqliteConnectionStringBuilder(_connectionString);
        string dbPath = builder.DataSource;
        long dbSizeBytes = 0;

        if (!string.IsNullOrEmpty(dbPath) && File.Exists(dbPath))
        {
            dbSizeBytes = new FileInfo(dbPath).Length;
        }

        string journalMode = "UNKNOWN";
        bool foreignKeysEnabled = false;

        await using (var conn = new SqliteConnection(_connectionString))
        {
            await conn.OpenAsync(cancellationToken);

            await using (var jmCmd = conn.CreateCommand())
            {
                jmCmd.CommandText = "PRAGMA journal_mode;";
                var result = await jmCmd.ExecuteScalarAsync(cancellationToken);
                journalMode = result?.ToString()?.ToUpperInvariant() ?? "UNKNOWN";
            }

            await using (var fkCmd = conn.CreateCommand())
            {
                fkCmd.CommandText = "PRAGMA foreign_keys;";
                var fkResult = await fkCmd.ExecuteScalarAsync(cancellationToken);
                foreignKeysEnabled = Convert.ToInt32(fkResult) == 1;
            }
        }

        string backupsDir = Path.Combine(_pathProvider.DataDirectory, "backups");
        var backupList = new List<BackupInfoDto>();
        long totalBackupSizeBytes = 0;
        DateTimeOffset? latestBackupTime = null;

        if (Directory.Exists(backupsDir))
        {
            var files = Directory.GetFiles(backupsDir, "*.db")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTimeUtc);

            foreach (var fi in files)
            {
                backupList.Add(new BackupInfoDto(
                    fi.Name,
                    fi.FullName,
                    fi.Length,
                    new DateTimeOffset(fi.CreationTimeUtc)));
                totalBackupSizeBytes += fi.Length;
            }

            if (backupList.Count > 0)
            {
                latestBackupTime = backupList[0].CreatedAt;
            }
        }

        return new DatabaseStatusDto(
            dbPath,
            dbSizeBytes,
            journalMode,
            foreignKeysEnabled,
            backupList.Count,
            totalBackupSizeBytes,
            latestBackupTime,
            backupList);
    }

    public async Task<BackupInfoDto> CreateBackupAsync(CancellationToken cancellationToken = default)
    {
        string backupsDir = Path.Combine(_pathProvider.DataDirectory, "backups");
        Directory.CreateDirectory(backupsDir);

        string targetBackupPath = Path.Combine(
            backupsDir,
            $"pharmacy_backup_{_dateTimeProvider.UtcNow:yyyyMMdd_HHmmss}.db");

        await _backupService.CreateBackupAsync(_connectionString, targetBackupPath, cancellationToken);

        var fi = new FileInfo(targetBackupPath);
        return new BackupInfoDto(
            fi.Name,
            fi.FullName,
            fi.Length,
            new DateTimeOffset(fi.CreationTimeUtc));
    }
}
