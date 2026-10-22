using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace PharmacyApp.Infrastructure.Persistence;

/// <summary>
/// Provides WAL-safe, verified SQLite backups using SQLite's native VACUUM INTO.
/// Ensures 100% data preservation and full-record user authentication verification.
/// </summary>
public sealed class SqliteBackupService : ISqliteBackupService
{
    private readonly Func<string, long, bool>? _diskSpaceChecker;

    public SqliteBackupService(Func<string, long, bool>? diskSpaceChecker = null)
    {
        _diskSpaceChecker = diskSpaceChecker;
    }

    public async Task<string> CreateBackupAsync(
        string sourceConnectionString,
        string targetBackupPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceConnectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetBackupPath);

        var sourceBuilder = new SqliteConnectionStringBuilder(sourceConnectionString);
        string sourceDbPath = sourceBuilder.DataSource;

        if (string.IsNullOrWhiteSpace(sourceDbPath))
        {
            throw new ArgumentException("Source connection string does not specify a valid DataSource.", nameof(sourceConnectionString));
        }

        if (File.Exists(targetBackupPath))
        {
            throw new InvalidOperationException(
                $"Backup target file already exists at '{targetBackupPath}'. Backups must never overwrite existing files.");
        }

        string? targetDir = Path.GetDirectoryName(Path.GetFullPath(targetBackupPath));
        if (!string.IsNullOrEmpty(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        // Validate available disk space
        if (File.Exists(sourceDbPath) && !string.IsNullOrEmpty(targetDir))
        {
            long sourceSize = new FileInfo(sourceDbPath).Length;
            long requiredBytes = Math.Max(sourceSize * 2, 10 * 1024 * 1024); // 2x source size or 10 MB minimum

            if (_diskSpaceChecker != null)
            {
                if (!_diskSpaceChecker(targetDir, requiredBytes))
                {
                    throw new InvalidOperationException(
                        $"Insufficient disk space for backup in '{targetDir}'. Required: {requiredBytes} bytes.");
                }
            }
            else
            {
                try
                {
                    var drive = new DriveInfo(targetDir);
                    if (drive.IsReady && drive.AvailableFreeSpace < requiredBytes)
                    {
                        throw new InvalidOperationException(
                            $"Insufficient disk space for backup in '{targetDir}'. Required: {requiredBytes} bytes, Available: {drive.AvailableFreeSpace} bytes.");
                    }
                }
                catch (Exception ex) when (ex is not InvalidOperationException)
                {
                    // Proceed if DriveInfo is unsupported for this mount path
                }
            }
        }

        // Execute VACUUM INTO
        try
        {
            await using (var connection = new SqliteConnection(sourceConnectionString))
            {
                await connection.OpenAsync(cancellationToken);

                await using var cmd = connection.CreateCommand();
                cmd.CommandText = "VACUUM INTO $backupPath;";
                cmd.Parameters.AddWithValue("$backupPath", targetBackupPath);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // Verify the generated backup
            await VerifyBackupAsync(sourceConnectionString, targetBackupPath, cancellationToken);

            return targetBackupPath;
        }
        catch
        {
            // Clean up unverified or partially written backup file on failure
            if (File.Exists(targetBackupPath))
            {
                try
                {
                    SqliteConnection.ClearAllPools();
                    File.Delete(targetBackupPath);
                }
                catch
                {
                    // Best effort cleanup
                }
            }

            throw;
        }
    }

    public async Task VerifyBackupAsync(
        string sourceConnectionString,
        string backupPath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(backupPath))
        {
            throw new FileNotFoundException($"Backup file does not exist at '{backupPath}'.", backupPath);
        }

        // Open read-only connection to the backup database
        var backupBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = backupPath,
            Mode = SqliteOpenMode.ReadOnly,
            ForeignKeys = true,
            DefaultTimeout = 5
        };

        await using var backupConn = new SqliteConnection(backupBuilder.ConnectionString);
        await backupConn.OpenAsync(cancellationToken);

        // 1. Check database integrity
        await using (var integrityCmd = backupConn.CreateCommand())
        {
            integrityCmd.CommandText = "PRAGMA integrity_check;";
            var integrityResult = (string?)await integrityCmd.ExecuteScalarAsync(cancellationToken);
            if (!string.Equals(integrityResult, "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Backup failed PRAGMA integrity_check: {integrityResult}");
            }
        }

        // Open read-only connection to the source database
        var sourceBuilder = new SqliteConnectionStringBuilder(sourceConnectionString)
        {
            Mode = SqliteOpenMode.ReadOnly,
            ForeignKeys = true,
            DefaultTimeout = 5
        };

        await using var sourceConn = new SqliteConnection(sourceBuilder.ConnectionString);
        await sourceConn.OpenAsync(cancellationToken);

        // 2. Compare table list
        var sourceTables = await GetTableNamesAsync(sourceConn, cancellationToken);
        var backupTables = await GetTableNamesAsync(backupConn, cancellationToken);

        foreach (string sourceTable in sourceTables)
        {
            if (!backupTables.Contains(sourceTable))
            {
                throw new InvalidOperationException($"Backup is missing table '{sourceTable}' present in source database.");
            }
        }

        // 3. Compare row counts for each table
        foreach (string table in sourceTables)
        {
            long sourceCount = await GetTableRowCountAsync(sourceConn, table, cancellationToken);
            long backupCount = await GetTableRowCountAsync(backupConn, table, cancellationToken);

            if (sourceCount != backupCount)
            {
                throw new InvalidOperationException(
                    $"Row count mismatch for table '{table}': source has {sourceCount}, backup has {backupCount}.");
            }
        }

        // 4. Exact full-record preservation verification for Users
        if (sourceTables.Contains("Users"))
        {
            var sourceUsers = await GetUserRecordsAsync(sourceConn, cancellationToken);
            var backupUsers = await GetUserRecordsAsync(backupConn, cancellationToken);

            if (sourceUsers.Count != backupUsers.Count)
            {
                throw new InvalidOperationException(
                    $"Backup user count mismatch: source has {sourceUsers.Count}, backup has {backupUsers.Count}.");
            }

            for (int i = 0; i < sourceUsers.Count; i++)
            {
                var s = sourceUsers[i];
                var b = backupUsers[i];

                if (!string.Equals(s.Id, b.Id, StringComparison.Ordinal) ||
                    !string.Equals(s.Username, b.Username, StringComparison.Ordinal) ||
                    !string.Equals(s.DisplayName, b.DisplayName, StringComparison.Ordinal) ||
                    !string.Equals(s.PasswordHash, b.PasswordHash, StringComparison.Ordinal) ||
                    s.Role != b.Role ||
                    s.IsActive != b.IsActive ||
                    !string.Equals(s.CreatedAt, b.CreatedAt, StringComparison.Ordinal) ||
                    !string.Equals(s.UpdatedAt, b.UpdatedAt, StringComparison.Ordinal) ||
                    s.Version != b.Version)
                {
                    // Fail closed without logging secret hashes
                    throw new InvalidOperationException(
                        $"Backup verification failed: user record mismatch detected for user ID '{s.Id}'.");
                }
            }
        }
    }

