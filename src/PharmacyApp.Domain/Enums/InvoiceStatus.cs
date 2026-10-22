namespace PharmacyApp.Domain.Enums;

/// <summary>
/// Status of an invoice transaction.
/// </summary>
public enum InvoiceStatus
{
    Completed = 1,
    Cancelled = 2,
    Refunded = 3
}
