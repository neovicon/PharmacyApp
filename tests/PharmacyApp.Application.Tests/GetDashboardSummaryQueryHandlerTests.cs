using PharmacyApp.Application.Dashboard;
using PharmacyApp.Application.Tests.TestDoubles;
using PharmacyApp.Domain.Entities;
using PharmacyApp.Domain.Enums;
using PharmacyApp.Domain.ValueObjects;
using Xunit;

namespace PharmacyApp.Application.Tests;

public class GetDashboardSummaryQueryHandlerTests
{
    private readonly TestMedicineRepository _medicineRepository = new();
    private readonly TestBatchRepository _batchRepository = new();
    private readonly TestStockAdjustmentRepository _stockAdjustmentRepository = new();
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero));
    private readonly GetDashboardSummaryQueryHandler _handler;

    public GetDashboardSummaryQueryHandlerTests()
    {
        _handler = new GetDashboardSummaryQueryHandler(
            _medicineRepository,
            _batchRepository,
            _stockAdjustmentRepository,
            _dateTimeProvider);
    }

    [Fact]
    public async Task HandleAsync_AggregatesCountsValuesAndAlertsAccurately()
    {
        // 1. Medicine with low stock (reorder level = 50, batch has 30 units)
        var med1 = Medicine.Create("Med A", "Gen A", "Tablet", "Strip", 50);
        await _medicineRepository.AddAsync(med1);
        var b1 = Batch.Create(med1.Id, "B1", 30, Money.FromNpr(10m), Money.FromNpr(20m), expiryDate: new DateOnly(2027, 5, 1));
        await _batchRepository.AddAsync(b1);

        // 2. Medicine with healthy stock but batch expiring soon (expiring in 20 days)
        var med2 = Medicine.Create("Med B", "Gen B", "Syrup", "Bottle", 10);
        await _medicineRepository.AddAsync(med2);
        var b2 = Batch.Create(med2.Id, "B2", 100, Money.FromNpr(50m), Money.FromNpr(80m), expiryDate: new DateOnly(2026, 11, 5));
        await _batchRepository.AddAsync(b2);

        // Record an audit adjustment
        var adj = StockAdjustment.Create(b1.Id, med1.Id, Guid.NewGuid(), StockAdjustmentType.InitialStock, 0, 30, "Initial intake");
        await _stockAdjustmentRepository.AddAsync(adj);

        var result = await _handler.HandleAsync(new GetDashboardSummaryQuery());

        Assert.True(result.IsSuccess);
        var summary = result.Value;

        Assert.Equal(2, summary.TotalMedicinesCount);
        Assert.Equal(2, summary.ActiveMedicinesCount);
        Assert.Equal(2, summary.TotalBatchesCount);
        Assert.Equal(130, summary.TotalUnitsInStock);

        // Cost = (30 * 10) + (100 * 50) = 300 + 5000 = 5300
        Assert.Equal(5300m, summary.TotalValuationCostNpr);
        // Retail = (30 * 20) + (100 * 80) = 600 + 8000 = 8600
        Assert.Equal(8600m, summary.TotalValuationRetailNpr);

        // Med A is low stock
        Assert.Single(summary.LowStockMedicines);
        Assert.Equal("Med A", summary.LowStockMedicines[0].Name);

        // B2 is expiring soon (< 90 days)
        Assert.Single(summary.ExpiringBatches);
        Assert.Equal("B2", summary.ExpiringBatches[0].BatchCode);

        // Recent activity feed contains adjustment
        Assert.Single(summary.RecentActivities);
    }
}
