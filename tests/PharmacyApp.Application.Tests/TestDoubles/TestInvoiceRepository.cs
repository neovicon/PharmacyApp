using PharmacyApp.Application.Abstractions;
using PharmacyApp.Domain.Entities;

namespace PharmacyApp.Application.Tests.TestDoubles;

public sealed class TestInvoiceRepository : IInvoiceRepository
{
    public List<Invoice> Invoices { get; } = new();

    public Task<Guid> AddAsync(Invoice invoice, CancellationToken cancellationToken = default, object? transaction = null)
    {
        Invoices.Add(invoice);
        return Task.FromResult(invoice.Id);
    }

    public Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default, object? transaction = null)
    {
        return Task.FromResult(Invoices.FirstOrDefault(i => i.Id == id));
    }

    public Task<Invoice?> GetByInvoiceNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default, object? transaction = null)
    {
        return Task.FromResult(Invoices.FirstOrDefault(i => i.InvoiceNumber == invoiceNumber));
    }

    public Task<IReadOnlyList<Invoice>> ListRecentAsync(int limit = 50, string? searchTerm = null, CancellationToken cancellationToken = default)
    {
        var query = Invoices.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(i => i.InvoiceNumber.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                                  || (i.CustomerName != null && i.CustomerName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)));
        }
        return Task.FromResult<IReadOnlyList<Invoice>>(query.Take(limit).ToList());
    }

    public Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Invoices.Count);
    }

    public Task<decimal> GetTotalSalesAmountAsync(DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken cancellationToken = default)
    {
        var total = Invoices.Sum(i => i.TotalAmount.Amount);
        return Task.FromResult(total);
    }

    public Task UpdateAsync(Invoice invoice, uint expectedVersion, CancellationToken cancellationToken = default, object? transaction = null)
    {
        return Task.CompletedTask;
    }
}
