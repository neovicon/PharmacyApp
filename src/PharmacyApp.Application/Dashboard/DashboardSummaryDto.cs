using PharmacyApp.Application.Inventory;
using PharmacyApp.Application.Medicines;

namespace PharmacyApp.Application.Dashboard;

public sealed record DashboardSummaryDto(
    int TotalMedicinesCount,
    int ActiveMedicinesCount,
    int TotalBatchesCount,
    int TotalUnitsInStock,
    int LowStockMedicinesCount,
    int ExpiringSoonBatchesCount,
    decimal TotalValuationCostNpr,
    decimal TotalValuationRetailNpr,
    IReadOnlyList<StockAdjustmentDto> RecentActivities,
    IReadOnlyList<MedicineDto> LowStockMedicines,
    IReadOnlyList<BatchDto> ExpiringBatches);
