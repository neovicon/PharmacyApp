using System.Data.Common;
using PharmacyApp.Domain.Entities;

namespace PharmacyApp.Application.Abstractions;

/// <summary>
/// Abstraction for persistent storage and retrieval of physical inventory batch records.
/// </summary>
public interface IBatchRepository
{
    /// <summary>
    /// Finds a batch by its unique primary key.
    /// </summary>
    Task<Batch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default, DbTransaction? transaction = null);

    /// <summary>
    /// Finds a batch by its internal BatchCode (case-insensitive).
    /// </summary>
    Task<Batch?> GetByBatchCodeAsync(string batchCode, CancellationToken cancellationToken = default, DbTransaction? transaction = null);

    /// <summary>
    /// Lists all batches belonging to a specific medicine, optionally filtered by active status.
    /// Ordered with known earliest expiry dates first, followed by batches with unknown expiry.
    /// </summary>
    Task<IReadOnlyList<Batch>> GetByMedicineIdAsync(Guid medicineId, bool? isActive = null, CancellationToken cancellationToken = default, DbTransaction? transaction = null);

    /// <summary>
    /// Queries batches using the specified filter criteria (expiry, stock level, active state).
    /// Preserves nullable expiry dates and exact integer paisa pricing.
    /// </summary>
    Task<IReadOnlyList<Batch>> ListAsync(BatchFilter? filter = null, CancellationToken cancellationToken = default, DbTransaction? transaction = null);

    /// <summary>
    /// Retrieves active batches that are confirmed expired as of the specified date (ExpiryDate &lt;= asOfDate).
    /// Batches with unknown expiry (null) are NOT included in expired batches.
    /// </summary>
    Task<IReadOnlyList<Batch>> GetExpiredBatchesAsync(DateOnly asOfDate, CancellationToken cancellationToken = default, DbTransaction? transaction = null);

    /// <summary>
    /// Persists a new batch record.
    /// Enforces internal BatchCode uniqueness and valid foreign key reference to Medicine.
    /// </summary>
    Task AddAsync(Batch batch, CancellationToken cancellationToken = default, DbTransaction? transaction = null);

    /// <summary>
    /// Updates editable batch fields (pricing, quantities, location, manufacturer lot number, active status),
    /// increments the record version, and enforces optimistic concurrency version checking.
    /// </summary>
    Task UpdateAsync(Batch batch, uint? expectedVersion = null, CancellationToken cancellationToken = default, DbTransaction? transaction = null);

    /// <summary>
    /// Checks whether an internal BatchCode is already in use, optionally excluding a specific batch ID.
    /// </summary>
    Task<bool> ExistsBatchCodeAsync(string batchCode, Guid? excludeBatchId = null, CancellationToken cancellationToken = default, DbTransaction? transaction = null);
}
