using PharmacyApp.Domain.Common;
using PharmacyApp.Domain.Enums;

namespace PharmacyApp.Domain.Entities;

/// <summary>
/// Represents an immutable audit ledger entry for any manual or operational stock correction.
/// Records the before/after counts, the delta, the responsible user, the reason, and the UTC timestamp.
/// </summary>
public sealed class StockAdjustment : EntityBase
{
    public Guid BatchId { get; private set; }
    public Guid MedicineId { get; private set; }
    public Guid UserId { get; private set; }
    public StockAdjustmentType AdjustmentType { get; private set; }
    public int QuantityDelta { get; private set; }
    public int PreviousQuantity { get; private set; }
    public int NewQuantity { get; private set; }
    public string Reason { get; private set; }
    public DateTimeOffset TimestampUtc { get; private set; }

    private StockAdjustment(
        Guid id,
        Guid batchId,
        Guid medicineId,
        Guid userId,
        StockAdjustmentType adjustmentType,
        int quantityDelta,
        int previousQuantity,
        int newQuantity,
        string reason,
        DateTimeOffset timestampUtc)
    {
        Id = id;
        BatchId = batchId;
        MedicineId = medicineId;
        UserId = userId;
        AdjustmentType = adjustmentType;
        QuantityDelta = quantityDelta;
        PreviousQuantity = previousQuantity;
        NewQuantity = newQuantity;
        Reason = reason;
        TimestampUtc = timestampUtc;
        CreatedAt = timestampUtc;
        UpdatedAt = timestampUtc;
        Version = 1;
    }

    /// <summary>
    /// Factory method for creating a new immutable stock adjustment record.
    /// Validates invariants: non-zero delta, non-negative quantities, consistent math, and non-empty reason.
    /// </summary>
    public static StockAdjustment Create(
        Guid batchId,
        Guid medicineId,
        Guid userId,
        StockAdjustmentType adjustmentType,
        int previousQuantity,
        int newQuantity,
        string reason,
        DateTimeOffset timestampUtc = default)
    {
        ValidateCoreAttributes(batchId, medicineId, userId, previousQuantity, newQuantity, reason);

        int delta = newQuantity - previousQuantity;
        if (delta == 0)
        {
            throw new ArgumentException(
                "Stock adjustment must have a non-zero quantity delta (new quantity must differ from previous quantity).",
                nameof(newQuantity));
        }

        DateTimeOffset timestamp = timestampUtc == default ? DateTimeOffset.UtcNow : timestampUtc;

        return new StockAdjustment(
            id: Guid.NewGuid(),
            batchId: batchId,
            medicineId: medicineId,
            userId: userId,
            adjustmentType: adjustmentType,
            quantityDelta: delta,
            previousQuantity: previousQuantity,
            newQuantity: newQuantity,
            reason: reason.Trim(),
            timestampUtc: timestamp);
    }

    /// <summary>
    /// Factory method used to reconstitute an existing StockAdjustment ledger record from persistence storage.
    /// </summary>
    public static StockAdjustment Load(
        Guid id,
        Guid batchId,
        Guid medicineId,
        Guid userId,
        StockAdjustmentType adjustmentType,
        int quantityDelta,
        int previousQuantity,
        int newQuantity,
        string reason,
        DateTimeOffset timestampUtc)
    {
        ValidateCoreAttributes(batchId, medicineId, userId, previousQuantity, newQuantity, reason);

        if (previousQuantity + quantityDelta != newQuantity)
        {
            throw new ArgumentException(
                $"Inconsistent adjustment state: Previous ({previousQuantity}) + Delta ({quantityDelta}) != New ({newQuantity}).");
        }

        return new StockAdjustment(
            id,
            batchId,
            medicineId,
            userId,
            adjustmentType,
            quantityDelta,
            previousQuantity,
            newQuantity,
            reason.Trim(),
            timestampUtc);
    }

    private static void ValidateCoreAttributes(
        Guid batchId,
        Guid medicineId,
        Guid userId,
        int previousQuantity,
        int newQuantity,
        string reason)
    {
        if (batchId == Guid.Empty)
            throw new ArgumentException("Batch reference ID cannot be empty.", nameof(batchId));

        if (medicineId == Guid.Empty)
            throw new ArgumentException("Medicine reference ID cannot be empty.", nameof(medicineId));

        if (userId == Guid.Empty)
            throw new ArgumentException("Responsible user ID cannot be empty.", nameof(userId));

        if (previousQuantity < 0)
            throw new ArgumentOutOfRangeException(nameof(previousQuantity), previousQuantity, "Previous quantity cannot be negative.");

        if (newQuantity < 0)
            throw new ArgumentOutOfRangeException(nameof(newQuantity), newQuantity, "New quantity cannot be negative.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason for stock adjustment cannot be empty.", nameof(reason));
    }
}
