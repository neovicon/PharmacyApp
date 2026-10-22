using PharmacyApp.Domain.Entities;
using PharmacyApp.Domain.Enums;

namespace PharmacyApp.Application.Inventory;

/// <summary>
/// Data transfer object for audit ledger entries of stock movements.
/// </summary>
public sealed record StockAdjustmentDto(
    Guid Id,
    Guid BatchId,
    string BatchCode,
    Guid MedicineId,
    string MedicineName,
    Guid UserId,
    StockAdjustmentType AdjustmentType,
    string AdjustmentTypeName,
    int PreviousQuantity,
    int QuantityDelta,
    int NewQuantity,
    string Reason,
    DateTimeOffset TimestampUtc)
{
    public static StockAdjustmentDto FromDomain(
        StockAdjustment adjustment,
        string batchCode,
        string medicineName)
    {
        ArgumentNullException.ThrowIfNull(adjustment);

        return new StockAdjustmentDto(
            Id: adjustment.Id,
            BatchId: adjustment.BatchId,
            BatchCode: batchCode,
            MedicineId: adjustment.MedicineId,
            MedicineName: medicineName,
            UserId: adjustment.UserId,
            AdjustmentType: adjustment.AdjustmentType,
            AdjustmentTypeName: FormatAdjustmentType(adjustment.AdjustmentType),
            PreviousQuantity: adjustment.PreviousQuantity,
            QuantityDelta: adjustment.QuantityDelta,
            NewQuantity: adjustment.NewQuantity,
            Reason: adjustment.Reason,
            TimestampUtc: adjustment.TimestampUtc);
    }

    private static string FormatAdjustmentType(StockAdjustmentType type) => type switch
    {
        StockAdjustmentType.InitialStock => "Initial Intake",
        StockAdjustmentType.PurchaseReceipt => "Purchase Receipt",
        StockAdjustmentType.SaleDispense => "Sale / Dispense",
        StockAdjustmentType.PhysicalCountCorrection => "Physical Count Correction",
        StockAdjustmentType.Damaged => "Damaged / Broken",
        StockAdjustmentType.ExpiredWriteOff => "Expired Write-Off",
        StockAdjustmentType.ReturnToSupplier => "Return to Supplier",
        StockAdjustmentType.ReturnFromCustomer => "Customer Return / Refund",
        StockAdjustmentType.TheftOrLoss => "Theft / Loss",
        _ => type.ToString()
    };
}
