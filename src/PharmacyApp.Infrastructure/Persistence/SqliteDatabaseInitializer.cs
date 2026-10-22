using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using PharmacyApp.Infrastructure.Persistence.Migrations;

namespace PharmacyApp.Infrastructure.Persistence;

/// <summary>
/// Initializes and migrates the local SQLite database schema safely.
/// Enforces WAL mode, foreign keys, busy timeouts, pre-write backups,
/// and fail-closed versioned schema migrations.
/// </summary>
public sealed class SqliteDatabaseInitializer
{
    private readonly string _connectionString;
    private readonly ISqliteBackupService _backupService;

    public SqliteDatabaseInitializer(
        string connectionString,
        ISqliteBackupService? backupService = null)
    {
        _connectionString = connectionString;
        _backupService = backupService ?? new SqliteBackupService();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Configure connection-local settings without modifying persistent database header state
        await using (var pragmaCmd = connection.CreateCommand())
        {
            pragmaCmd.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;";
            await pragmaCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        var existingTables = await GetExistingTablesAsync(connection, cancellationToken);

        // Case 1: Fresh database (no user tables exist yet)
        if (existingTables.Count == 0)
        {
            await InitializeFreshDatabaseAsync(connection, cancellationToken);
            return;
        }

        // Case 2: Existing database
        bool hasMigrationsTable = existingTables.Contains("__SchemaMigrations");

        if (!hasMigrationsTable)
        {
            // Must have a valid existing authentication database (Users table)
            if (!existingTables.Contains("Users"))
            {
                throw new InvalidOperationException(
                    "Unexpected database state: database contains tables, but neither '__SchemaMigrations' nor 'Users' was found.");
            }

            // Validate Users schema against real DDL and PRAGMA rules
            await ValidateExistingUsersSchemaAsync(connection, cancellationToken);

            // MANDATORY SAFEGUARD: Create and verify backup BEFORE making the first write to the database!
            await CreatePreMigrationBackupIfNeededAsync(cancellationToken);

            // Configure WAL mode for the database only after verified backup completes
            await using (var walCmd = connection.CreateCommand())
            {
                walCmd.CommandText = "PRAGMA journal_mode = WAL;";
                await walCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // Create __SchemaMigrations table and baseline migration 1
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = """
                    CREATE TABLE __SchemaMigrations (
                        Version INTEGER PRIMARY KEY,
                        Description TEXT NOT NULL,
                        AppliedAtUtc TEXT NOT NULL
                    );

                    INSERT INTO __SchemaMigrations (Version, Description, AppliedAtUtc)
                    VALUES (1, '001_InitialAuth', $appliedAt);
                    """;
                cmd.Parameters.AddWithValue("$appliedAt", DateTimeOffset.UtcNow.ToString("O"));
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        else
        {
            // Existing migration-history database: ensure WAL mode is active
            await using (var walCmd = connection.CreateCommand())
            {
                walCmd.CommandText = "PRAGMA journal_mode = WAL;";
                await walCmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        // Read and validate applied migrations
        var appliedMigrations = await GetAppliedMigrationsAsync(connection, cancellationToken);
        ValidateMigrationHistory(appliedMigrations);

        int currentMaxVersion = appliedMigrations.Count > 0
            ? appliedMigrations.Max(m => m.Version)
            : 0;

        // Determine pending migrations
        var pendingMigrations = MigrationScripts.All
            .Where(m => m.Version > currentMaxVersion)
            .OrderBy(m => m.Version)
            .ToList();

        if (pendingMigrations.Count == 0)
        {
            return; // Already up to date
        }

        foreach (var migration in pendingMigrations)
        {
            // Fail-closed collision check: ensure no target objects exist prior to running this migration
            await CheckObjectCollisionsAsync(connection, migration, cancellationToken);

            // Apply migration inside an immediate transaction
            await using var tx = connection.BeginTransaction(IsolationLevel.Serializable);
            try
            {
                await using (var migrationCmd = connection.CreateCommand())
                {
                    migrationCmd.Transaction = tx;
                    migrationCmd.CommandText = migration.UpSql;
                    await migrationCmd.ExecuteNonQueryAsync(cancellationToken);
                }

                await using (var recordCmd = connection.CreateCommand())
                {
                    recordCmd.Transaction = tx;
                    recordCmd.CommandText = """
                        INSERT INTO __SchemaMigrations (Version, Description, AppliedAtUtc)
                        VALUES ($version, $description, $appliedAt);
                        """;
                    recordCmd.Parameters.AddWithValue("$version", migration.Version);
                    recordCmd.Parameters.AddWithValue("$description", migration.Description);
                    recordCmd.Parameters.AddWithValue("$appliedAt", DateTimeOffset.UtcNow.ToString("O"));
                    await recordCmd.ExecuteNonQueryAsync(cancellationToken);
                }

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
    }

    private async Task CreatePreMigrationBackupIfNeededAsync(CancellationToken ct)
    {
        var builder = new SqliteConnectionStringBuilder(_connectionString);
        string dbPath = builder.DataSource;

        if (string.IsNullOrWhiteSpace(dbPath) ||
            string.Equals(dbPath, ":memory:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string dbDir = Path.GetDirectoryName(Path.GetFullPath(dbPath)) ?? "";
        string backupDir = Path.Combine(dbDir, "Backups");
        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string backupPath = Path.Combine(backupDir, $"pharmacy_pre_migration_{timestamp}_{suffix}.bak");

        await _backupService.CreateBackupAsync(_connectionString, backupPath, ct);
    }

    private static async Task InitializeFreshDatabaseAsync(SqliteConnection connection, CancellationToken ct)
    {
        // Configure WAL mode for fresh database
        await using (var walCmd = connection.CreateCommand())
        {
            walCmd.CommandText = "PRAGMA journal_mode = WAL;";
            await walCmd.ExecuteNonQueryAsync(ct);
        }

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                CREATE TABLE __SchemaMigrations (
                    Version INTEGER PRIMARY KEY,
                    Description TEXT NOT NULL,
                    AppliedAtUtc TEXT NOT NULL
                );
                """;
            await cmd.ExecuteNonQueryAsync(ct);
        }

        foreach (var migration in MigrationScripts.All.OrderBy(m => m.Version))
        {
            await using var tx = connection.BeginTransaction(IsolationLevel.Serializable);
            try
            {
                await using (var migrationCmd = connection.CreateCommand())
                {
                    migrationCmd.Transaction = tx;
                    migrationCmd.CommandText = migration.UpSql;
                    await migrationCmd.ExecuteNonQueryAsync(ct);
                }

                await using (var recordCmd = connection.CreateCommand())
                {
                    recordCmd.Transaction = tx;
                    recordCmd.CommandText = """
                        INSERT INTO __SchemaMigrations (Version, Description, AppliedAtUtc)
                        VALUES ($version, $description, $appliedAt);
                        """;
                    recordCmd.Parameters.AddWithValue("$version", migration.Version);
                    recordCmd.Parameters.AddWithValue("$description", migration.Description);
                    recordCmd.Parameters.AddWithValue("$appliedAt", DateTimeOffset.UtcNow.ToString("O"));
                    await recordCmd.ExecuteNonQueryAsync(ct);
                }

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
    }

    private static async Task<HashSet<string>> GetExistingTablesAsync(SqliteConnection conn, CancellationToken ct)
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

    private static async Task ValidateExistingUsersSchemaAsync(SqliteConnection connection, CancellationToken ct)
    {
        var columns = new Dictionary<string, (string Type, int NotNull, int Pk)>(StringComparer.OrdinalIgnoreCase);
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(Users);";
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                string name = reader.GetString(1);
                string type = reader.GetString(2);
                int notNull = reader.GetInt32(3);
                int pk = reader.GetInt32(5);
                columns[name] = (type, notNull, pk);
            }
        }

        if (columns.Count != 9)
        {
            throw new InvalidOperationException(
                $"Users table schema validation failed: expected 9 columns but found {columns.Count}.");
        }

        // Validate Id primary key (SQLite table_info yields notnull=0 for 'Id TEXT PRIMARY KEY')
        if (!columns.TryGetValue("Id", out var idCol) ||
            !string.Equals(idCol.Type, "TEXT", StringComparison.OrdinalIgnoreCase) ||
            idCol.Pk != 1)
        {
            throw new InvalidOperationException(
                "Users table schema validation failed: 'Id' must be TEXT PRIMARY KEY.");
        }

        var expectedColumns = new (string Name, string Type)[]
        {
            ("Username", "TEXT"),
            ("DisplayName", "TEXT"),
            ("PasswordHash", "TEXT"),
            ("Role", "INTEGER"),
            ("IsActive", "INTEGER"),
            ("CreatedAt", "TEXT"),
            ("UpdatedAt", "TEXT"),
            ("Version", "INTEGER")
        };

        foreach (var (colName, expectedType) in expectedColumns)
        {
            if (!columns.TryGetValue(colName, out var col) ||
                !string.Equals(col.Type, expectedType, StringComparison.OrdinalIgnoreCase) ||
                col.NotNull != 1)
            {
                throw new InvalidOperationException(
                    $"Users table schema validation failed: column '{colName}' expected {expectedType} NOT NULL.");
            }
        }

        // Validate DDL contains COLLATE NOCASE and UNIQUE for Username
        await using (var ddlCmd = connection.CreateCommand())
        {
            ddlCmd.CommandText = "SELECT sql FROM sqlite_master WHERE type='table' AND name='Users';";
            var ddl = (string?)await ddlCmd.ExecuteScalarAsync(ct);
            if (string.IsNullOrWhiteSpace(ddl) ||
                !ddl.Contains("COLLATE NOCASE", StringComparison.OrdinalIgnoreCase) ||
                !ddl.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Users table schema validation failed: Username must be defined with UNIQUE and COLLATE NOCASE.");
            }
        }
    }

    private static async Task CheckObjectCollisionsAsync(
        SqliteConnection connection,
        MigrationDefinition migration,
        CancellationToken ct)
    {
        foreach (string obj in migration.TargetObjects)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT type FROM sqlite_master WHERE name = $name;";
            cmd.Parameters.AddWithValue("$name", obj);
            var existingType = (string?)await cmd.ExecuteScalarAsync(ct);

            if (!string.IsNullOrEmpty(existingType))
            {
                throw new InvalidOperationException(
                    $"Migration {migration.Version} ('{migration.Description}') aborted: unexpected pre-existing {existingType} '{obj}' already exists in database. Aborting to prevent silent schema corruption.");
            }
        }
    }

    private sealed record MigrationRecord(int Version, string Description);

    private static async Task<List<MigrationRecord>> GetAppliedMigrationsAsync(SqliteConnection conn, CancellationToken ct)
    {
        var list = new List<MigrationRecord>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Version, Description FROM __SchemaMigrations ORDER BY Version;";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new MigrationRecord(reader.GetInt32(0), reader.GetString(1)));
        }
        return list;
    }

    private static void ValidateMigrationHistory(IReadOnlyList<MigrationRecord> applied)
    {
        var knownVersions = MigrationScripts.All.ToDictionary(m => m.Version);

        for (int i = 0; i < applied.Count; i++)
        {
            var record = applied[i];
            int expectedVersion = i + 1;

            // Detect non-contiguous version sequence gaps (e.g., [1, 3] or [2] missing 1)
            if (record.Version != expectedVersion && (applied.Any(r => r.Version == 1) || knownVersions.ContainsKey(record.Version)))
            {
                throw new InvalidOperationException(
                    $"Inconsistent migration history: gap detected in applied migrations. Expected version {expectedVersion} but found {record.Version}.");
            }

            if (!knownVersions.TryGetValue(record.Version, out _))
            {
                throw new InvalidOperationException(
                    $"Inconsistent migration history: database contains unknown migration version {record.Version} ('{record.Description}').");
            }

            if (record.Version != expectedVersion)
            {
                throw new InvalidOperationException(
                    $"Inconsistent migration history: gap detected in applied migrations. Expected version {expectedVersion} but found {record.Version}.");
            }
        }
    }
}
