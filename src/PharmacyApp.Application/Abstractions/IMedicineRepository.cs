using System.Data.Common;
using PharmacyApp.Domain.Entities;

namespace PharmacyApp.Application.Abstractions;

/// <summary>
/// Abstraction for persistent storage and retrieval of master medicine catalogue records.
/// </summary>
public interface IMedicineRepository
{
    /// <summary>
    /// Finds a medicine by its unique identifier.
    /// </summary>
    Task<Medicine?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default, DbTransaction? transaction = null);

    /// <summary>
    /// Finds a medicine by its optional barcode (case-insensitive).
    /// Returns null if barcode is null, empty, or does not exist.
    /// </summary>
    Task<Medicine?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default, DbTransaction? transaction = null);

    /// <summary>
    /// Lists medicines ordered by name.
    /// </summary>
    /// <param name="isActive">Optional filter: true for active only, false for inactive only, null for all.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="transaction">Optional active database transaction.</param>
    Task<IReadOnlyList<Medicine>> ListAsync(bool? isActive = null, CancellationToken cancellationToken = default, DbTransaction? transaction = null);

    /// <summary>
    /// Searches medicines across Name, GenericName, Brand, Category, and Barcode.
    /// </summary>
    /// <param name="query">Search term.</param>
    /// <param name="isActive">Optional filter: true for active only, false for inactive only, null for all.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="transaction">Optional active database transaction.</param>
    Task<IReadOnlyList<Medicine>> SearchAsync(string query, bool? isActive = null, CancellationToken cancellationToken = default, DbTransaction? transaction = null);

    /// <summary>
    /// Persists a newly created medicine catalogue item.
    /// Enforces barcode uniqueness consistent with database index UX_Medicines_Barcode.
    /// Never silently overwrites an existing record.
    /// </summary>
    Task AddAsync(Medicine medicine, CancellationToken cancellationToken = default, DbTransaction? transaction = null);

    /// <summary>
    /// Updates editable catalogue details of an existing medicine and increments its version.
    /// Enforces barcode uniqueness against other medicines and validates optimistic concurrency version.
    /// </summary>
    Task UpdateAsync(Medicine medicine, uint? expectedVersion = null, CancellationToken cancellationToken = default, DbTransaction? transaction = null);

    /// <summary>
    /// Checks whether a barcode is already registered, optionally excluding a specific medicine ID.
    /// </summary>
    Task<bool> ExistsBarcodeAsync(string barcode, Guid? excludeMedicineId = null, CancellationToken cancellationToken = default, DbTransaction? transaction = null);
}
