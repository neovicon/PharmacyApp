namespace PharmacyApp.Domain.Enums;

/// <summary>
/// Categorizes reasons for manual and operational stock adjustments in the audit ledger.
/// </summary>
public enum StockAdjustmentType
{
    /// <summary>Initial inventory entry when receiving or onboarding a batch.</summary>
    InitialStock = 1,

    /// <summary>Discrepancy correction following a physical stock count or audit.</summary>
    PhysicalCountCorrection = 2,

    /// <summary>Stock removed due to breakage, leakage, contamination, or damaged packaging.</summary>
    Damaged = 3,

    /// <summary>Stock written off and removed from active shelves due to expiry.</summary>
    ExpiredWriteOff = 4,

    /// <summary>Stock returned to manufacturer, wholesaler, or distributor (e.g. recall or overstock).</summary>
    ReturnToSupplier = 5,

    /// <summary>Unexplained shortage or loss identified during inventory audit.</summary>
    TheftOrLoss = 6,

    /// <summary>Stock deducted upon dispensing or completing an over-the-counter sale.</summary>
    SaleDispense = 7,

    /// <summary>Stock added upon receiving goods from a supplier purchase order.</summary>
    PurchaseReceipt = 8,

    /// <summary>Stock returned to inventory from a customer refund or return.</summary>
    ReturnFromCustomer = 9
}
