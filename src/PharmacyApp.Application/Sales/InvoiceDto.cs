namespace PharmacyApp.Application.Sales;

public sealed record InvoiceItemDto(
    Guid Id,
    Guid BatchId,
    Guid MedicineId,
    string MedicineName,
    string BatchCode,
    int Quantity,
    decimal UnitPriceNpr,
    decimal LineTotalNpr);

public sealed record InvoiceDto(
    Guid Id,
    string InvoiceNumber,
    DateTimeOffset CreatedAtUtc,
    string? CustomerName,
    string? PrescriptionNumber,
    string PaymentMethodName,
    decimal SubTotalNpr,
    decimal DiscountNpr,
    decimal TaxNpr,
    decimal TotalAmountNpr,
    string StatusName,
    string? Notes,
    IReadOnlyList<InvoiceItemDto> Items);