    private static async Task<HashSet<string>> GetTableNamesAsync(SqliteConnection conn, CancellationToken ct)
    {
        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            tables.Add(reader.GetString(0));
        }
        return tables;
    }

    private static async Task<long> GetTableRowCountAsync(SqliteConnection conn, string tableName, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        // Quote table name safely against injection
        cmd.CommandText = $"SELECT COUNT(*) FROM \"{tableName.Replace("\"", "\"\"")}\";";
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(result);
    }

    private sealed record UserRecord(
        string Id,
        string Username,
        string DisplayName,
        string PasswordHash,
        long Role,
        long IsActive,
        string CreatedAt,
        string UpdatedAt,
        long Version);

    private static async Task<List<UserRecord>> GetUserRecordsAsync(SqliteConnection conn, CancellationToken ct)
    {
        var list = new List<UserRecord>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Id, Username, DisplayName, PasswordHash, Role, IsActive, CreatedAt, UpdatedAt, Version
            FROM Users
            ORDER BY Id;
            """;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new UserRecord(
                Id: reader.GetString(0),
                Username: reader.GetString(1),
                DisplayName: reader.GetString(2),
                PasswordHash: reader.GetString(3),
                Role: reader.GetInt64(4),
                IsActive: reader.GetInt64(5),
                CreatedAt: reader.GetString(6),
                UpdatedAt: reader.GetString(7),
                Version: reader.GetInt64(8)
            ));
        }

        return list;
    }
}
