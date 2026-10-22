using Microsoft.Data.Sqlite;

namespace PharmacyApp.Infrastructure.Persistence;

/// <summary>
/// Helper for consistently opening SQLite connections with required pragmas
/// (foreign keys enabled and busy timeout configured).
/// </summary>
internal static class SqliteConnectionHelper
{
    public static async Task<SqliteConnection> OpenConnectionAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;";
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        return connection;
    }
}
