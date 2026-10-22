using PharmacyApp.Domain.Entities;

namespace PharmacyApp.Application.Abstractions;

public interface IInvoiceRepository
{
    Task<Guid> AddAsync(Invoice invoice, CancellationToken cancellationToken = default, object? transaction = null);
    Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default, object? transaction = null);
    Task<Invoice?> GetByInvoiceNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default, object? transaction = null);
    Task<IReadOnlyList<Invoice>> ListRecentAsync(int limit = 50, string? searchTerm = null, CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);
    Task<decimal> GetTotalSalesAmountAsync(DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken cancellationToken = default);
    Task UpdateAsync(Invoice invoice, uint expectedVersion, CancellationToken cancellationToken = default, object? transaction = null);
}
