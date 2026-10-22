using PharmacyApp.Domain.Entities;
using PharmacyApp.Domain.Enums;
using Xunit;

namespace PharmacyApp.Domain.Tests;

public class StockAdjustmentTests
{
    private readonly Guid _batchId = Guid.NewGuid();
    private readonly Guid _medicineId = Guid.NewGuid(); 
    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTimeOffset _fixedTime = new(2026, 10, 19, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidIncrease_InitializesCorrectly()
    {
        var adjustment = StockAdjustment.Create(
            batchId: _batchId,
            medicineId: _medicineId,
            userId: _userId,
            adjustmentType: StockAdjustmentType.InitialStock,
            previousQuantity: 0,
            newQuantity: 100,
            reason: "Initial batch intake",
            timestampUtc: _fixedTime);

        Assert.NotEqual(Guid.Empty, adjustment.Id);
        Assert.Equal(_batchId, adjustment.BatchId);
        Assert.Equal(_medicineId, adjustment.MedicineId);
        Assert.Equal(_userId, adjustment.UserId);
        Assert.Equal(StockAdjustmentType.InitialStock, adjustment.AdjustmentType);
        Assert.Equal(0, adjustment.PreviousQuantity);
        Assert.Equal(100, adjustment.NewQuantity);
        Assert.Equal(100, adjustment.QuantityDelta);
        Assert.Equal("Initial batch intake", adjustment.Reason);
        Assert.Equal(_fixedTime, adjustment.TimestampUtc);
        Assert.Equal(_fixedTime, adjustment.CreatedAt);
        Assert.Equal(1u, adjustment.Version);
    }

    [Fact]
    public void Create_WithValidDecrease_ComputesNegativeDelta()
    {
        var adjustment = StockAdjustment.Create(
            batchId: _batchId,
            medicineId: _medicineId,
            userId: _userId,
            adjustmentType: StockAdjustmentType.Damaged,
            previousQuantity: 50,
            newQuantity: 45,
            reason: "5 broken bottles during transit",
            timestampUtc: _fixedTime);

        Assert.Equal(50, adjustment.PreviousQuantity);
        Assert.Equal(45, adjustment.NewQuantity);
        Assert.Equal(-5, adjustment.QuantityDelta);
        Assert.Equal(StockAdjustmentType.Damaged, adjustment.AdjustmentType);
    }

    [Fact]
    public void Create_WithZeroDelta_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            StockAdjustment.Create(
                batchId: _batchId,
                medicineId: _medicineId,
                userId: _userId,
                adjustmentType: StockAdjustmentType.PhysicalCountCorrection,
                previousQuantity: 50,
                newQuantity: 50,
                reason: "Count matches exactly",
                timestampUtc: _fixedTime));
    }

    [Fact]
    public void Create_WithEmptyIds_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            StockAdjustment.Create(Guid.Empty, _medicineId, _userId, StockAdjustmentType.Damaged, 10, 5, "Damaged", _fixedTime));

        Assert.Throws<ArgumentException>(() =>
            StockAdjustment.Create(_batchId, Guid.Empty, _userId, StockAdjustmentType.Damaged, 10, 5, "Damaged", _fixedTime));

        Assert.Throws<ArgumentException>(() =>
            StockAdjustment.Create(_batchId, _medicineId, Guid.Empty, StockAdjustmentType.Damaged, 10, 5, "Damaged", _fixedTime));
    }

    [Fact]
    public void Create_WithNegativeQuantities_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            StockAdjustment.Create(_batchId, _medicineId, _userId, StockAdjustmentType.Damaged, -1, 5, "Reason", _fixedTime));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            StockAdjustment.Create(_batchId, _medicineId, _userId, StockAdjustmentType.Damaged, 10, -5, "Reason", _fixedTime));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyReason_ThrowsArgumentException(string? reason)
    {
        Assert.Throws<ArgumentException>(() =>
            StockAdjustment.Create(_batchId, _medicineId, _userId, StockAdjustmentType.Damaged, 10, 5, reason!, _fixedTime));
    }

    [Fact]
    public void Load_WithConsistentAttributes_LoadsSuccessfully()
    {
        var id = Guid.NewGuid();
        var adjustment = StockAdjustment.Load(
            id: id,
            batchId: _batchId,
            medicineId: _medicineId,
            userId: _userId,
            adjustmentType: StockAdjustmentType.ExpiredWriteOff,
            quantityDelta: -10,
            previousQuantity: 10,
            newQuantity: 0,
            reason: "Expired stock written off",
            timestampUtc: _fixedTime);

        Assert.Equal(id, adjustment.Id);
        Assert.Equal(-10, adjustment.QuantityDelta);
        Assert.Equal(10, adjustment.PreviousQuantity);
        Assert.Equal(0, adjustment.NewQuantity);
    }

    [Fact]
    public void Load_WithInconsistentDelta_ThrowsArgumentException()
    {
        var id = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() =>
            StockAdjustment.Load(
                id: id,
                batchId: _batchId,
                medicineId: _medicineId,
                userId: _userId,
                adjustmentType: StockAdjustmentType.ExpiredWriteOff,
                quantityDelta: -5, // Inconsistent with 10 -> 0 (should be -10)
                previousQuantity: 10,
                newQuantity: 0,
                reason: "Expired stock written off",
                timestampUtc: _fixedTime));
    }
}
