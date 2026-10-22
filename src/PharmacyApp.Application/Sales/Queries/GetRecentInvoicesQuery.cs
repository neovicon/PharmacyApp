using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Common;

namespace PharmacyApp.Application.Sales.Queries;

public sealed record GetRecentInvoicesQuery(
    int Limit = 50,
    string? SearchTerm = null);

public sealed class GetRecentInvoicesQueryHandler : IQueryHandler<GetRecentInvoicesQuery, IReadOnlyList<InvoiceDto>>
{
    private readonly IInvoiceRepository _invoiceRepository;

    public GetRecentInvoicesQueryHandler(IInvoiceRepository invoiceRepository)
    {
        _invoiceRepository = invoiceRepository;
    }

    public async Task<Result<IReadOnlyList<InvoiceDto>>> HandleAsync(GetRecentInvoicesQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var invoices = await _invoiceRepository.ListRecentAsync(query.Limit, query.SearchTerm, cancellationToken);

        var dtos = invoices.Select(inv => new InvoiceDto(
            Id: inv.Id,
            InvoiceNumber: inv.InvoiceNumber,
            CreatedAtUtc: inv.CreatedAt,
            CustomerName: inv.CustomerName,
            PrescriptionNumber: inv.PrescriptionNumber,
            PaymentMethodName: inv.PaymentMethod.ToString(),
            SubTotalNpr: inv.SubTotal.Amount,
            DiscountNpr: inv.Discount.Amount,
            TaxNpr: inv.Tax.Amount,
            TotalAmountNpr: inv.TotalAmount.Amount,
            StatusName: inv.Status.ToString(),
            Notes: inv.Notes,
            Items: inv.Items.Select(i => new InvoiceItemDto(
                Id: i.Id,
                BatchId: i.BatchId,
                MedicineId: i.MedicineId,
                MedicineName: i.MedicineName,
                BatchCode: i.BatchCode,
                Quantity: i.Quantity,
                UnitPriceNpr: i.UnitPrice.Amount,
                LineTotalNpr: i.LineTotal.Amount)).ToList()
        )).ToList();

        return Result.Success<IReadOnlyList<InvoiceDto>>(dtos);
    }
}
