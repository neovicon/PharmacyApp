using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Common;
using PharmacyApp.Application.Inventory;
using PharmacyApp.Application.Medicines;

namespace PharmacyApp.Application.Dashboard;

public sealed record GetDashboardSummaryQuery;

public sealed class GetDashboardSummaryQueryHandler : IQueryHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    private readonly IMedicineRepository _medicineRepository;
    private readonly IBatchRepository _batchRepository;
    private readonly IStockAdjustmentRepository _stockAdjustmentRepository;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetDashboardSummaryQueryHandler(
        IMedicineRepository medicineRepository,
        IBatchRepository batchRepository,
        IStockAdjustmentRepository stockAdjustmentRepository,
        IDateTimeProvider dateTimeProvider)
    {
        _medicineRepository = medicineRepository;
        _batchRepository = batchRepository;
        _stockAdjustmentRepository = stockAdjustmentRepository;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<DashboardSummaryDto>> HandleAsync(GetDashboardSummaryQuery query, CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = _dateTimeProvider.UtcNow;
        DateOnly today = DateOnly.FromDateTime(now.Date);
        DateOnly expiryCutoff = today.AddDays(90);

        var medicines = await _medicineRepository.ListAsync(cancellationToken: cancellationToken);
        var activeMedicines = medicines.Where(m => m.IsActive).ToList();
        var batches = await _batchRepository.ListAsync(cancellationToken: cancellationToken);
        var activeBatches = batches.Where(b => b.IsActive).ToList();

        // Calculate total units and total stock per medicine
        var stockPerMedicine = activeBatches
            .GroupBy(b => b.MedicineId)
            .ToDictionary(g => g.Key, g => g.Sum(b => b.QuantityOnHand));

        int totalUnits = activeBatches.Sum(b => b.QuantityOnHand);
        decimal totalCost = activeBatches.Sum(b => b.PurchaseCost.Amount * b.QuantityOnHand);
        decimal totalRetail = activeBatches.Sum(b => b.SellingPrice.Amount * b.QuantityOnHand);

        // Low stock medicines: active medicine where total in stock <= reorder level
        var lowStockMedicines = activeMedicines
            .Where(m =>
            {
                stockPerMedicine.TryGetValue(m.Id, out int onHand);
                return onHand <= m.ReorderLevel;
            })
            .Select(MedicineDto.FromDomain)
            .ToList();

        // Expiring or expired batches within 90 days
        var medicineNameMap = medicines.ToDictionary(m => m.Id, m => m.Name);
        var expiringBatches = activeBatches
            .Where(b => b.QuantityOnHand > 0 && b.ExpiryDate.HasValue && b.ExpiryDate.Value <= expiryCutoff)
            .OrderBy(b => b.ExpiryDate)
            .Select(b =>
            {
                medicineNameMap.TryGetValue(b.MedicineId, out var medName);
                return BatchDto.FromDomain(b, medName ?? "Unknown Medicine", today);
            })
            .ToList();

        // Recent activity feed (recent 10 adjustments)
        var recentAdjustments = await _stockAdjustmentRepository.ListRecentAsync(10, cancellationToken);
        var batchMap = activeBatches.ToDictionary(b => b.Id, b => b.BatchCode);

        var recentActivities = new List<StockAdjustmentDto>();
        foreach (var adj in recentAdjustments)
        {
            medicineNameMap.TryGetValue(adj.MedicineId, out var medName);
            batchMap.TryGetValue(adj.BatchId, out var bCode);
            recentActivities.Add(StockAdjustmentDto.FromDomain(adj, bCode ?? "BATCH", medName ?? "Unknown Medicine"));
        }

        var summary = new DashboardSummaryDto(
            TotalMedicinesCount: medicines.Count,
            ActiveMedicinesCount: activeMedicines.Count,
            TotalBatchesCount: batches.Count,
            TotalUnitsInStock: totalUnits,
            LowStockMedicinesCount: lowStockMedicines.Count,
            ExpiringSoonBatchesCount: expiringBatches.Count,
            TotalValuationCostNpr: totalCost,
            TotalValuationRetailNpr: totalRetail,
            RecentActivities: recentActivities,
            LowStockMedicines: lowStockMedicines,
            ExpiringBatches: expiringBatches);

        return Result.Success(summary);
    }
}
