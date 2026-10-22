using PharmacyApp.Application.Inventory.Commands;
using PharmacyApp.Application.Tests.TestDoubles;
using PharmacyApp.Domain.Entities;
using PharmacyApp.Domain.Enums;
using Xunit;

namespace PharmacyApp.Application.Tests;

public class ReceiveBatchCommandHandlerTests
{
    private readonly TestMedicineRepository _medicineRepository = new();
    private readonly TestBatchRepository _batchRepository = new();
    private readonly TestStockAdjustmentRepository _stockAdjustmentRepository = new();
    private readonly TestTransactionRunner _transactionRunner = new();
    private readonly TestCurrentUserService _currentUserService = new();
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero));
    private readonly ReceiveBatchCommandHandler _handler;

    public ReceiveBatchCommandHandlerTests()
    {
        _handler = new ReceiveBatchCommandHandler(
            _medicineRepository,
            _batchRepository,
            _stockAdjustmentRepository,
            _transactionRunner,
            _currentUserService,
            _dateTimeProvider);
    }

    private async Task<Medicine> SeedActiveMedicineAsync(string name = "Paracetamol 500mg")
    {
        var medicine = Medicine.Create(name, "Paracetamol", "Tablet", "Strip", 20);
        await _medicineRepository.AddAsync(medicine);
        return medicine;
    }

    [Fact]
    public async Task HandleAsync_WithValidNewBatch_CreatesBatchAndAuditLedger()
    {
        var medicine = await SeedActiveMedicineAsync();

        var command = new ReceiveBatchCommand(
            MedicineId: medicine.Id,
            BatchCode: "B-2026-001",
            Quantity: 100,
            PurchaseCostNpr: 15.50m,
            SellingPriceNpr: 25.00m,
            ManufacturerBatchNumber: "MFG-8899",
            ExpiryDate: new DateOnly(2027, 12, 31),
            Location: "Shelf A-1",
            Reason: "Initial delivery",
            IsPurchaseOrder: true);

        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value);

        var batch = await _batchRepository.GetByIdAsync(result.Value);
        Assert.NotNull(batch);
        Assert.Equal("B-2026-001", batch.BatchCode);
        Assert.Equal(100, batch.QuantityOnHand);
        Assert.Equal(15.50m, batch.PurchaseCost.Amount);
        Assert.Equal(25.00m, batch.SellingPrice.Amount);
        Assert.Equal("Shelf A-1", batch.Location);

        var adjustments = await _stockAdjustmentRepository.GetByBatchIdAsync(batch.Id);
        Assert.Single(adjustments);
        Assert.Equal(StockAdjustmentType.PurchaseReceipt, adjustments[0].AdjustmentType);
        Assert.Equal(0, adjustments[0].PreviousQuantity);
        Assert.Equal(100, adjustments[0].NewQuantity);
        Assert.Equal(100, adjustments[0].QuantityDelta);
    }

    [Fact]
    public async Task HandleAsync_ExistingBatchCodeForSameMedicine_IncrementsQuantity()
    {
        var medicine = await SeedActiveMedicineAsync();

        var command1 = new ReceiveBatchCommand(medicine.Id, "BATCH-SAME", 50, 10m, 20m);
        var res1 = await _handler.HandleAsync(command1);
        Assert.True(res1.IsSuccess);

        var command2 = new ReceiveBatchCommand(medicine.Id, "batch-same", 30, 12m, 22m);
        var res2 = await _handler.HandleAsync(command2);
        Assert.True(res2.IsSuccess);

        var batch = await _batchRepository.GetByIdAsync(res1.Value);
        Assert.NotNull(batch);
        Assert.Equal(80, batch.QuantityOnHand);
        Assert.Equal(12m, batch.PurchaseCost.Amount);

        var adjustments = await _stockAdjustmentRepository.GetByBatchIdAsync(batch.Id);
        Assert.Equal(2, adjustments.Count);
    }

    [Fact]
    public async Task HandleAsync_WhenBatchCodeBelongsToDifferentMedicine_ReturnsFailure()
    {
        var med1 = await SeedActiveMedicineAsync("Medicine One");
        var med2 = await SeedActiveMedicineAsync("Medicine Two");

        await _handler.HandleAsync(new ReceiveBatchCommand(med1.Id, "SHARED-CODE", 50, 10m, 20m));

        var result = await _handler.HandleAsync(new ReceiveBatchCommand(med2.Id, "shared-code", 30, 10m, 20m));

        Assert.True(result.IsFailure);
        Assert.Contains("already belongs to a different medicine", result.Error);
    }

    [Fact]
    public async Task HandleAsync_WithZeroOrNegativeQuantity_ReturnsValidationFailure()
    {
        var medicine = await SeedActiveMedicineAsync();
        var result = await _handler.HandleAsync(new ReceiveBatchCommand(medicine.Id, "B-TEST", 0, 10m, 20m));

        Assert.True(result.IsFailure);
        Assert.Contains("must be greater than zero", result.Error);
    }
}
