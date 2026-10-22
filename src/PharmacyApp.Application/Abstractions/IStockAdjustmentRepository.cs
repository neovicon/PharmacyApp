using System.Data.Common;
using PharmacyApp.Domain.Entities;

namespace PharmacyApp.Application.Abstractions;

/// <summary>
/// Abstraction for the append-only, immutable stock-adjustment audit ledger.
/// Under strict compliance rules, this interface explicitly excludes update and delete operations.
/// </summary>
public interface IStockAdjustmentRepository
{
    /// <summary>
    /// Persists a new immutable stock-adjustment audit record.
    /// Enforces foreign keys to Batch, Medicine, and User, as well as arithmetic integrity.
    /// </summary>
    Task AddAsync(StockAdjustment adjustment, CancellationToken cancellationToken = default, DbTransaction? transaction = null);

    /// <summary>
    /// Finds a single stock-adjustment record by its unique identifier.
    /// </summary>
    Task<StockAdjustment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default, DbTransaction? transaction = null);

    /// <summary>
    /// Retrieves all adjustment audit records for a given batch, ordered from newest to oldest.
    /// </summary>
    Task<IReadOnlyList<StockAdjustment>> GetByBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default, DbTransaction? transaction = null);

    /// <summary>
    /// Retrieves all adjustment audit records for a given medicine, ordered from newest to oldest.
    /// </summary>
    Task<IReadOnlyList<StockAdjustment>> GetByMedicineIdAsync(Guid medicineId, CancellationToken cancellationToken = default, DbTransaction? transaction = null);

    /// <summary>
    /// Retrieves the most recent stock-adjustment records across the pharmacy ledger.
    /// </summary>
    /// <param name="limit">Maximum number of records to return (defaults to 50).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="transaction">Optional active database transaction.</param>
    Task<IReadOnlyList<StockAdjustment>> ListRecentAsync(int limit = 50, CancellationToken cancellationToken = default, DbTransaction? transaction = null);
}
