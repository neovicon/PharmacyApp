using System.Globalization;
using Microsoft.Data.Sqlite;
using PharmacyApp.Application.Abstractions;
using PharmacyApp.Domain.Entities;

namespace PharmacyApp.Infrastructure.Persistence;

/// <summary>
/// SQLite implementation of <see cref="ISupplierRepository"/>.
/// Uses safe parameterized queries and optimistic concurrency version tracking.
/// </summary>
public sealed class SqliteSupplierRepository : ISupplierRepository
{
    private readonly string _connectionString;

    public SqliteSupplierRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        if (!await TableExistsAsync(connection, cancellationToken))
            return null;

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Name, ContactPerson, Phone, Email, PanNumber, Address, IsActive, CreatedAt, UpdatedAt, Version
            FROM Suppliers
            WHERE Id = @Id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@Id", id.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapSupplierFromReader(reader);
    }

    public async Task<IReadOnlyList<Supplier>> ListAsync(bool? isActive = null, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        if (!await TableExistsAsync(connection, cancellationToken))
            return Array.Empty<Supplier>();

        await using var command = connection.CreateCommand();
        command.CommandText = isActive.HasValue
            ? """
              SELECT Id, Name, ContactPerson, Phone, Email, PanNumber, Address, IsActive, CreatedAt, UpdatedAt, Version
              FROM Suppliers
              WHERE IsActive = @IsActive
              ORDER BY Name ASC;
              """
            : """
              SELECT Id, Name, ContactPerson, Phone, Email, PanNumber, Address, IsActive, CreatedAt, UpdatedAt, Version
              FROM Suppliers
              ORDER BY Name ASC;
              """;

        if (isActive.HasValue)
        {
            command.Parameters.AddWithValue("@IsActive", isActive.Value ? 1 : 0);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var list = new List<Supplier>();
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(MapSupplierFromReader(reader));
        }

        return list;
    }

    public async Task<IReadOnlyList<Supplier>> SearchAsync(string query, bool? isActive = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await ListAsync(isActive, cancellationToken);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        if (!await TableExistsAsync(connection, cancellationToken))
            return Array.Empty<Supplier>();

        await using var command = connection.CreateCommand();
        string baseSql = """
            SELECT Id, Name, ContactPerson, Phone, Email, PanNumber, Address, IsActive, CreatedAt, UpdatedAt, Version
            FROM Suppliers
            WHERE (Name LIKE @Query ESCAPE '\' OR PanNumber LIKE @Query ESCAPE '\' OR ContactPerson LIKE @Query ESCAPE '\')
            """;

        if (isActive.HasValue)
        {
            baseSql += " AND IsActive = @IsActive";
            command.Parameters.AddWithValue("@IsActive", isActive.Value ? 1 : 0);
        }

        baseSql += " ORDER BY Name ASC;";
        command.CommandText = baseSql;

        string escaped = query.Trim().Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
        command.Parameters.AddWithValue("@Query", $"%{escaped}%");

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var list = new List<Supplier>();
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(MapSupplierFromReader(reader));
        }

        return list;
    }

    public async Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(supplier);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Suppliers (Id, Name, ContactPerson, Phone, Email, PanNumber, Address, IsActive, CreatedAt, UpdatedAt, Version)
            VALUES (@Id, @Name, @ContactPerson, @Phone, @Email, @PanNumber, @Address, @IsActive, @CreatedAt, @UpdatedAt, @Version);
            """;

        command.Parameters.AddWithValue("@Id", supplier.Id.ToString());
        command.Parameters.AddWithValue("@Name", supplier.Name);
        command.Parameters.AddWithValue("@ContactPerson", (object?)supplier.ContactPerson ?? DBNull.Value);
        command.Parameters.AddWithValue("@Phone", (object?)supplier.Phone ?? DBNull.Value);
        command.Parameters.AddWithValue("@Email", (object?)supplier.Email ?? DBNull.Value);
        command.Parameters.AddWithValue("@PanNumber", (object?)supplier.PanNumber ?? DBNull.Value);
        command.Parameters.AddWithValue("@Address", (object?)supplier.Address ?? DBNull.Value);
        command.Parameters.AddWithValue("@IsActive", supplier.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("@CreatedAt", supplier.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@UpdatedAt", supplier.UpdatedAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@Version", (long)supplier.Version);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(Supplier supplier, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(supplier);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Suppliers
            SET Name = @Name,
                ContactPerson = @ContactPerson,
                Phone = @Phone,
                Email = @Email,
                PanNumber = @PanNumber,
                Address = @Address,
                IsActive = @IsActive,
                UpdatedAt = @UpdatedAt,
                Version = @Version
            WHERE Id = @Id;
            """;

        command.Parameters.AddWithValue("@Id", supplier.Id.ToString());
        command.Parameters.AddWithValue("@Name", supplier.Name);
        command.Parameters.AddWithValue("@ContactPerson", (object?)supplier.ContactPerson ?? DBNull.Value);
        command.Parameters.AddWithValue("@Phone", (object?)supplier.Phone ?? DBNull.Value);
        command.Parameters.AddWithValue("@Email", (object?)supplier.Email ?? DBNull.Value);
        command.Parameters.AddWithValue("@PanNumber", (object?)supplier.PanNumber ?? DBNull.Value);
        command.Parameters.AddWithValue("@Address", (object?)supplier.Address ?? DBNull.Value);
        command.Parameters.AddWithValue("@IsActive", supplier.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("@UpdatedAt", supplier.UpdatedAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@Version", (long)supplier.Version);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, CancellationToken ct)
    {
        await using var checkTableCmd = connection.CreateCommand();
        checkTableCmd.CommandText = "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name='Suppliers';";
        long count = (long)(await checkTableCmd.ExecuteScalarAsync(ct) ?? 0L);
        return count > 0;
    }

    private static Supplier MapSupplierFromReader(SqliteDataReader reader)
    {
        Guid id = Guid.Parse(reader.GetString(0));
        string name = reader.GetString(1);
        string? contactPerson = reader.IsDBNull(2) ? null : reader.GetString(2);
        string? phone = reader.IsDBNull(3) ? null : reader.GetString(3);
        string? email = reader.IsDBNull(4) ? null : reader.GetString(4);
        string? panNumber = reader.IsDBNull(5) ? null : reader.GetString(5);
        string? address = reader.IsDBNull(6) ? null : reader.GetString(6);
        bool isActive = reader.GetInt32(7) != 0;
        DateTimeOffset createdAt = DateTimeOffset.Parse(reader.GetString(8), CultureInfo.InvariantCulture);
        DateTimeOffset updatedAt = DateTimeOffset.Parse(reader.GetString(9), CultureInfo.InvariantCulture);
        uint version = (uint)reader.GetInt64(10);

        return Supplier.Load(id, name, contactPerson, phone, email, panNumber, address, isActive, createdAt, updatedAt, version);
    }
}
