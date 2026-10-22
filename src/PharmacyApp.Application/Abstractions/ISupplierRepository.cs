using PharmacyApp.Domain.Entities;

namespace PharmacyApp.Application.Abstractions;

/// <summary>
/// Repository abstraction for supplier and vendor management.
/// </summary>
public interface ISupplierRepository
{
    Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Supplier>> ListAsync(bool? isActive = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Supplier>> SearchAsync(string query, bool? isActive = null, CancellationToken cancellationToken = default);
    Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default);
    Task UpdateAsync(Supplier supplier, CancellationToken cancellationToken = default);
}
