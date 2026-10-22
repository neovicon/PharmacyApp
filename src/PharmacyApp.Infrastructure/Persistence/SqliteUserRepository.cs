using Microsoft.Data.Sqlite;
using PharmacyApp.Application.Abstractions;
using PharmacyApp.Domain.Entities;
using PharmacyApp.Domain.Enums;
using System.Globalization;

namespace PharmacyApp.Infrastructure.Persistence;

/// <summary>
/// SQLite implementation of <see cref="IUserRepository"/>.
/// Uses safe parameterized SQL queries to prevent injection vulnerabilities.
/// </summary>
public sealed class SqliteUserRepository : IUserRepository
{
    private readonly string _connectionString;

    public SqliteUserRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<bool> HasAnyUsersAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var checkTableCmd = connection.CreateCommand();
        checkTableCmd.CommandText = "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name='Users';";
        long tableExists = (long)(await checkTableCmd.ExecuteScalarAsync(cancellationToken) ?? 0L);
        if (tableExists == 0)
        {
            return false;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM Users LIMIT 1;";

        object? result = await command.ExecuteScalarAsync(cancellationToken);
        long count = (long)(result ?? 0L);
        return count > 0;
    }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
            return null;

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Username, DisplayName, PasswordHash, Role, IsActive, CreatedAt, UpdatedAt, Version
            FROM Users
            WHERE Username = @Username COLLATE NOCASE
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@Username", username.Trim());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapUserFromReader(reader);
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Username, DisplayName, PasswordHash, Role, IsActive, CreatedAt, UpdatedAt, Version
            FROM Users
            WHERE Id = @Id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@Id", id.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapUserFromReader(reader);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Users (Id, Username, DisplayName, PasswordHash, Role, IsActive, CreatedAt, UpdatedAt, Version)
            VALUES (@Id, @Username, @DisplayName, @PasswordHash, @Role, @IsActive, @CreatedAt, @UpdatedAt, @Version);
            """;

        command.Parameters.AddWithValue("@Id", user.Id.ToString());
        command.Parameters.AddWithValue("@Username", user.Username);
        command.Parameters.AddWithValue("@DisplayName", user.DisplayName);
        command.Parameters.AddWithValue("@PasswordHash", user.PasswordHash);
        command.Parameters.AddWithValue("@Role", (int)user.Role);
        command.Parameters.AddWithValue("@IsActive", user.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("@CreatedAt", user.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@UpdatedAt", user.UpdatedAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@Version", (long)user.Version);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Users
            SET Username = @Username,
                DisplayName = @DisplayName,
                PasswordHash = @PasswordHash,
                Role = @Role,
                IsActive = @IsActive,
                UpdatedAt = @UpdatedAt,
                Version = @Version
            WHERE Id = @Id;
            """;

        command.Parameters.AddWithValue("@Id", user.Id.ToString());
        command.Parameters.AddWithValue("@Username", user.Username);
        command.Parameters.AddWithValue("@DisplayName", user.DisplayName);
        command.Parameters.AddWithValue("@PasswordHash", user.PasswordHash);
        command.Parameters.AddWithValue("@Role", (int)user.Role);
        command.Parameters.AddWithValue("@IsActive", user.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("@UpdatedAt", user.UpdatedAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@Version", (long)user.Version);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var checkTableCmd = connection.CreateCommand();
        checkTableCmd.CommandText = "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name='Users';";
        long tableExists = (long)(await checkTableCmd.ExecuteScalarAsync(cancellationToken) ?? 0L);
        if (tableExists == 0)
        {
            return Array.Empty<User>();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Username, DisplayName, PasswordHash, Role, IsActive, CreatedAt, UpdatedAt, Version
            FROM Users
            ORDER BY Username ASC;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var users = new List<User>();
        while (await reader.ReadAsync(cancellationToken))
        {
            users.Add(MapUserFromReader(reader));
        }

        return users;
    }

    public async Task<bool> TryCreateInitialAdminAsync(User user, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO Users (Id, Username, DisplayName, PasswordHash, Role, IsActive, CreatedAt, UpdatedAt, Version)
            SELECT @Id, @Username, @DisplayName, @PasswordHash, @Role, @IsActive, @CreatedAt, @UpdatedAt, @Version
            WHERE NOT EXISTS (SELECT 1 FROM Users LIMIT 1);
            """;

        command.Parameters.AddWithValue("@Id", user.Id.ToString());
        command.Parameters.AddWithValue("@Username", user.Username);
        command.Parameters.AddWithValue("@DisplayName", user.DisplayName);
        command.Parameters.AddWithValue("@PasswordHash", user.PasswordHash);
        command.Parameters.AddWithValue("@Role", (int)user.Role);
        command.Parameters.AddWithValue("@IsActive", user.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("@CreatedAt", user.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@UpdatedAt", user.UpdatedAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@Version", (long)user.Version);

        int rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return rowsAffected > 0;
    }

    private static User MapUserFromReader(SqliteDataReader reader)
    {
        Guid id = Guid.Parse(reader.GetString(0));
        string username = reader.GetString(1);
        string displayName = reader.GetString(2);
        string passwordHash = reader.GetString(3);
        UserRole role = (UserRole)reader.GetInt32(4);
        bool isActive = reader.GetInt32(5) != 0;
        DateTimeOffset createdAt = DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture);
        DateTimeOffset updatedAt = DateTimeOffset.Parse(reader.GetString(7), CultureInfo.InvariantCulture);
        uint version = (uint)reader.GetInt64(8);

        return User.Load(id, username, displayName, passwordHash, role, isActive, createdAt, updatedAt, version);
    }
}
