using System.Data.Common;
using System.Globalization;
using Microsoft.Data.Sqlite;
using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Exceptions;
using PharmacyApp.Domain.Entities;
using PharmacyApp.Domain.ValueObjects;

namespace PharmacyApp.Infrastructure.Persistence;

/// <summary>
/// SQLite implementation of <see cref="IBatchRepository"/>.
/// Preserves exact integer paisa monetary precision, nullable expiry dates,
/// whole-unit quantities, and strict BatchCode uniqueness.
/// </summary>
public sealed class SqliteBatchRepository : IBatchRepository
{
    private readonly string _connectionString;

    public SqliteBatchRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Batch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        var (connection, shouldDispose) = await GetConnectionAsync(transaction, cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            if (transaction is SqliteTransaction sqliteTx)
                command.Transaction = sqliteTx;

            command.CommandText = """
                SELECT Id, MedicineId, BatchCode, ManufacturerBatchNumber, ExpiryDate,
                       QuantityOnHand, PurchaseCostPaisa, SellingPricePaisa, Location,
                       IsActive, CreatedAt, UpdatedAt, Version
                FROM Batches
                WHERE Id = @Id
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("@Id", id.ToString());

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            return MapBatchFromReader(reader);
        }
        finally
        {
            if (shouldDispose)
                await connection.DisposeAsync();
        }
    }

    public async Task<Batch?> GetByBatchCodeAsync(string batchCode, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        if (string.IsNullOrWhiteSpace(batchCode))
            return null;

        var (connection, shouldDispose) = await GetConnectionAsync(transaction, cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            if (transaction is SqliteTransaction sqliteTx)
                command.Transaction = sqliteTx;

            command.CommandText = """
                SELECT Id, MedicineId, BatchCode, ManufacturerBatchNumber, ExpiryDate,
                       QuantityOnHand, PurchaseCostPaisa, SellingPricePaisa, Location,
                       IsActive, CreatedAt, UpdatedAt, Version
                FROM Batches
                WHERE BatchCode = @BatchCode COLLATE NOCASE
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("@BatchCode", batchCode.Trim());

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            return MapBatchFromReader(reader);
        }
        finally
        {
            if (shouldDispose)
                await connection.DisposeAsync();
        }
    }

    public async Task<IReadOnlyList<Batch>> GetByMedicineIdAsync(Guid medicineId, bool? isActive = null, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        var (connection, shouldDispose) = await GetConnectionAsync(transaction, cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            if (transaction is SqliteTransaction sqliteTx)
                command.Transaction = sqliteTx;

            command.CommandText = """
                SELECT Id, MedicineId, BatchCode, ManufacturerBatchNumber, ExpiryDate,
                       QuantityOnHand, PurchaseCostPaisa, SellingPricePaisa, Location,
                       IsActive, CreatedAt, UpdatedAt, Version
                FROM Batches
                WHERE MedicineId = @MedicineId
                  AND (@IsActive IS NULL OR IsActive = @IsActive)
                ORDER BY
                    CASE WHEN ExpiryDate IS NULL THEN 1 ELSE 0 END,
                    ExpiryDate ASC,
                    CreatedAt ASC;
                """;
            command.Parameters.AddWithValue("@MedicineId", medicineId.ToString());
            command.Parameters.AddWithValue("@IsActive", isActive.HasValue ? (isActive.Value ? 1 : 0) : DBNull.Value);

            var list = new List<Batch>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(MapBatchFromReader(reader));
            }

            return list;
        }
        finally
        {
            if (shouldDispose)
                await connection.DisposeAsync();
        }
    }

    public async Task<IReadOnlyList<Batch>> ListAsync(BatchFilter? filter = null, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        var (connection, shouldDispose) = await GetConnectionAsync(transaction, cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            if (transaction is SqliteTransaction sqliteTx)
                command.Transaction = sqliteTx;

            var clauses = new List<string>();

            if (filter?.IsActive.HasValue == true)
            {
                clauses.Add("IsActive = @IsActive");
                command.Parameters.AddWithValue("@IsActive", filter.IsActive.Value ? 1 : 0);
            }

            if (filter?.MedicineId.HasValue == true)
            {
                clauses.Add("MedicineId = @MedicineId");
                command.Parameters.AddWithValue("@MedicineId", filter.MedicineId.Value.ToString());
            }

            if (filter?.InStockOnly.HasValue == true)
            {
                if (filter.InStockOnly.Value)
                    clauses.Add("QuantityOnHand > 0");
                else
                    clauses.Add("QuantityOnHand = 0");
            }

            if (filter?.ExpiredAsOf.HasValue == true)
            {
                clauses.Add("ExpiryDate IS NOT NULL AND ExpiryDate <= @ExpiredAsOf");
                command.Parameters.AddWithValue("@ExpiredAsOf", filter.ExpiredAsOf.Value.ToString("yyyy-MM-dd"));
            }

            if (filter?.ValidAsOf.HasValue == true)
            {
                // Crucial: ExpiryDate IS NOT NULL ensures unknown expiry is NOT treated as valid/safe to dispense
                clauses.Add("ExpiryDate IS NOT NULL AND ExpiryDate > @ValidAsOf");
                command.Parameters.AddWithValue("@ValidAsOf", filter.ValidAsOf.Value.ToString("yyyy-MM-dd"));
            }

            if (filter?.HasUnknownExpiry.HasValue == true)
            {
                if (filter.HasUnknownExpiry.Value)
                    clauses.Add("ExpiryDate IS NULL");
                else
                    clauses.Add("ExpiryDate IS NOT NULL");
            }

            string whereClause = clauses.Count > 0 ? "WHERE " + string.Join(" AND ", clauses) : "";

            command.CommandText = $"""
                SELECT Id, MedicineId, BatchCode, ManufacturerBatchNumber, ExpiryDate,
                       QuantityOnHand, PurchaseCostPaisa, SellingPricePaisa, Location,
                       IsActive, CreatedAt, UpdatedAt, Version
                FROM Batches
                {whereClause}
                ORDER BY
                    CASE WHEN ExpiryDate IS NULL THEN 1 ELSE 0 END,
                    ExpiryDate ASC,
                    BatchCode ASC;
                """;

            var list = new List<Batch>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(MapBatchFromReader(reader));
            }

            return list;
        }
        finally
        {
            if (shouldDispose)
                await connection.DisposeAsync();
        }
    }

    public async Task<IReadOnlyList<Batch>> GetExpiredBatchesAsync(DateOnly asOfDate, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        var (connection, shouldDispose) = await GetConnectionAsync(transaction, cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            if (transaction is SqliteTransaction sqliteTx)
                command.Transaction = sqliteTx;

            command.CommandText = """
                SELECT Id, MedicineId, BatchCode, ManufacturerBatchNumber, ExpiryDate,
                       QuantityOnHand, PurchaseCostPaisa, SellingPricePaisa, Location,
                       IsActive, CreatedAt, UpdatedAt, Version
                FROM Batches
                WHERE ExpiryDate IS NOT NULL
                  AND ExpiryDate <= @AsOfDate
                  AND IsActive = 1
                ORDER BY ExpiryDate ASC, BatchCode ASC;
                """;
            command.Parameters.AddWithValue("@AsOfDate", asOfDate.ToString("yyyy-MM-dd"));

            var list = new List<Batch>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(MapBatchFromReader(reader));
            }

            return list;
        }
        finally
        {
            if (shouldDispose)
                await connection.DisposeAsync();
        }
    }

    public async Task AddAsync(Batch batch, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        if (await ExistsBatchCodeAsync(batch.BatchCode, cancellationToken: cancellationToken, transaction: transaction))
        {
            throw new DuplicateBatchCodeException(batch.BatchCode);
        }

        var (connection, shouldDispose) = await GetConnectionAsync(transaction, cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            if (transaction is SqliteTransaction sqliteTx)
                command.Transaction = sqliteTx;

            command.CommandText = """
                INSERT INTO Batches (
                    Id, MedicineId, BatchCode, ManufacturerBatchNumber, ExpiryDate,
                    QuantityOnHand, PurchaseCostPaisa, SellingPricePaisa, Location,
                    IsActive, CreatedAt, UpdatedAt, Version
                ) VALUES (
                    @Id, @MedicineId, @BatchCode, @ManufacturerBatchNumber, @ExpiryDate,
                    @QuantityOnHand, @PurchaseCostPaisa, @SellingPricePaisa, @Location,
                    @IsActive, @CreatedAt, @UpdatedAt, @Version
                );
                """;

            command.Parameters.AddWithValue("@Id", batch.Id.ToString());
            command.Parameters.AddWithValue("@MedicineId", batch.MedicineId.ToString());
            command.Parameters.AddWithValue("@BatchCode", batch.BatchCode);
            command.Parameters.AddWithValue("@ManufacturerBatchNumber", (object?)batch.ManufacturerBatchNumber ?? DBNull.Value);
            command.Parameters.AddWithValue("@ExpiryDate", batch.ExpiryDate.HasValue ? batch.ExpiryDate.Value.ToString("yyyy-MM-dd") : DBNull.Value);
            command.Parameters.AddWithValue("@QuantityOnHand", batch.QuantityOnHand);
            command.Parameters.AddWithValue("@PurchaseCostPaisa", batch.PurchaseCost.ToPaisa());
            command.Parameters.AddWithValue("@SellingPricePaisa", batch.SellingPrice.ToPaisa());
            command.Parameters.AddWithValue("@Location", (object?)batch.Location ?? DBNull.Value);
            command.Parameters.AddWithValue("@IsActive", batch.IsActive ? 1 : 0);
            command.Parameters.AddWithValue("@CreatedAt", batch.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("@UpdatedAt", batch.UpdatedAt.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("@Version", (long)batch.Version);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException ex)
        {
            var mapped = SqliteExceptionMapper.Map(ex, nameof(Batch), batch.BatchCode);
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

    public async Task UpdateAsync(Batch batch, uint? expectedVersion = null, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        if (await ExistsBatchCodeAsync(batch.BatchCode, excludeBatchId: batch.Id, cancellationToken: cancellationToken, transaction: transaction))
        {
            throw new DuplicateBatchCodeException(batch.BatchCode);
        }

        uint checkVersion = expectedVersion ?? (batch.Version > 1 ? batch.Version - 1 : 1);

        var (connection, shouldDispose) = await GetConnectionAsync(transaction, cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            if (transaction is SqliteTransaction sqliteTx)
                command.Transaction = sqliteTx;

            command.CommandText = """
                UPDATE Batches
                SET MedicineId = @MedicineId,
                    BatchCode = @BatchCode,
                    ManufacturerBatchNumber = @ManufacturerBatchNumber,
                    ExpiryDate = @ExpiryDate,
                    QuantityOnHand = @QuantityOnHand,
                    PurchaseCostPaisa = @PurchaseCostPaisa,
                    SellingPricePaisa = @SellingPricePaisa,
                    Location = @Location,
                    IsActive = @IsActive,
                    UpdatedAt = @UpdatedAt,
                    Version = @Version
                WHERE Id = @Id AND Version = @ExpectedVersion;
                """;

            command.Parameters.AddWithValue("@Id", batch.Id.ToString());
            command.Parameters.AddWithValue("@MedicineId", batch.MedicineId.ToString());
            command.Parameters.AddWithValue("@BatchCode", batch.BatchCode);
            command.Parameters.AddWithValue("@ManufacturerBatchNumber", (object?)batch.ManufacturerBatchNumber ?? DBNull.Value);
            command.Parameters.AddWithValue("@ExpiryDate", batch.ExpiryDate.HasValue ? batch.ExpiryDate.Value.ToString("yyyy-MM-dd") : DBNull.Value);
            command.Parameters.AddWithValue("@QuantityOnHand", batch.QuantityOnHand);
            command.Parameters.AddWithValue("@PurchaseCostPaisa", batch.PurchaseCost.ToPaisa());
            command.Parameters.AddWithValue("@SellingPricePaisa", batch.SellingPrice.ToPaisa());
            command.Parameters.AddWithValue("@Location", (object?)batch.Location ?? DBNull.Value);
            command.Parameters.AddWithValue("@IsActive", batch.IsActive ? 1 : 0);
            command.Parameters.AddWithValue("@UpdatedAt", batch.UpdatedAt.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("@Version", (long)batch.Version);
            command.Parameters.AddWithValue("@ExpectedVersion", (long)checkVersion);

            int rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
            if (rowsAffected == 0)
            {
                var existing = await GetByIdAsync(batch.Id, cancellationToken, transaction);
                if (existing == null)
                {
                    throw new EntityNotFoundException($"Batch with ID '{batch.Id}' was not found.");
                }

                throw new ConcurrencyException(
                    $"Optimistic concurrency violation: Batch with ID '{batch.Id}' was modified by another operation. Expected version {checkVersion}, but current database version is {existing.Version}.");
            }
        }
        catch (SqliteException ex)
        {
            var mapped = SqliteExceptionMapper.Map(ex, nameof(Batch), batch.BatchCode);
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

    public async Task<bool> ExistsBatchCodeAsync(string batchCode, Guid? excludeBatchId = null, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        if (string.IsNullOrWhiteSpace(batchCode))
            return false;

        var (connection, shouldDispose) = await GetConnectionAsync(transaction, cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            if (transaction is SqliteTransaction sqliteTx)
                command.Transaction = sqliteTx;

            command.CommandText = """
                SELECT COUNT(1)
                FROM Batches
                WHERE BatchCode = @BatchCode COLLATE NOCASE
                  AND (@ExcludeId IS NULL OR Id != @ExcludeId)
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("@BatchCode", batchCode.Trim());
            command.Parameters.AddWithValue("@ExcludeId", excludeBatchId.HasValue ? excludeBatchId.Value.ToString() : DBNull.Value);

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

    private static Batch MapBatchFromReader(SqliteDataReader reader)
    {
        Guid id = Guid.Parse(reader.GetString(0));
        Guid medicineId = Guid.Parse(reader.GetString(1));
        string batchCode = reader.GetString(2);
        string? manufacturerBatchNumber = reader.IsDBNull(3) ? null : reader.GetString(3);
        DateOnly? expiryDate = reader.IsDBNull(4) ? null : DateOnly.Parse(reader.GetString(4), CultureInfo.InvariantCulture);
        int quantityOnHand = reader.GetInt32(5);
        Money purchaseCost = Money.FromPaisa(reader.GetInt64(6));
        Money sellingPrice = Money.FromPaisa(reader.GetInt64(7));
        string? location = reader.IsDBNull(8) ? null : reader.GetString(8);
        bool isActive = reader.GetInt32(9) != 0;
        DateTimeOffset createdAt = DateTimeOffset.Parse(reader.GetString(10), CultureInfo.InvariantCulture);
        DateTimeOffset updatedAt = DateTimeOffset.Parse(reader.GetString(11), CultureInfo.InvariantCulture);
        uint version = (uint)reader.GetInt64(12);

        return Batch.Load(
            id,
            medicineId,
            batchCode,
            manufacturerBatchNumber,
            expiryDate,
            quantityOnHand,
            purchaseCost,
            sellingPrice,
            location,
            isActive,
            createdAt,
            updatedAt,
            version);
    }
}
