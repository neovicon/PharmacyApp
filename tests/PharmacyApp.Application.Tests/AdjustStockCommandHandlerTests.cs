using PharmacyApp.Application.Inventory.Commands;
using PharmacyApp.Application.Tests.TestDoubles;
using PharmacyApp.Domain.Entities;
using PharmacyApp.Domain.Enums;
using PharmacyApp.Domain.ValueObjects;
using Xunit;

namespace PharmacyApp.Application.Tests;

public class AdjustStockCommandHandlerTests
{
    private readonly TestBatchRepository _batchRepository = new();
    private readonly TestStockAdjustmentRepository _stockAdjustmentRepository = new();
    private readonly TestTransactionRunner _transactionRunner = new();
    private readonly TestCurrentUserService _currentUserService = new();
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero));
    private readonly AdjustStockCommandHandler _handler;

    public AdjustStockCommandHandlerTests()
    {
        _handler = new AdjustStockCommandHandler(
            _batchRepository,
            _stockAdjustmentRepository,
            _transactionRunner,
            _currentUserService,
            _dateTimeProvider);
    }

    private async Task<Batch> SeedBatchAsync(int qty = 50)
    {
        var batch = Batch.Create(
            Guid.NewGuid(),
            "BATCH-001",
            qty,
            Money.FromNpr(10m),
            Money.FromNpr(20m),
            expiryDate: new DateOnly(2027, 6, 30));

        await _batchRepository.AddAsync(batch);
        return batch;
    }

    [Fact]
    public async Task HandleAsync_ValidAdjustmentByAdminOrPharmacist_UpdatesStockAndRecordsLedger()
    {
        var batch = await SeedBatchAsync(50);
        _currentUserService.Role = UserRole.Pharmacist;

        var command = new AdjustStockCommand(
            BatchId: batch.Id,
            NewQuantity: 42,
            AdjustmentType: StockAdjustmentType.Damaged,
            Reason: "8 damaged ampoules found during inspection",
            ExpectedVersion: batch.Version);

        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsSuccess);

        var updated = await _batchRepository.GetByIdAsync(batch.Id);
        Assert.NotNull(updated);
        Assert.Equal(42, updated.QuantityOnHand);

        var adjustments = await _stockAdjustmentRepository.GetByBatchIdAsync(batch.Id);
        Assert.Single(adjustments);
        Assert.Equal(-8, adjustments[0].QuantityDelta);
        Assert.Equal(StockAdjustmentType.Damaged, adjustments[0].AdjustmentType);
        Assert.Equal("8 damaged ampoules found during inspection", adjustments[0].Reason);
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsCashier_RejectsWithAuthorizationFailure()
    {
        var batch = await SeedBatchAsync(50);
        _currentUserService.Role = UserRole.Cashier;

        var command = new AdjustStockCommand(
            batch.Id,
            30,
            StockAdjustmentType.PhysicalCountCorrection,
            "Count adjustment",
            batch.Version);

        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Contains("Access denied", result.Error);
    }

    [Fact]
    public async Task HandleAsync_WithStaleVersion_ReturnsConcurrencyViolation()
    {
        var batch = await SeedBatchAsync(50);
        _currentUserService.Role = UserRole.Admin;

        var command = new AdjustStockCommand(
            batch.Id,
            30,
            StockAdjustmentType.PhysicalCountCorrection,
            "Count adjustment",
            ExpectedVersion: 99); // Stale version

        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Contains("Optimistic concurrency violation", result.Error);
    }

    [Fact]
    public async Task HandleAsync_WithNegativeNewQuantity_ReturnsValidationFailure()
    {
        var batch = await SeedBatchAsync(50);
        _currentUserService.Role = UserRole.Admin;

        var command = new AdjustStockCommand(
            batch.Id,
            -5,
            StockAdjustmentType.PhysicalCountCorrection,
            "Count adjustment",
            batch.Version);

        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Contains("cannot be negative", result.Error);
    }
}
