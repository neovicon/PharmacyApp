using PharmacyApp.Domain.ValueObjects;

namespace PharmacyApp.Domain.Entities;

/// <summary>
/// Represents a dispensed line item within a customer sales invoice.
/// </summary>
public sealed class InvoiceItem
{
    public Guid Id { get; private set; }
    public Guid InvoiceId { get; private set; }
    public Guid BatchId { get; private set; }
    public Guid MedicineId { get; private set; }
    public string MedicineName { get; private set; }
    public string BatchCode { get; private set; }
    public int Quantity { get; private set; }
    public Money UnitPrice { get; private set; }
    public Money LineTotal { get; private set; }

    private InvoiceItem(
        Guid id,
        Guid invoiceId,
        Guid batchId,
        Guid medicineId,
        string medicineName,
        string batchCode,
        int quantity,
        Money unitPrice,
        Money lineTotal)
    {
        Id = id;
        InvoiceId = invoiceId;
        BatchId = batchId;
        MedicineId = medicineId;
        MedicineName = medicineName;
        BatchCode = batchCode;
        Quantity = quantity;
        UnitPrice = unitPrice;
        LineTotal = lineTotal;
    }

    public static InvoiceItem Create(
        Guid invoiceId,
        Guid batchId,
        Guid medicineId,
        string medicineName,
        string batchCode,
        int quantity,
        Money unitPrice)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");

        if (string.IsNullOrWhiteSpace(medicineName))
            throw new ArgumentException("Medicine name cannot be empty.", nameof(medicineName));

        if (string.IsNullOrWhiteSpace(batchCode))
            throw new ArgumentException("Batch code cannot be empty.", nameof(batchCode));

        Money lineTotal = unitPrice * quantity;

        return new InvoiceItem(
            id: Guid.NewGuid(),
            invoiceId: invoiceId,
            batchId: batchId,
            medicineId: medicineId,
            medicineName: medicineName.Trim(),
            batchCode: batchCode.Trim().ToUpperInvariant(),
            quantity: quantity,
            unitPrice: unitPrice,
            lineTotal: lineTotal);
    }

    public static InvoiceItem Load(
        Guid id,
        Guid invoiceId,
        Guid batchId,
        Guid medicineId,
        string medicineName,
        string batchCode,
        int quantity,
        Money unitPrice,
        Money lineTotal)
    {
        return new InvoiceItem(id, invoiceId, batchId, medicineId, medicineName, batchCode, quantity, unitPrice, lineTotal);
    }
}
