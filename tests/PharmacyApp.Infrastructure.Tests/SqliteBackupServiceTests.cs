using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using PharmacyApp.Infrastructure.Persistence;
using Xunit;

namespace PharmacyApp.Infrastructure.Tests;

public sealed class SqliteBackupServiceTests : IDisposable
{
    private readonly IsolatedTestPathProvider _pathProvider;
    private readonly string _sourceDbPath;
    private readonly string _sourceConnectionString;
    private readonly SqliteBackupService _backupService;

    public SqliteBackupServiceTests()
    {
        _pathProvider = new IsolatedTestPathProvider();
        _sourceDbPath = Path.Combine(_pathProvider.DataDirectory, "source.db");
        _sourceConnectionString = $"Data Source={_sourceDbPath};Cache=Shared;Foreign Keys=True;Default Timeout=5;";
        _backupService = new SqliteBackupService();

        // Create sample WAL-enabled database with Users table
        using var conn = new SqliteConnection(_sourceConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode = WAL;
            PRAGMA foreign_keys = ON;

            CREATE TABLE Users (
                Id TEXT PRIMARY KEY,
                Username TEXT NOT NULL UNIQUE COLLATE NOCASE,
                DisplayName TEXT NOT NULL,
                PasswordHash TEXT NOT NULL,
                Role INTEGER NOT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                Version INTEGER NOT NULL DEFAULT 1
            );
            CREATE INDEX IX_Users_Username ON Users(Username);

            INSERT INTO Users (Id, Username, DisplayName, PasswordHash, Role, IsActive, CreatedAt, UpdatedAt, Version)
            VALUES (
                'user-1',
                'admin_user',
                'Admin User',
                '$2a$12$super_secret_hash_value_1234567890abcdef',
                0,
                1,
                '2026-10-19T10:00:00Z',
                '2026-10-19T10:00:00Z',
                1
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        _pathProvider.Dispose();
    }

    [Fact]
    public async Task CreateBackupAsync_CreatesValidBackup_AndPreservesWalData()
    {
        string targetBackupPath = Path.Combine(_pathProvider.DataDirectory, "Backups", "backup1.bak");

        string resultPath = await _backupService.CreateBackupAsync(_sourceConnectionString, targetBackupPath);

        Assert.Equal(targetBackupPath, resultPath);
        Assert.True(File.Exists(targetBackupPath));

        // Verify backup is a valid SQLite DB with integrity_check = ok
        using var backupConn = new SqliteConnection($"Data Source={targetBackupPath};Mode=ReadOnly;");
        backupConn.Open();
        using var cmd = backupConn.CreateCommand();
        cmd.CommandText = "PRAGMA integrity_check;";
        var integrity = (string?)cmd.ExecuteScalar();
        Assert.Equal("ok", integrity);

        cmd.CommandText = "SELECT COUNT(*) FROM Users;";
        long userCount = Convert.ToInt64(cmd.ExecuteScalar());
        Assert.Equal(1L, userCount);
    }

    [Fact]
    public async Task CreateBackupAsync_PreservesCompleteUserRecords_IncludingHashAndTimestamps()
    {
        // Insert a second user into source
        using (var conn = new SqliteConnection(_sourceConnectionString))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO Users (Id, Username, DisplayName, PasswordHash, Role, IsActive, CreatedAt, UpdatedAt, Version)
                VALUES (
                    'user-2',
                    'pharmacist_jane',
                    'Jane Pharmacist',
                    '$2a$12$another_secret_hash_pharmacist_xyz',
                    1,
                    1,
                    '2026-10-19T11:30:00Z',
                    '2026-10-19T11:30:00Z',
                    2
                );
                """;
            cmd.ExecuteNonQuery();
        }

        string targetBackupPath = Path.Combine(_pathProvider.DataDirectory, "Backups", "backup_users.bak");
        await _backupService.CreateBackupAsync(_sourceConnectionString, targetBackupPath);

        // Verify user 2 in backup matches exactly
        using var backupConn = new SqliteConnection($"Data Source={targetBackupPath};Mode=ReadOnly;");
        backupConn.Open();
        using var verifyCmd = backupConn.CreateCommand();
        verifyCmd.CommandText = """
            SELECT Id, Username, DisplayName, PasswordHash, Role, IsActive, CreatedAt, UpdatedAt, Version
            FROM Users WHERE Id = 'user-2';
            """;
        using var reader = verifyCmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("user-2", reader.GetString(0));
        Assert.Equal("pharmacist_jane", reader.GetString(1));
        Assert.Equal("Jane Pharmacist", reader.GetString(2));
        Assert.Equal("$2a$12$another_secret_hash_pharmacist_xyz", reader.GetString(3));
        Assert.Equal(1L, reader.GetInt64(4));
        Assert.Equal(1L, reader.GetInt64(5));
        Assert.Equal("2026-10-19T11:30:00Z", reader.GetString(6));
        Assert.Equal("2026-10-19T11:30:00Z", reader.GetString(7));
        Assert.Equal(2L, reader.GetInt64(8));
    }

    [Fact]
    public async Task CreateBackupAsync_WhenTargetAlreadyExists_ThrowsAndDoesNotOverwrite()
    {
        string targetBackupPath = Path.Combine(_pathProvider.DataDirectory, "Backups", "existing.bak");
        Directory.CreateDirectory(Path.GetDirectoryName(targetBackupPath)!);
        await File.WriteAllTextAsync(targetBackupPath, "pre-existing content");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _backupService.CreateBackupAsync(_sourceConnectionString, targetBackupPath));

        Assert.Contains("already exists", ex.Message);
        Assert.Equal("pre-existing content", await File.ReadAllTextAsync(targetBackupPath));
    }

    [Fact]
    public async Task CreateBackupAsync_WhenInsufficientDiskSpace_FailsClosed_AndCleansUp()
    {
        var lowSpaceService = new SqliteBackupService((dir, required) => false);
        string targetBackupPath = Path.Combine(_pathProvider.DataDirectory, "Backups", "fail_space.bak");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            lowSpaceService.CreateBackupAsync(_sourceConnectionString, targetBackupPath));

        Assert.Contains("Insufficient disk space", ex.Message);
        Assert.False(File.Exists(targetBackupPath));
    }

    [Fact]
    public async Task VerifyBackupAsync_WhenIntegrityCorrupted_ThrowsInvalidOperationException()
    {
        string targetBackupPath = Path.Combine(_pathProvider.DataDirectory, "Backups", "to_corrupt.bak");
        await _backupService.CreateBackupAsync(_sourceConnectionString, targetBackupPath);

        // Corrupt backup file by overwriting bytes inside
        SqliteConnection.ClearAllPools();
        using (var fs = new FileStream(targetBackupPath, FileMode.Open, FileAccess.ReadWrite))
        {
            fs.Seek(100, SeekOrigin.Begin);
            fs.Write(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF });
        }

        await Assert.ThrowsAnyAsync<Exception>(() =>
            _backupService.VerifyBackupAsync(_sourceConnectionString, targetBackupPath));
    }

    [Fact]
    public async Task VerifyBackupAsync_WhenUserRecordModified_Throws_WithoutExposingPasswordHash()
    {
        string targetBackupPath = Path.Combine(_pathProvider.DataDirectory, "Backups", "tampered_user.bak");
        await _backupService.CreateBackupAsync(_sourceConnectionString, targetBackupPath);

        // Tamper with password hash in the backup
        SqliteConnection.ClearAllPools();
        using (var backupConn = new SqliteConnection($"Data Source={targetBackupPath};"))
        {
            backupConn.Open();
            using var cmd = backupConn.CreateCommand();
            cmd.CommandText = "UPDATE Users SET PasswordHash = 'tampered_hash_value' WHERE Id = 'user-1';";
            cmd.ExecuteNonQuery();
        }

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _backupService.VerifyBackupAsync(_sourceConnectionString, targetBackupPath));

        Assert.Contains("Backup verification failed: user record mismatch detected", ex.Message);
        // Guarantee secret password hashes are not leaked in the exception message
        Assert.DoesNotContain("super_secret_hash_value", ex.Message);
        Assert.DoesNotContain("tampered_hash_value", ex.Message);
    }
}
