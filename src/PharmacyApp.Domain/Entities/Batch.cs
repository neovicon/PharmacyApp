using PharmacyApp.Domain.Common;
using PharmacyApp.Domain.ValueObjects;

namespace PharmacyApp.Domain.Entities;

/// <summary>
/// Represents a specific physical stock lot/batch of a medicine.
/// Contains lot-specific attributes including the distinct internal BatchCode,
/// optional manufacturer lot number, optional expiry date, physical location,
/// whole-unit quantity on hand, and batch-level purchase cost and selling price in NPR.
/// </summary>
public sealed class Batch : EntityBase
{
    public Guid MedicineId { get; private set; }

    /// <summary>
    /// Distinct, stable internal identifier assigned at batch intake/creation.
    /// Used by pharmacy staff to unambiguously distinguish batches even when
    /// manufacturer batch numbers are missing or duplicated.
    /// </summary>
    public string BatchCode { get; private set; }

    /// <summary>
    /// Manufacturer or distributor lot/batch number printed on packaging.
    /// Optional; may be null if missing or not supplied on the physical package.
    /// </summary>
    public string? ManufacturerBatchNumber { get; private set; }

    /// <summary>
    /// Expiration date of the batch.
    /// Optional; a null value signifies that the expiry date is unknown / not recorded,
    /// NOT that the batch never expires or is verified safe to dispense.
    /// </summary>
    public DateOnly? ExpiryDate { get; private set; }

    /// <summary>
    /// Physical stock count in whole dispensing units (sealed packages, bottles, strips, or tablets).
    /// Cannot be negative.
    /// </summary>
    public int QuantityOnHand { get; private set; }

    /// <summary>
    /// Wholesale purchase cost per dispensing unit in Nepalese Rupees (NPR).
    /// </summary>
    public Money PurchaseCost { get; private set; }

    /// <summary>
    /// Retail selling price per dispensing unit in Nepalese Rupees (NPR).
    /// </summary>
    public Money SellingPrice { get; private set; }

    /// <summary>
    /// Optional free-form shelf, rack, or storage location label.
    /// </summary>
    public string? Location { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>
    /// Indicates whether this batch has a verified expiration date on record.
    /// </summary>
    public bool HasKnownExpiry => ExpiryDate.HasValue;

    private Batch(
        Guid id,
        Guid medicineId,
        string batchCode,
        string? manufacturerBatchNumber,
        DateOnly? expiryDate,
        int quantityOnHand,
        Money purchaseCost,
        Money sellingPrice,
        string? location,
        bool isActive,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        uint version)
    {
        Id = id;
        MedicineId = medicineId;
        BatchCode = batchCode;
        ManufacturerBatchNumber = manufacturerBatchNumber;
        ExpiryDate = expiryDate;
        QuantityOnHand = quantityOnHand;
        PurchaseCost = purchaseCost;
        SellingPrice = sellingPrice;
        Location = location;
        IsActive = isActive;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        Version = version;
    }

    /// <summary>
    /// Factory method for creating a new batch entry.
    /// BatchCode is mandatory and permanent. Past expiry dates are permitted at creation
    /// so legacy/existing inventory can be accurately recorded.
    /// </summary>
    public static Batch Create(
        Guid medicineId,
        string batchCode,
        int quantityOnHand,
        Money purchaseCost,
        Money sellingPrice,
        string? manufacturerBatchNumber = null,
        DateOnly? expiryDate = null,
        string? location = null,
        DateTimeOffset now = default)
    {
        ValidateCoreAttributes(medicineId, batchCode, quantityOnHand);

        DateTimeOffset timestamp = now == default ? DateTimeOffset.UtcNow : now;

        return new Batch(
            id: Guid.NewGuid(),
            medicineId: medicineId,
            batchCode: batchCode.Trim().ToUpperInvariant(),
            manufacturerBatchNumber: NormalizeOptionalString(manufacturerBatchNumber),
            expiryDate: expiryDate,
            quantityOnHand: quantityOnHand,
            purchaseCost: purchaseCost,
            sellingPrice: sellingPrice,
            location: NormalizeOptionalString(location),
            isActive: true,
            createdAt: timestamp,
            updatedAt: timestamp,
            version: 1);
    }

    /// <summary>
    /// Factory method used to reconstitute an existing Batch from persistence storage.
    /// </summary>
    public static Batch Load(
        Guid id,
        Guid medicineId,
        string batchCode,
        string? manufacturerBatchNumber,
        DateOnly? expiryDate,
        int quantityOnHand,
        Money purchaseCost,
        Money sellingPrice,
        string? location,
        bool isActive,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        uint version)
    {
        ValidateCoreAttributes(medicineId, batchCode, quantityOnHand);

        return new Batch(
            id,
            medicineId,
            batchCode.Trim().ToUpperInvariant(),
            NormalizeOptionalString(manufacturerBatchNumber),
            expiryDate,
            quantityOnHand,
            purchaseCost,
            sellingPrice,
            NormalizeOptionalString(location),
            isActive,
            createdAt,
            updatedAt,
            version);
    }

    /// <summary>
    /// Adjusts the stock quantity by a signed delta.
    /// Throws <see cref="InvalidOperationException"/> if the adjustment would result in negative stock.
    /// </summary>
    public void AdjustQuantity(int delta, DateTimeOffset now)
    {
        int resultingQuantity = QuantityOnHand + delta;
        if (resultingQuantity < 0)
        {
            throw new InvalidOperationException(
                $"Stock adjustment would result in negative quantity ({QuantityOnHand} + {delta} = {resultingQuantity}).");
        }

        QuantityOnHand = resultingQuantity;
        UpdatedAt = now;
        Version++;
    }

    /// <summary>
    /// Sets the stock quantity directly to a specific non-negative value (e.g. following physical count reconciliation).
    /// </summary>
    public void SetQuantity(int newQuantity, DateTimeOffset now)
    {
        if (newQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(newQuantity),
                newQuantity,
                "Quantity on hand cannot be negative.");
        }

        QuantityOnHand = newQuantity;
        UpdatedAt = now;
        Version++;
    }

