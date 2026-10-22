using System.Data.Common;
using System.Globalization;
using Microsoft.Data.Sqlite;
using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Exceptions;
using PharmacyApp.Domain.Entities;

namespace PharmacyApp.Infrastructure.Persistence;

/// <summary>
/// SQLite implementation of <see cref="IMedicineRepository"/>.
/// Handles parameterized queries, barcode uniqueness enforcement, and atomic transactions.
/// </summary>
public sealed class SqliteMedicineRepository : IMedicineRepository
{
    private readonly string _connectionString;

    public SqliteMedicineRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Medicine?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        var (connection, shouldDispose) = await GetConnectionAsync(transaction, cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            if (transaction is SqliteTransaction sqliteTx)
                command.Transaction = sqliteTx;

            command.CommandText = """
                SELECT Id, Name, GenericName, Brand, Strength, DosageForm, DispensingUnit,
                       Barcode, Category, ReorderLevel, RequiresPrescription, IsActive,
                       CreatedAt, UpdatedAt, Version
                FROM Medicines
                WHERE Id = @Id
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("@Id", id.ToString());

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            return MapMedicineFromReader(reader);
        }
        finally
        {
            if (shouldDispose)
                await connection.DisposeAsync();
        }
    }

    public async Task<Medicine?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return null;

        var (connection, shouldDispose) = await GetConnectionAsync(transaction, cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            if (transaction is SqliteTransaction sqliteTx)
                command.Transaction = sqliteTx;

            command.CommandText = """
                SELECT Id, Name, GenericName, Brand, Strength, DosageForm, DispensingUnit,
                       Barcode, Category, ReorderLevel, RequiresPrescription, IsActive,
                       CreatedAt, UpdatedAt, Version
                FROM Medicines
                WHERE Barcode = @Barcode COLLATE NOCASE
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("@Barcode", barcode.Trim());

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            return MapMedicineFromReader(reader);
        }
        finally
        {
            if (shouldDispose)
                await connection.DisposeAsync();
        }
    }

    public async Task<IReadOnlyList<Medicine>> ListAsync(bool? isActive = null, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        var (connection, shouldDispose) = await GetConnectionAsync(transaction, cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            if (transaction is SqliteTransaction sqliteTx)
                command.Transaction = sqliteTx;

            command.CommandText = """
                SELECT Id, Name, GenericName, Brand, Strength, DosageForm, DispensingUnit,
                       Barcode, Category, ReorderLevel, RequiresPrescription, IsActive,
                       CreatedAt, UpdatedAt, Version
                FROM Medicines
                WHERE (@IsActive IS NULL OR IsActive = @IsActive)
                ORDER BY Name COLLATE NOCASE ASC;
                """;
            command.Parameters.AddWithValue("@IsActive", isActive.HasValue ? (isActive.Value ? 1 : 0) : DBNull.Value);

            var list = new List<Medicine>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(MapMedicineFromReader(reader));
            }

            return list;
        }
        finally
        {
            if (shouldDispose)
                await connection.DisposeAsync();
        }
    }

    public async Task<IReadOnlyList<Medicine>> SearchAsync(string query, bool? isActive = null, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await ListAsync(isActive, cancellationToken, transaction);

        string trimmed = query.Trim();
        string escapedPattern = EscapeLikePattern(trimmed);
        var (connection, shouldDispose) = await GetConnectionAsync(transaction, cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            if (transaction is SqliteTransaction sqliteTx)
                command.Transaction = sqliteTx;

            command.CommandText = """
                SELECT Id, Name, GenericName, Brand, Strength, DosageForm, DispensingUnit,
                       Barcode, Category, ReorderLevel, RequiresPrescription, IsActive,
                       CreatedAt, UpdatedAt, Version
                FROM Medicines
                WHERE (@IsActive IS NULL OR IsActive = @IsActive)
                  AND (
                      Name LIKE @Pattern ESCAPE '\' COLLATE NOCASE OR
                      GenericName LIKE @Pattern ESCAPE '\' COLLATE NOCASE OR
                      Brand LIKE @Pattern ESCAPE '\' COLLATE NOCASE OR
                      Category LIKE @Pattern ESCAPE '\' COLLATE NOCASE OR
                      Barcode = @ExactBarcode COLLATE NOCASE
                  )
                ORDER BY Name COLLATE NOCASE ASC;
                """;
            command.Parameters.AddWithValue("@IsActive", isActive.HasValue ? (isActive.Value ? 1 : 0) : DBNull.Value);
            command.Parameters.AddWithValue("@Pattern", $"%{escapedPattern}%");
            command.Parameters.AddWithValue("@ExactBarcode", trimmed);

            var list = new List<Medicine>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(MapMedicineFromReader(reader));
            }

            return list;
        }
        finally
        {
            if (shouldDispose)
                await connection.DisposeAsync();
        }
    }

    public async Task AddAsync(Medicine medicine, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        if (!string.IsNullOrWhiteSpace(medicine.Barcode) &&
            await ExistsBarcodeAsync(medicine.Barcode, cancellationToken: cancellationToken, transaction: transaction))
        {
            throw new DuplicateBarcodeException(medicine.Barcode);
        }

        var (connection, shouldDispose) = await GetConnectionAsync(transaction, cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            if (transaction is SqliteTransaction sqliteTx)
                command.Transaction = sqliteTx;

            command.CommandText = """
                INSERT INTO Medicines (
                    Id, Name, GenericName, Brand, Strength, DosageForm, DispensingUnit,
                    Barcode, Category, ReorderLevel, RequiresPrescription, IsActive,
                    CreatedAt, UpdatedAt, Version
                ) VALUES (
                    @Id, @Name, @GenericName, @Brand, @Strength, @DosageForm, @DispensingUnit,
                    @Barcode, @Category, @ReorderLevel, @RequiresPrescription, @IsActive,
                    @CreatedAt, @UpdatedAt, @Version
                );
                """;

            command.Parameters.AddWithValue("@Id", medicine.Id.ToString());
            command.Parameters.AddWithValue("@Name", medicine.Name);
            command.Parameters.AddWithValue("@GenericName", medicine.GenericName);
            command.Parameters.AddWithValue("@Brand", (object?)medicine.Brand ?? DBNull.Value);
            command.Parameters.AddWithValue("@Strength", (object?)medicine.Strength ?? DBNull.Value);
            command.Parameters.AddWithValue("@DosageForm", medicine.DosageForm);
            command.Parameters.AddWithValue("@DispensingUnit", medicine.DispensingUnit);
            command.Parameters.AddWithValue("@Barcode", (object?)medicine.Barcode ?? DBNull.Value);
            command.Parameters.AddWithValue("@Category", (object?)medicine.Category ?? DBNull.Value);
            command.Parameters.AddWithValue("@ReorderLevel", medicine.ReorderLevel);
            command.Parameters.AddWithValue("@RequiresPrescription", medicine.RequiresPrescription ? 1 : 0);
            command.Parameters.AddWithValue("@IsActive", medicine.IsActive ? 1 : 0);
            command.Parameters.AddWithValue("@CreatedAt", medicine.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("@UpdatedAt", medicine.UpdatedAt.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("@Version", (long)medicine.Version);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException ex)
        {
            var mapped = SqliteExceptionMapper.Map(ex, nameof(Medicine), medicine.Barcode);
            if (ReferenceEquals(mapped, ex))
                throw;
            throw mapped;
        }
        finally
        {
            if (shouldDispose)
                await connection.DisposeAsync();
        }
    }

    public async Task UpdateAsync(Medicine medicine, uint? expectedVersion = null, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        if (!string.IsNullOrWhiteSpace(medicine.Barcode) &&
            await ExistsBarcodeAsync(medicine.Barcode, excludeMedicineId: medicine.Id, cancellationToken: cancellationToken, transaction: transaction))
        {
            throw new DuplicateBarcodeException(medicine.Barcode);
        }

        uint checkVersion = expectedVersion ?? (medicine.Version > 1 ? medicine.Version - 1 : 1);

        var (connection, shouldDispose) = await GetConnectionAsync(transaction, cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            if (transaction is SqliteTransaction sqliteTx)
                command.Transaction = sqliteTx;

            command.CommandText = """
                UPDATE Medicines
                SET Name = @Name,
                    GenericName = @GenericName,
                    Brand = @Brand,
                    Strength = @Strength,
                    DosageForm = @DosageForm,
                    DispensingUnit = @DispensingUnit,
                    Barcode = @Barcode,
                    Category = @Category,
                    ReorderLevel = @ReorderLevel,
                    RequiresPrescription = @RequiresPrescription,
                    IsActive = @IsActive,
                    UpdatedAt = @UpdatedAt,
                    Version = @Version
                WHERE Id = @Id AND Version = @ExpectedVersion;
                """;

            command.Parameters.AddWithValue("@Id", medicine.Id.ToString());
            command.Parameters.AddWithValue("@Name", medicine.Name);
            command.Parameters.AddWithValue("@GenericName", medicine.GenericName);
            command.Parameters.AddWithValue("@Brand", (object?)medicine.Brand ?? DBNull.Value);
            command.Parameters.AddWithValue("@Strength", (object?)medicine.Strength ?? DBNull.Value);
            command.Parameters.AddWithValue("@DosageForm", medicine.DosageForm);
            command.Parameters.AddWithValue("@DispensingUnit", medicine.DispensingUnit);
            command.Parameters.AddWithValue("@Barcode", (object?)medicine.Barcode ?? DBNull.Value);
            command.Parameters.AddWithValue("@Category", (object?)medicine.Category ?? DBNull.Value);
            command.Parameters.AddWithValue("@ReorderLevel", medicine.ReorderLevel);
            command.Parameters.AddWithValue("@RequiresPrescription", medicine.RequiresPrescription ? 1 : 0);
            command.Parameters.AddWithValue("@IsActive", medicine.IsActive ? 1 : 0);
            command.Parameters.AddWithValue("@UpdatedAt", medicine.UpdatedAt.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("@Version", (long)medicine.Version);
            command.Parameters.AddWithValue("@ExpectedVersion", (long)checkVersion);

            int rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
            if (rowsAffected == 0)
            {
                var existing = await GetByIdAsync(medicine.Id, cancellationToken, transaction);
                if (existing == null)
                {
                    throw new EntityNotFoundException($"Medicine with ID '{medicine.Id}' was not found.");
                }

                throw new ConcurrencyException(
                    $"Optimistic concurrency violation: Medicine with ID '{medicine.Id}' was modified by another operation. Expected version {checkVersion}, but current database version is {existing.Version}.");
            }
        }
        catch (SqliteException ex)
        {
            var mapped = SqliteExceptionMapper.Map(ex, nameof(Medicine), medicine.Barcode);
            if (ReferenceEquals(mapped, ex))
                throw;
            throw mapped;
        }
        finally
        {
            if (shouldDispose)
                await connection.DisposeAsync();
        }
    }

    public async Task<bool> ExistsBarcodeAsync(string barcode, Guid? excludeMedicineId = null, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return false;

        var (connection, shouldDispose) = await GetConnectionAsync(transaction, cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            if (transaction is SqliteTransaction sqliteTx)
                command.Transaction = sqliteTx;

            command.CommandText = """
                SELECT COUNT(1)
                FROM Medicines
                WHERE Barcode = @Barcode COLLATE NOCASE
                  AND (@ExcludeId IS NULL OR Id != @ExcludeId)
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("@Barcode", barcode.Trim());
            command.Parameters.AddWithValue("@ExcludeId", excludeMedicineId.HasValue ? excludeMedicineId.Value.ToString() : DBNull.Value);

            object? result = await command.ExecuteScalarAsync(cancellationToken);
            long count = (long)(result ?? 0L);
            return count > 0;
        }
        finally
        {
            if (shouldDispose)
                await connection.DisposeAsync();
        }
    }

    private async Task<(SqliteConnection Connection, bool ShouldDispose)> GetConnectionAsync(DbTransaction? transaction, CancellationToken cancellationToken)
    {
        if (transaction?.Connection is SqliteConnection sqliteConnection)
        {
            return (sqliteConnection, false);
        }

        var connection = await SqliteConnectionHelper.OpenConnectionAsync(_connectionString, cancellationToken);
        return (connection, true);
    }

    private static Medicine MapMedicineFromReader(SqliteDataReader reader)
    {
        Guid id = Guid.Parse(reader.GetString(0));
        string name = reader.GetString(1);
        string genericName = reader.GetString(2);
        string? brand = reader.IsDBNull(3) ? null : reader.GetString(3);
        string? strength = reader.IsDBNull(4) ? null : reader.GetString(4);
        string dosageForm = reader.GetString(5);
        string dispensingUnit = reader.GetString(6);
        string? barcode = reader.IsDBNull(7) ? null : reader.GetString(7);
        string? category = reader.IsDBNull(8) ? null : reader.GetString(8);
        int reorderLevel = reader.GetInt32(9);
        bool requiresPrescription = reader.GetInt32(10) != 0;
        bool isActive = reader.GetInt32(11) != 0;
        DateTimeOffset createdAt = DateTimeOffset.Parse(reader.GetString(12), CultureInfo.InvariantCulture);
        DateTimeOffset updatedAt = DateTimeOffset.Parse(reader.GetString(13), CultureInfo.InvariantCulture);
        uint version = (uint)reader.GetInt64(14);

        return Medicine.Load(
            id,
            name,
            genericName,
            brand,
            strength,
            dosageForm,
            dispensingUnit,
            barcode,
            category,
            reorderLevel,
            requiresPrescription,
            isActive,
            createdAt,
            updatedAt,
            version);
    }

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace(@"\", @"\\")
            .Replace("%", @"\%")
            .Replace("_", @"\_");
    }
}
