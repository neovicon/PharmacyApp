using PharmacyApp.Domain.Enums;

namespace PharmacyApp.Application.Sales;

public sealed record SaleReceiptLineDto(
    Guid BatchId,
    Guid MedicineId,
    string MedicineName,
    string BatchCode,
    int Quantity,
    decimal UnitPriceNpr,
    decimal LineTotalNpr);

public sealed record SaleReceiptDto(
    Guid TransactionId,
    string InvoiceNumber,
    DateTimeOffset TimestampUtc,
    string? CustomerName,
    string? PrescriptionNumber,
    PaymentMethod PaymentMethod,
    IReadOnlyList<SaleReceiptLineDto> Lines,
    decimal SubTotalNpr,
    decimal DiscountNpr,
    decimal TaxNpr,
    decimal TotalAmountNpr,
    string? CashierName = null);