    /// <summary>
    /// Updates wholesale purchase cost and retail selling price in Nepalese Rupees (NPR).
    /// </summary>
    public void UpdatePricing(Money newPurchaseCost, Money newSellingPrice, DateTimeOffset now)
    {
        PurchaseCost = newPurchaseCost;
        SellingPrice = newSellingPrice;
        UpdatedAt = now;
        Version++;
    }

    /// <summary>
    /// Updates the batch-specific selling price in Nepalese Rupees (NPR).
    /// </summary>
    public void UpdateSellingPrice(Money newSellingPrice, DateTimeOffset now)
    {
        SellingPrice = newSellingPrice;
        UpdatedAt = now;
        Version++;
    }

    /// <summary>
    /// Updates the wholesale purchase cost in Nepalese Rupees (NPR).
    /// </summary>
    public void UpdatePurchaseCost(Money newPurchaseCost, DateTimeOffset now)
    {
        PurchaseCost = newPurchaseCost;
        UpdatedAt = now;
        Version++;
    }

    /// <summary>
    /// Updates the physical storage location label.
    /// </summary>
    public void UpdateLocation(string? newLocation, DateTimeOffset now)
    {
        Location = NormalizeOptionalString(newLocation);
        UpdatedAt = now;
        Version++;
    }

    /// <summary>
    /// Updates the manufacturer batch number if corrected or retroactively supplied.
    /// </summary>
    public void UpdateManufacturerBatchNumber(string? newManufacturerBatchNumber, DateTimeOffset now)
    {
        ManufacturerBatchNumber = NormalizeOptionalString(newManufacturerBatchNumber);
        UpdatedAt = now;
        Version++;
    }

    /// <summary>
    /// Determines whether the batch has reached or passed its expiry date as of a given calendar date.
    /// If ExpiryDate is null (unknown), this returns false (unknown is distinct from verified expired).
    /// </summary>
    public bool IsExpired(DateOnly asOfDate)
    {
        return ExpiryDate.HasValue && ExpiryDate.Value <= asOfDate;
    }

    /// <summary>
    /// Deactivates this batch (e.g. written off, returned to supplier, or recalled).
    /// </summary>
    public void Deactivate(DateTimeOffset now)
    {
        IsActive = false;
        UpdatedAt = now;
        Version++;
    }

    /// <summary>
    /// Reactivates this batch.
    /// </summary>
    public void Reactivate(DateTimeOffset now)
    {
        IsActive = true;
        UpdatedAt = now;
        Version++;
    }

    private static void ValidateCoreAttributes(Guid medicineId, string batchCode, int quantityOnHand)
    {
        if (medicineId == Guid.Empty)
            throw new ArgumentException("Medicine reference ID cannot be empty.", nameof(medicineId));

        if (string.IsNullOrWhiteSpace(batchCode))
            throw new ArgumentException("Internal batch code cannot be empty.", nameof(batchCode));

        if (quantityOnHand < 0)
            throw new ArgumentOutOfRangeException(nameof(quantityOnHand), quantityOnHand, "Quantity on hand cannot be negative.");
    }

    private static string? NormalizeOptionalString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim();
    }
}
