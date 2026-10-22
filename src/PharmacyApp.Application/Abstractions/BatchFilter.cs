namespace PharmacyApp.Application.Abstractions;

/// <summary>
/// Criteria used for filtering and querying inventory batches.
/// </summary>
public sealed record BatchFilter
{
    /// <summary>
    /// Filter by active/inactive status. Null returns both active and inactive.
    /// </summary>
    public bool? IsActive { get; init; }

    /// <summary>
    /// Filter by stock level: true returns batches with QuantityOnHand > 0;
    /// false returns out-of-stock batches (QuantityOnHand = 0); null returns all.
    /// </summary>
    public bool? InStockOnly { get; init; }

    /// <summary>
    /// Filter by expiry: if set, returns batches where ExpiryDate is known and <= ExpiredAsOf.
    /// Unknown expiry (null) is excluded.
    /// </summary>
    public DateOnly? ExpiredAsOf { get; init; }

    /// <summary>
    /// Filter by validity: if set, returns batches where ExpiryDate is known and > ValidAsOf.
    /// Note: Unknown expiry (null) is excluded from valid batches because unknown expiry is NOT proof of safety.
    /// </summary>
    public DateOnly? ValidAsOf { get; init; }

    /// <summary>
    /// If true, returns only batches where ExpiryDate is null (unknown expiry).
    /// </summary>
    public bool? HasUnknownExpiry { get; init; }

    /// <summary>
    /// Optional filter to restrict results to a specific medicine ID.
    /// </summary>
    public Guid? MedicineId { get; init; }
}
