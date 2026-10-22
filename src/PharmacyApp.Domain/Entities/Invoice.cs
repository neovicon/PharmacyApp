using PharmacyApp.Domain.Common;
using PharmacyApp.Domain.Enums;
using PharmacyApp.Domain.ValueObjects;

namespace PharmacyApp.Domain.Entities;

/// <summary>
/// Represents a finalized customer sales transaction invoice.
/// </summary>
public sealed class Invoice : EntityBase
{
    private readonly List<InvoiceItem> _items = new();

    public string InvoiceNumber { get; private set; }
    public string? CustomerName { get; private set; }
    public string? PrescriptionNumber { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; }
    public Money SubTotal { get; private set; }
    public Money Discount { get; private set; }
    public Money Tax { get; private set; }
    public Money TotalAmount { get; private set; }
    public Guid UserId { get; private set; }
    public string? Notes { get; private set; }
    public InvoiceStatus Status { get; private set; }
    public IReadOnlyList<InvoiceItem> Items => _items.AsReadOnly();

    private Invoice(
        Guid id,
        string invoiceNumber,
        string? customerName,
        string? prescriptionNumber,
        PaymentMethod paymentMethod,
        Money subTotal,
        Money discount,
        Money tax,
        Money totalAmount,
        Guid userId,
        string? notes,
        InvoiceStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        uint version,
        IEnumerable<InvoiceItem>? items = null)
    {
        Id = id;
        InvoiceNumber = invoiceNumber;
        CustomerName = customerName;
        PrescriptionNumber = prescriptionNumber;
        PaymentMethod = paymentMethod;
        SubTotal = subTotal;
        Discount = discount;
        Tax = tax;
        TotalAmount = totalAmount;
        UserId = userId;
        Notes = notes;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        Version = version;

        if (items is not null)
        {
            _items.AddRange(items);
        }
    }

    public static Invoice Create(
        string invoiceNumber,
        Guid userId,
        PaymentMethod paymentMethod,
        Money subTotal,
        Money discount,
        Money tax,
        Money totalAmount,
        string? customerName,
        string? prescriptionNumber,
        string? notes,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            throw new ArgumentException("Invoice number cannot be empty.", nameof(invoiceNumber));

        if (userId == Guid.Empty)
            throw new ArgumentException("User ID is required.", nameof(userId));

        return new Invoice(
            id: Guid.NewGuid(),
            invoiceNumber: invoiceNumber.Trim(),
            customerName: string.IsNullOrWhiteSpace(customerName) ? null : customerName.Trim(),
            prescriptionNumber: string.IsNullOrWhiteSpace(prescriptionNumber) ? null : prescriptionNumber.Trim(),
            paymentMethod: paymentMethod,
            subTotal: subTotal,
            discount: discount,
            tax: tax,
            totalAmount: totalAmount,
            userId: userId,
            notes: string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            status: InvoiceStatus.Completed,
            createdAt: now,
            updatedAt: now,
            version: 1);
    }

    public static Invoice Load(
        Guid id,
        string invoiceNumber,
        string? customerName,
        string? prescriptionNumber,
        PaymentMethod paymentMethod,
        Money subTotal,
        Money discount,
        Money tax,
        Money totalAmount,
        Guid userId,
        string? notes,
        InvoiceStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        uint version,
        IEnumerable<InvoiceItem>? items = null)
    {
        return new Invoice(
            id,
            invoiceNumber,
            customerName,
            prescriptionNumber,
            paymentMethod,
            subTotal,
            discount,
            tax,
            totalAmount,
            userId,
            notes,
            status,
            createdAt,
            updatedAt,
            version,
            items);
    }

    public void AddItem(InvoiceItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _items.Add(item);
    }

    public void AddItems(IEnumerable<InvoiceItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items.AddRange(items);
    }

    public void MarkCancelled(string? cancellationReason, DateTimeOffset now)
    {
        Status = InvoiceStatus.Cancelled;
        Notes = string.IsNullOrWhiteSpace(Notes)
            ? $"Cancelled: {cancellationReason}"
            : $"{Notes} | Cancelled: {cancellationReason}";
        UpdatedAt = now;
        Version++;
    }

    public void MarkRefunded(string? refundReason, DateTimeOffset now)
    {
        Status = InvoiceStatus.Refunded;
        Notes = string.IsNullOrWhiteSpace(Notes)
            ? $"Refunded: {refundReason}"
            : $"{Notes} | Refunded: {refundReason}";
        UpdatedAt = now;
        Version++;
    }
}
