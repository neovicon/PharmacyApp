using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Common;

namespace PharmacyApp.Application.Reports;

public sealed record GetInventoryValuationQuery;

public sealed class GetInventoryValuationQueryHandler : IQueryHandler<GetInventoryValuationQuery, InventoryValuationReportDto>
{
    private readonly IMedicineRepository _medicineRepository;
    private readonly IBatchRepository _batchRepository;

    public GetInventoryValuationQueryHandler(
        IMedicineRepository medicineRepository,
        IBatchRepository batchRepository)
    {
        _medicineRepository = medicineRepository;
        _batchRepository = batchRepository;
    }

    public async Task<Result<InventoryValuationReportDto>> HandleAsync(GetInventoryValuationQuery query, CancellationToken cancellationToken = default)
    {
        var medicines = await _medicineRepository.ListAsync(cancellationToken: cancellationToken);
        var batches = await _batchRepository.ListAsync(cancellationToken: cancellationToken);
        var activeBatches = batches.Where(b => b.IsActive).ToList();

        var batchesByMedicine = activeBatches.GroupBy(b => b.MedicineId).ToDictionary(g => g.Key, g => g.ToList());

        var items = new List<MedicineValuationItemDto>();
        decimal grandCost = 0;
        decimal grandRetail = 0;
        int grandUnits = 0;

        foreach (var med in medicines)
        {
            batchesByMedicine.TryGetValue(med.Id, out var medBatches);
            medBatches ??= new();

            int units = medBatches.Sum(b => b.QuantityOnHand);
            decimal cost = medBatches.Sum(b => b.PurchaseCost.Amount * b.QuantityOnHand);
            decimal retail = medBatches.Sum(b => b.SellingPrice.Amount * b.QuantityOnHand);
            decimal marginPct = retail > 0 ? Math.Round(((retail - cost) / retail) * 100m, 1) : 0m;

            grandUnits += units;
            grandCost += cost;
            grandRetail += retail;

            items.Add(new MedicineValuationItemDto(
                MedicineId: med.Id,
                MedicineName: med.Name,
                GenericName: med.GenericName,
                Category: med.Category,
                TotalUnitsOnHand: units,
                ReorderLevel: med.ReorderLevel,
                TotalCostValueNpr: cost,
                TotalRetailValueNpr: retail,
                MarginPercentage: marginPct));
        }

        decimal grandProfit = grandRetail - grandCost;
        decimal overallMargin = grandRetail > 0 ? Math.Round((grandProfit / grandRetail) * 100m, 1) : 0m;

        var report = new InventoryValuationReportDto(
            TotalDistinctMedicines: medicines.Count,
            TotalBatches: activeBatches.Count,
            TotalUnitsOnHand: grandUnits,
            TotalCostValueNpr: grandCost,
            TotalRetailValueNpr: grandRetail,
            TotalPotentialProfitNpr: grandProfit,
            OverallMarginPercentage: overallMargin,
            Items: items.OrderBy(i => i.MedicineName).ToList());

        return Result.Success(report);
    }
}
