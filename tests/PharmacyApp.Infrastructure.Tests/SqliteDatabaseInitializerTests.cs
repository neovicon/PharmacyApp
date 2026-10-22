using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using PharmacyApp.Infrastructure.Persistence;
using PharmacyApp.Infrastructure.Persistence.Migrations;
using Xunit;

namespace PharmacyApp.Infrastructure.Tests;

public sealed class SqliteDatabaseInitializerTests : IDisposable
{
    private readonly IsolatedTestPathProvider _pathProvider;
    private readonly string _dbPath;
    private readonly string _connectionString;

    public SqliteDatabaseInitializerTests()
    {
        _pathProvider = new IsolatedTestPathProvider();
        _dbPath = Path.Combine(_pathProvider.DataDirectory, "pharmacy.db");
        _connectionString = $"Data Source={_dbPath};Cache=Shared;Foreign Keys=True;Default Timeout=5;";
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        _pathProvider.Dispose();
    }

    [Fact]
    public async Task FreshDatabase_AppliesAllMigrations_InCorrectSequence()
    {
        var initializer = new SqliteDatabaseInitializer(_connectionString);
        await initializer.InitializeAsync();

        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        // Verify __SchemaMigrations
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Version, Description FROM __SchemaMigrations ORDER BY Version;";
        using (var reader = cmd.ExecuteReader())
        {
            Assert.True(reader.Read());
            Assert.Equal(1, reader.GetInt32(0));
            Assert.Equal("001_InitialAuth", reader.GetString(1));

            Assert.True(reader.Read());
            Assert.Equal(2, reader.GetInt32(0));
            Assert.Equal("002_MedicineCatalogueAndBatches", reader.GetString(1));

            Assert.True(reader.Read());
            Assert.Equal(3, reader.GetInt32(0));
            Assert.Equal("003_Suppliers", reader.GetString(1));

            Assert.True(reader.Read());
            Assert.Equal(4, reader.GetInt32(0));
            Assert.Equal("004_InvoicesAndSalesHistory", reader.GetString(1));

            Assert.False(reader.Read());
        }

        // Verify tables exist
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;";
        var tables = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                tables.Add(reader.GetString(0));
            }
        }

        Assert.Contains("Users", tables);
        Assert.Contains("Medicines", tables);
        Assert.Contains("Batches", tables);
        Assert.Contains("StockAdjustments", tables);
        Assert.Contains("Suppliers", tables);
        Assert.Contains("Invoices", tables);
        Assert.Contains("InvoiceItems", tables);
        Assert.Contains("__SchemaMigrations", tables);

        // Verify triggers exist
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='trigger' ORDER BY name;";
        var triggers = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                triggers.Add(reader.GetString(0));
            }
        }

        Assert.Contains("TR_StockAdjustments_PreventUpdate", triggers);
        Assert.Contains("TR_StockAdjustments_PreventDelete", triggers);
    }

    [Fact]
    public async Task RepeatedStartup_OnFreshDatabase_IsIdempotent()
    {
        var initializer = new SqliteDatabaseInitializer(_connectionString);
        await initializer.InitializeAsync();
        // Second call should succeed cleanly without throwing or duplicating
        await initializer.InitializeAsync();

        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM __SchemaMigrations;";
        long count = Convert.ToInt64(cmd.ExecuteScalar());
        Assert.Equal(4L, count);
    }

    [Fact]
    public async Task ExistingAuthDatabase_CreatesVerifiedBackup_BeforeFirstWrite_AndAppliesMigrations()
    {
        // 1. Create a realistic pre-migration authentication database (Milestone 0 state)
        using (var conn = new SqliteConnection(_connectionString))
        {
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
                    'admin-id-1',
                    'admin',
                    'Main Admin',
                    '$2a$12$AdminSecretHashValue1234567890abcdef',
                    0,
                    1,
                    '2026-10-18T18:00:00Z',
                    '2026-10-18T18:00:00Z',
                    1
                );
                """;
            cmd.ExecuteNonQuery();
        }

        // 2. Run initializer
        var initializer = new SqliteDatabaseInitializer(_connectionString);
        await initializer.InitializeAsync();

        // 3. Verify backup file was created in Backups directory
        string backupDir = Path.Combine(_pathProvider.DataDirectory, "Backups");
        Assert.True(Directory.Exists(backupDir));
        var backupFiles = Directory.GetFiles(backupDir, "pharmacy_pre_migration_*.bak");
        Assert.Single(backupFiles);

        // 4. Verify backup contains original user record
        using (var backupConn = new SqliteConnection($"Data Source={backupFiles[0]};Mode=ReadOnly;"))
        {
            backupConn.Open();
            using var backupCmd = backupConn.CreateCommand();
            backupCmd.CommandText = "SELECT Username, PasswordHash FROM Users WHERE Id = 'admin-id-1';";
            using var reader = backupCmd.ExecuteReader();
            Assert.True(reader.Read());
            Assert.Equal("admin", reader.GetString(0));
            Assert.Equal("$2a$12$AdminSecretHashValue1234567890abcdef", reader.GetString(1));
        }

        // 5. Verify live database is fully migrated and users are intact
        using (var liveConn = new SqliteConnection(_connectionString))
        {
            liveConn.Open();
            using var cmd = liveConn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Users;";
            Assert.Equal(1L, Convert.ToInt64(cmd.ExecuteScalar()));

            cmd.CommandText = "SELECT Version FROM __SchemaMigrations ORDER BY Version;";
            using var reader = cmd.ExecuteReader();
            Assert.True(reader.Read());
            Assert.Equal(1, reader.GetInt32(0));
            Assert.True(reader.Read());
            Assert.Equal(2, reader.GetInt32(0));
            Assert.True(reader.Read());
            Assert.Equal(3, reader.GetInt32(0));
            Assert.True(reader.Read());
            Assert.Equal(4, reader.GetInt32(0));
        }
    }

    [Fact]
    public async Task ExistingAuthDatabase_WhenBackupFails_AbortsBeforeFirstWrite()
    {
        // 1. Create pre-migration auth database
        using (var conn = new SqliteConnection(_connectionString))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
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
                INSERT INTO Users VALUES ('u-1', 'admin', 'Admin', 'hash', 0, 1, 'now', 'now', 1);
                """;
            cmd.ExecuteNonQuery();
        }

        // 2. Mock backup service that throws an exception
        var failingBackupService = new FailingBackupService();
        var initializer = new SqliteDatabaseInitializer(_connectionString, failingBackupService);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => initializer.InitializeAsync());
        Assert.Equal("Simulated backup failure", ex.Message);

        // 3. Verify NO writes occurred on live database:
        using (var conn = new SqliteConnection(_connectionString))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();

            // __SchemaMigrations must NOT exist
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE name = '__SchemaMigrations';";
            Assert.Null(cmd.ExecuteScalar());

            // Medicines must NOT exist
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE name = 'Medicines';";
            Assert.Null(cmd.ExecuteScalar());

            // User record is still intact
            cmd.CommandText = "SELECT COUNT(*) FROM Users;";
            Assert.Equal(1L, Convert.ToInt64(cmd.ExecuteScalar()));
        }
    }

    [Fact]
    public async Task IncompatibleSchema_MissingUsersOrWrongSchema_FailsClosed()
    {
        // Create an incompatible database with random table Foo
        using (var conn = new SqliteConnection(_connectionString))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE Foo (Id TEXT PRIMARY KEY);";
            cmd.ExecuteNonQuery();
        }

        var initializer = new SqliteDatabaseInitializer(_connectionString);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => initializer.InitializeAsync());
        Assert.Contains("neither '__SchemaMigrations' nor 'Users' was found", ex.Message);
    }

    [Fact]
    public async Task IncompatibleSchema_UsersTableMissingCollateNocase_FailsClosed()
    {
        // Missing COLLATE NOCASE
        using (var conn = new SqliteConnection(_connectionString))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE Users (
                    Id TEXT PRIMARY KEY,
                    Username TEXT NOT NULL UNIQUE,
                    DisplayName TEXT NOT NULL,
                    PasswordHash TEXT NOT NULL,
                    Role INTEGER NOT NULL,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL,
                    Version INTEGER NOT NULL DEFAULT 1
                );
                """;
            cmd.ExecuteNonQuery();
        }

        var initializer = new SqliteDatabaseInitializer(_connectionString);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => initializer.InitializeAsync());
        Assert.Contains("Users table schema validation failed", ex.Message);
    }

    [Fact]
    public async Task UnexpectedObjectCollision_FailsClosed_AndRollsBack()
    {
        // Setup database with Version 1 recorded in __SchemaMigrations,
        // but an unexpected colliding table 'Batches' already exists
        using (var conn = new SqliteConnection(_connectionString))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE __SchemaMigrations (
                    Version INTEGER PRIMARY KEY,
                    Description TEXT NOT NULL,
                    AppliedAtUtc TEXT NOT NULL
                );
                INSERT INTO __SchemaMigrations VALUES (1, '001_InitialAuth', '2026-10-18T18:00:00Z');

                CREATE TABLE Batches (
                    IncompatibleCol TEXT
                );
                """;
            cmd.ExecuteNonQuery();
        }

        var initializer = new SqliteDatabaseInitializer(_connectionString);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => initializer.InitializeAsync());
        Assert.Contains("unexpected pre-existing table 'Batches'", ex.Message);

        // Verify Version 2 was NOT recorded
        using (var conn = new SqliteConnection(_connectionString))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT MAX(Version) FROM __SchemaMigrations;";
            long maxVersion = Convert.ToInt64(cmd.ExecuteScalar());
            Assert.Equal(1L, maxVersion);
        }
    }

    [Fact]
    public async Task InconsistentMigrationHistory_UnknownVersion_FailsClosed()
    {
        using (var conn = new SqliteConnection(_connectionString))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE __SchemaMigrations (
                    Version INTEGER PRIMARY KEY,
                    Description TEXT NOT NULL,
                    AppliedAtUtc TEXT NOT NULL
                );
                INSERT INTO __SchemaMigrations VALUES (999, 'UnknownMigration', '2026-10-18T18:00:00Z');
                """;
            cmd.ExecuteNonQuery();
        }

        var initializer = new SqliteDatabaseInitializer(_connectionString);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => initializer.InitializeAsync());
        Assert.Contains("unknown migration version 999", ex.Message);
    }

    [Fact]
    public async Task ForeignKeysAndTriggers_Enforced()
    {
        var initializer = new SqliteDatabaseInitializer(_connectionString);
        await initializer.InitializeAsync();

        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        // 1. Verify foreign keys are enabled
        using (var pragmaCmd = conn.CreateCommand())
        {
            pragmaCmd.CommandText = "PRAGMA foreign_keys;";
            Assert.Equal(1L, Convert.ToInt64(pragmaCmd.ExecuteScalar()));
        }

        // 2. FK violation: inserting Batch with non-existent MedicineId must fail
        using (var insertCmd = conn.CreateCommand())
        {
            insertCmd.CommandText = """
                INSERT INTO Batches (Id, MedicineId, BatchCode, QuantityOnHand, PurchaseCostPaisa, SellingPricePaisa, CreatedAt, UpdatedAt)
                VALUES ('batch-1', 'non-existent-med', 'BTH-1', 10, 1000, 1500, '2026-10-19T00:00:00Z', '2026-10-19T00:00:00Z');
                """;
            await Assert.ThrowsAsync<SqliteException>(() => insertCmd.ExecuteNonQueryAsync());
        }

        // 3. Insert valid User, Medicine, and Batch
        using (var seedCmd = conn.CreateCommand())
        {
            seedCmd.CommandText = """
                INSERT INTO Users (Id, Username, DisplayName, PasswordHash, Role, IsActive, CreatedAt, UpdatedAt, Version)
                VALUES ('user-1', 'tester', 'Tester', 'hash', 0, 1, 'now', 'now', 1);

                INSERT INTO Medicines (Id, Name, GenericName, Brand, Category, DosageForm, Strength, DispensingUnit, ReorderLevel, RequiresPrescription, IsActive, CreatedAt, UpdatedAt, Version)
                VALUES ('med-1', 'Paracetamol', 'Paracetamol', 'Nepal Pharma', 'Analgesic', 'Tablet', '500mg', 'Strip', 10, 0, 1, 'now', 'now', 1);

                INSERT INTO Batches (Id, MedicineId, BatchCode, QuantityOnHand, PurchaseCostPaisa, SellingPricePaisa, CreatedAt, UpdatedAt)
                VALUES ('batch-1', 'med-1', 'BTH-101', 50, 1000, 1500, 'now', 'now');

                INSERT INTO StockAdjustments (Id, BatchId, MedicineId, AdjustmentType, PreviousQuantity, QuantityDelta, NewQuantity, Reason, UserId, Timestamp)
                VALUES ('adj-1', 'batch-1', 'med-1', 1, 50, 10, 60, 'Initial Stock Count', 'user-1', 'now');
                """;
            seedCmd.ExecuteNonQuery();
        }

        // 4. Immutability Trigger: updating StockAdjustments must fail
        using (var updateCmd = conn.CreateCommand())
        {
            updateCmd.CommandText = "UPDATE StockAdjustments SET Reason = 'Tampered Reason' WHERE Id = 'adj-1';";
            var ex = await Assert.ThrowsAsync<SqliteException>(() => updateCmd.ExecuteNonQueryAsync());
            Assert.Contains("StockAdjustments ledger entries are immutable", ex.Message);
        }

        // 5. Immutability Trigger: deleting from StockAdjustments must fail
        using (var deleteCmd = conn.CreateCommand())
        {
            deleteCmd.CommandText = "DELETE FROM StockAdjustments WHERE Id = 'adj-1';";
            var ex = await Assert.ThrowsAsync<SqliteException>(() => deleteCmd.ExecuteNonQueryAsync());
            Assert.Contains("StockAdjustments ledger entries are immutable", ex.Message);
        }
    }

    [Fact]
    public async Task ExistingAuthDatabase_InDeleteJournalMode_WhenBackupFails_PreservesJournalModeAndData()
    {
        // 1. Create a pre-migration auth database explicitly configured in DELETE journal mode
        using (var conn = new SqliteConnection(_connectionString))
        {
            conn.Open();
            using var pragmaCmd = conn.CreateCommand();
            pragmaCmd.CommandText = "PRAGMA journal_mode = DELETE;";
            var initialMode = (string?)pragmaCmd.ExecuteScalar();
            Assert.Equal("delete", initialMode?.ToLowerInvariant());

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
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
                VALUES ('user-delete-mode', 'admin', 'Admin User', 'hash-123', 0, 1, '2026-10-20T00:00:00Z', '2026-10-20T00:00:00Z', 1);
                """;
            cmd.ExecuteNonQuery();
        }

        // 2. Configure initializer with failing backup service
        var failingBackupService = new FailingBackupService();
        var initializer = new SqliteDatabaseInitializer(_connectionString, failingBackupService);

        // 3. Assert initialization aborts due to backup failure
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => initializer.InitializeAsync());
        Assert.Equal("Simulated backup failure", ex.Message);

        // 4. Verify that journal mode remains DELETE and persistent state was completely unchanged
        using (var conn = new SqliteConnection(_connectionString))
        {
            conn.Open();

            // Journal mode must NOT have been changed to WAL
            using var checkModeCmd = conn.CreateCommand();
            checkModeCmd.CommandText = "PRAGMA journal_mode;";
            var currentMode = (string?)checkModeCmd.ExecuteScalar();
            Assert.Equal("delete", currentMode?.ToLowerInvariant());

            // User record must remain intact
            using var checkUserCmd = conn.CreateCommand();
            checkUserCmd.CommandText = "SELECT Username, PasswordHash FROM Users WHERE Id = 'user-delete-mode';";
            using var reader = checkUserCmd.ExecuteReader();
            Assert.True(reader.Read());
            Assert.Equal("admin", reader.GetString(0));
            Assert.Equal("hash-123", reader.GetString(1));

            // __SchemaMigrations must NOT exist
            using var checkMigrationsCmd = conn.CreateCommand();
            checkMigrationsCmd.CommandText = "SELECT name FROM sqlite_master WHERE name = '__SchemaMigrations';";
            Assert.Null(checkMigrationsCmd.ExecuteScalar());
        }
    }

    [Fact]
    public async Task InconsistentMigrationHistory_GapInVersions_FailsClosed()
    {
        // Setup non-contiguous migration versions 1 and 3 in __SchemaMigrations
        using (var conn = new SqliteConnection(_connectionString))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE __SchemaMigrations (
                    Version INTEGER PRIMARY KEY,
                    Description TEXT NOT NULL,
                    AppliedAtUtc TEXT NOT NULL
                );
                INSERT INTO __SchemaMigrations VALUES (1, '001_InitialAuth', '2026-10-18T18:00:00Z');
                INSERT INTO __SchemaMigrations VALUES (3, '003_HypotheticalFuture', '2026-10-18T18:00:00Z');
                """;
            cmd.ExecuteNonQuery();
        }

        var initializer = new SqliteDatabaseInitializer(_connectionString);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => initializer.InitializeAsync());
        Assert.Contains("gap detected in applied migrations", ex.Message);

        // Verify __SchemaMigrations was not changed and no further migrations applied
        using (var conn = new SqliteConnection(_connectionString))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM __SchemaMigrations;";
            Assert.Equal(2L, Convert.ToInt64(cmd.ExecuteScalar()));

            // Batches table must NOT exist
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE name = 'Batches';";
            Assert.Null(cmd.ExecuteScalar());
        }
    }

    private sealed class FailingBackupService : ISqliteBackupService
    {
        public Task<string> CreateBackupAsync(string sourceConnectionString, string targetBackupPath, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Simulated backup failure");
        }

        public Task VerifyBackupAsync(string sourceConnectionString, string backupPath, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Simulated backup failure");
        }
    }
}
