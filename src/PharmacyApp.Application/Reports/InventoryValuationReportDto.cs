namespace PharmacyApp.Application.Reports;

public sealed record MedicineValuationItemDto(
    Guid MedicineId,
    string MedicineName,
    string GenericName,
    string? Category,
    int TotalUnitsOnHand,
    int ReorderLevel,
    decimal TotalCostValueNpr,
    decimal TotalRetailValueNpr,
    decimal MarginPercentage);

public sealed record InventoryValuationReportDto(
    int TotalDistinctMedicines,
    int TotalBatches,
    int TotalUnitsOnHand,
    decimal TotalCostValueNpr,
    decimal TotalRetailValueNpr,
    decimal TotalPotentialProfitNpr,
    decimal OverallMarginPercentage,
    IReadOnlyList<MedicineValuationItemDto> Items);
