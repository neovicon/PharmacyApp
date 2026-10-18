using Microsoft.Data.Sqlite;

namespace PharmacyApp.Infrastructure.Persistence;

/// <summary>
/// Initializes and verifies the local SQLite database schema.
/// Runs idempotently on application startup.
/// </summary>
public sealed class SqliteDatabaseInitializer
{
    private readonly string _connectionString;

    public SqliteDatabaseInitializer(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Configure Write-Ahead Logging (WAL) for responsive concurrent I/O and durability
        await using (var pragmaCmd = connection.CreateCommand())
        {
            pragmaCmd.CommandText = "PRAGMA journal_mode = WAL; PRAGMA foreign_keys = ON;";
            await pragmaCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        const string createTablesSql = """
            CREATE TABLE IF NOT EXISTS Users (
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
            CREATE INDEX IF NOT EXISTS IX_Users_Username ON Users(Username);
            """;

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = createTablesSql;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
