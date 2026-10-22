using PharmacyApp.Domain.Entities;

namespace PharmacyApp.Application.Inventory;

/// <summary>
/// Data transfer object for physical inventory batches.
/// </summary>
public sealed record BatchDto(
    Guid Id,
    Guid MedicineId,
    string MedicineName,
    string BatchCode,
    string? ManufacturerBatchNumber,
    DateOnly? ExpiryDate,
    int QuantityOnHand,
    decimal PurchaseCostNpr,
    decimal SellingPriceNpr,
    string? Location,
    bool IsActive,
    uint Version,
    bool IsExpired,
    bool HasKnownExpiry)
{
    public static BatchDto FromDomain(Batch batch, string medicineName, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(batch);

        return new BatchDto(
            Id: batch.Id,
            MedicineId: batch.MedicineId,
            MedicineName: medicineName,
            BatchCode: batch.BatchCode,
            ManufacturerBatchNumber: batch.ManufacturerBatchNumber,
            ExpiryDate: batch.ExpiryDate,
            QuantityOnHand: batch.QuantityOnHand,
            PurchaseCostNpr: batch.PurchaseCost.Amount,
            SellingPriceNpr: batch.SellingPrice.Amount,
            Location: batch.Location,
            IsActive: batch.IsActive,
            Version: batch.Version,
            IsExpired: batch.IsExpired(today),
            HasKnownExpiry: batch.HasKnownExpiry);
    }
}
