using System.Data.Common;
using PharmacyApp.Application.Abstractions;
using PharmacyApp.Domain.Entities;

namespace PharmacyApp.Application.Tests.TestDoubles;

public sealed class TestStockAdjustmentRepository : IStockAdjustmentRepository
{
    private readonly List<StockAdjustment> _adjustments = new();

    public IReadOnlyList<StockAdjustment> Adjustments => _adjustments.Select(Clone).ToList();

    public Task AddAsync(StockAdjustment adjustment, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(adjustment);
        _adjustments.Add(Clone(adjustment));
        return Task.CompletedTask;
    }

    public Task<StockAdjustment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        var match = _adjustments.FirstOrDefault(a => a.Id == id);
        return Task.FromResult(match != null ? Clone(match) : null);
    }

    public Task<IReadOnlyList<StockAdjustment>> GetByBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        var matches = _adjustments
            .Where(a => a.BatchId == batchId)
            .OrderByDescending(a => a.TimestampUtc)
            .Select(Clone)
            .ToList();

        return Task.FromResult<IReadOnlyList<StockAdjustment>>(matches);
    }

    public Task<IReadOnlyList<StockAdjustment>> GetByMedicineIdAsync(Guid medicineId, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        var matches = _adjustments
            .Where(a => a.MedicineId == medicineId)
            .OrderByDescending(a => a.TimestampUtc)
            .Select(Clone)
            .ToList();

        return Task.FromResult<IReadOnlyList<StockAdjustment>>(matches);
    }

    public Task<IReadOnlyList<StockAdjustment>> ListRecentAsync(int limit = 50, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        var matches = _adjustments
            .OrderByDescending(a => a.TimestampUtc)
            .Take(limit)
            .Select(Clone)
            .ToList();

        return Task.FromResult<IReadOnlyList<StockAdjustment>>(matches);
    }

    private static StockAdjustment Clone(StockAdjustment a) => StockAdjustment.Load(
        a.Id,
        a.BatchId,
        a.MedicineId,
        a.UserId,
        a.AdjustmentType,
        a.QuantityDelta,
        a.PreviousQuantity,
        a.NewQuantity,
        a.Reason,
        a.TimestampUtc);
}
