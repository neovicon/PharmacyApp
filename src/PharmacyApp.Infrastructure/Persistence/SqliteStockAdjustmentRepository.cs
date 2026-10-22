using System.Data.Common;
using System.Globalization;
using Microsoft.Data.Sqlite;
using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Exceptions;
using PharmacyApp.Domain.Entities;
using PharmacyApp.Domain.Enums;

namespace PharmacyApp.Infrastructure.Persistence;

/// <summary>
/// SQLite implementation of <see cref="IStockAdjustmentRepository"/>.
/// Appends and reads immutable audit ledger entries.
/// Update and delete methods are deliberately not implemented.
/// </summary>
public sealed class SqliteStockAdjustmentRepository : IStockAdjustmentRepository
{
    private readonly string _connectionString;

    public SqliteStockAdjustmentRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task AddAsync(StockAdjustment adjustment, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        var (connection, shouldDispose) = await GetConnectionAsync(transaction, cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            if (transaction is SqliteTransaction sqliteTx)
                command.Transaction = sqliteTx;

            command.CommandText = """
                INSERT INTO StockAdjustments (
                    Id, BatchId, MedicineId, AdjustmentType, PreviousQuantity,
                    QuantityDelta, NewQuantity, Reason, UserId, Timestamp
                ) VALUES (
                    @Id, @BatchId, @MedicineId, @AdjustmentType, @PreviousQuantity,
                    @QuantityDelta, @NewQuantity, @Reason, @UserId, @Timestamp
                );
                """;

            command.Parameters.AddWithValue("@Id", adjustment.Id.ToString());
            command.Parameters.AddWithValue("@BatchId", adjustment.BatchId.ToString());
            command.Parameters.AddWithValue("@MedicineId", adjustment.MedicineId.ToString());
            command.Parameters.AddWithValue("@AdjustmentType", (int)adjustment.AdjustmentType);
            command.Parameters.AddWithValue("@PreviousQuantity", adjustment.PreviousQuantity);
            command.Parameters.AddWithValue("@QuantityDelta", adjustment.QuantityDelta);
            command.Parameters.AddWithValue("@NewQuantity", adjustment.NewQuantity);
            command.Parameters.AddWithValue("@Reason", adjustment.Reason);
            command.Parameters.AddWithValue("@UserId", adjustment.UserId.ToString());
            command.Parameters.AddWithValue("@Timestamp", adjustment.TimestampUtc.ToString("O", CultureInfo.InvariantCulture));

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException ex)
        {
            var mapped = SqliteExceptionMapper.Map(ex, nameof(StockAdjustment), adjustment.Id.ToString());
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

    public async Task<StockAdjustment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        var (connection, shouldDispose) = await GetConnectionAsync(transaction, cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            if (transaction is SqliteTransaction sqliteTx)
                command.Transaction = sqliteTx;

            command.CommandText = """
                SELECT Id, BatchId, MedicineId, AdjustmentType, PreviousQuantity,
                       QuantityDelta, NewQuantity, Reason, UserId, Timestamp
                FROM StockAdjustments
                WHERE Id = @Id
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("@Id", id.ToString());

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            return MapStockAdjustmentFromReader(reader);
        }
        finally
        {
            if (shouldDispose)
                await connection.DisposeAsync();
        }
    }

    public async Task<IReadOnlyList<StockAdjustment>> GetByBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        var (connection, shouldDispose) = await GetConnectionAsync(transaction, cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            if (transaction is SqliteTransaction sqliteTx)
                command.Transaction = sqliteTx;

            command.CommandText = """
                SELECT Id, BatchId, MedicineId, AdjustmentType, PreviousQuantity,
                       QuantityDelta, NewQuantity, Reason, UserId, Timestamp
                FROM StockAdjustments
                WHERE BatchId = @BatchId
                ORDER BY Timestamp DESC;
                """;
            command.Parameters.AddWithValue("@BatchId", batchId.ToString());

            var list = new List<StockAdjustment>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(MapStockAdjustmentFromReader(reader));
            }

            return list;
        }
        finally
        {
            if (shouldDispose)
                await connection.DisposeAsync();
        }
    }

    public async Task<IReadOnlyList<StockAdjustment>> GetByMedicineIdAsync(Guid medicineId, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        var (connection, shouldDispose) = await GetConnectionAsync(transaction, cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            if (transaction is SqliteTransaction sqliteTx)
                command.Transaction = sqliteTx;

            command.CommandText = """
                SELECT Id, BatchId, MedicineId, AdjustmentType, PreviousQuantity,
                       QuantityDelta, NewQuantity, Reason, UserId, Timestamp
                FROM StockAdjustments
                WHERE MedicineId = @MedicineId
                ORDER BY Timestamp DESC;
                """;
            command.Parameters.AddWithValue("@MedicineId", medicineId.ToString());

            var list = new List<StockAdjustment>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(MapStockAdjustmentFromReader(reader));
            }

            return list;
        }
        finally
        {
            if (shouldDispose)
                await connection.DisposeAsync();
        }
    }

    public async Task<IReadOnlyList<StockAdjustment>> ListRecentAsync(int limit = 50, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        int effectiveLimit = limit <= 0 ? 50 : limit;

        var (connection, shouldDispose) = await GetConnectionAsync(transaction, cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            if (transaction is SqliteTransaction sqliteTx)
                command.Transaction = sqliteTx;

            command.CommandText = """
                SELECT Id, BatchId, MedicineId, AdjustmentType, PreviousQuantity,
                       QuantityDelta, NewQuantity, Reason, UserId, Timestamp
                FROM StockAdjustments
                ORDER BY Timestamp DESC
                LIMIT @Limit;
                """;
            command.Parameters.AddWithValue("@Limit", effectiveLimit);

            var list = new List<StockAdjustment>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(MapStockAdjustmentFromReader(reader));
            }

            return list;
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

    private static StockAdjustment MapStockAdjustmentFromReader(SqliteDataReader reader)
    {
        Guid id = Guid.Parse(reader.GetString(0));
        Guid batchId = Guid.Parse(reader.GetString(1));
        Guid medicineId = Guid.Parse(reader.GetString(2));
        StockAdjustmentType adjustmentType = (StockAdjustmentType)reader.GetInt32(3);
        int previousQuantity = reader.GetInt32(4);
        int quantityDelta = reader.GetInt32(5);
        int newQuantity = reader.GetInt32(6);
        string reason = reader.GetString(7);
        Guid userId = Guid.Parse(reader.GetString(8));
        DateTimeOffset timestampUtc = DateTimeOffset.Parse(reader.GetString(9), CultureInfo.InvariantCulture);

        return StockAdjustment.Load(
            id,
            batchId,
            medicineId,
            userId,
            adjustmentType,
            quantityDelta,
            previousQuantity,
            newQuantity,
            reason,
            timestampUtc);
    }
}
