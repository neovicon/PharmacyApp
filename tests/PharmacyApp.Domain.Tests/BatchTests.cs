using PharmacyApp.Domain.Entities;
using PharmacyApp.Domain.ValueObjects;
using Xunit;

namespace PharmacyApp.Domain.Tests;

public class BatchTests
{
    private readonly Guid _medicineId = Guid.NewGuid();
    private readonly DateTimeOffset _fixedTime = new(2026, 10, 19, 10, 0, 0, TimeSpan.Zero);
    private readonly DateOnly _expiryDate = new(2027, 6, 30);
    private readonly Money _purchaseCost = Money.FromNpr(10.25m);
    private readonly Money _sellingPrice = Money.FromNpr(15.50m);

    [Fact]
    public void Create_WithValidParameters_InitializesCorrectly()
    {
        var batch = Batch.Create(
            medicineId: _medicineId,
            batchCode: " bth-20261019-7k2m9w4p ",
            quantityOnHand: 100,
            purchaseCost: _purchaseCost,
            sellingPrice: _sellingPrice,
            manufacturerBatchNumber: " bn-2024-01 ",
            expiryDate: _expiryDate,
            location: " Shelf A-1 ",
            now: _fixedTime);

        Assert.NotEqual(Guid.Empty, batch.Id);
        Assert.Equal(_medicineId, batch.MedicineId);
        Assert.Equal("BTH-20261019-7K2M9W4P", batch.BatchCode); // Trimmed and uppercase
        Assert.Equal("bn-2024-01", batch.ManufacturerBatchNumber);
        Assert.Equal(_expiryDate, batch.ExpiryDate);
        Assert.True(batch.HasKnownExpiry);
        Assert.Equal(100, batch.QuantityOnHand);
        Assert.Equal(_purchaseCost, batch.PurchaseCost);
        Assert.Equal(_sellingPrice, batch.SellingPrice);
        Assert.Equal("Shelf A-1", batch.Location);
        Assert.True(batch.IsActive);
        Assert.Equal(_fixedTime, batch.CreatedAt);
        Assert.Equal(_fixedTime, batch.UpdatedAt);
        Assert.Equal(1u, batch.Version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithMissingBatchCode_ThrowsArgumentException(string? invalidBatchCode)
    {
        Assert.Throws<ArgumentException>(() =>
            Batch.Create(
                medicineId: _medicineId,
                batchCode: invalidBatchCode!,
                quantityOnHand: 10,
                purchaseCost: _purchaseCost,
                sellingPrice: _sellingPrice,
                now: _fixedTime));
    }

    [Fact]
    public void Create_WithEmptyMedicineId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            Batch.Create(
                medicineId: Guid.Empty,
                batchCode: "BTH-01",
                quantityOnHand: 10,
                purchaseCost: _purchaseCost,
                sellingPrice: _sellingPrice,
                now: _fixedTime));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithMissingManufacturerBatchNumber_NormalizesToNull(string? rawBatchNumber)
    {
        var batch = Batch.Create(
            medicineId: _medicineId,
            batchCode: "BTH-NO-MFG-01",
            quantityOnHand: 25,
            purchaseCost: _purchaseCost,
            sellingPrice: _sellingPrice,
            manufacturerBatchNumber: rawBatchNumber,
            now: _fixedTime);

        Assert.Null(batch.ManufacturerBatchNumber);
    }

    [Fact]
    public void Create_MultipleBatchesWithSameManufacturerBatchNumber_AllowsBothDueToDistinctBatchCode()
    {
        // Two shipments of the same manufacturer batch received on different dates or locations
        var batch1 = Batch.Create(
            medicineId: _medicineId,
            batchCode: "BTH-20261019-AAAAAA01",
            quantityOnHand: 50,
            purchaseCost: _purchaseCost,
            sellingPrice: _sellingPrice,
            manufacturerBatchNumber: "LOT-SHARED-123",
            now: _fixedTime);

        var batch2 = Batch.Create(
            medicineId: _medicineId,
            batchCode: "BTH-20261019-BBBBBB02",
            quantityOnHand: 50,
            purchaseCost: _purchaseCost,
            sellingPrice: _sellingPrice,
            manufacturerBatchNumber: "LOT-SHARED-123",
            now: _fixedTime);

        Assert.Equal(batch1.ManufacturerBatchNumber, batch2.ManufacturerBatchNumber);
        Assert.NotEqual(batch1.BatchCode, batch2.BatchCode);
        Assert.NotEqual(batch1.Id, batch2.Id);
    }

    [Fact]
    public void Create_WithNullExpiryDate_SignifiesUnknownExpiry_NotVerifiedSafe()
    {
        var batch = Batch.Create(
            medicineId: _medicineId,
            batchCode: "BTH-UNKNOWN-EXP",
            quantityOnHand: 30,
            purchaseCost: _purchaseCost,
            sellingPrice: _sellingPrice,
            expiryDate: null,
            now: _fixedTime);

        Assert.Null(batch.ExpiryDate);
        Assert.False(batch.HasKnownExpiry);
        // IsExpired returns false because the date is unverified/unknown, not expired
        Assert.False(batch.IsExpired(new DateOnly(2026, 10, 19)));
    }

    [Fact]
    public void Create_WithPastExpiryDate_IsPermittedForHistoricalRecords()
    {
        var pastDate = new DateOnly(2025, 1, 1);

        var batch = Batch.Create(
            medicineId: _medicineId,
            batchCode: "BTH-HISTORICAL-01",
            quantityOnHand: 20,
            purchaseCost: _purchaseCost,
            sellingPrice: _sellingPrice,
            expiryDate: pastDate,
            now: _fixedTime);

        Assert.Equal(pastDate, batch.ExpiryDate);
        Assert.True(batch.HasKnownExpiry);
        Assert.True(batch.IsExpired(new DateOnly(2026, 10, 19)));
    }

    [Fact]
    public void IsExpired_EvaluatesCorrectlyAgainstGivenDate()
    {
        var batch = Batch.Create(
            medicineId: _medicineId,
            batchCode: "BTH-20261231-TEST",
            quantityOnHand: 50,
            purchaseCost: _purchaseCost,
            sellingPrice: _sellingPrice,
            expiryDate: new DateOnly(2026, 12, 31),
            now: _fixedTime);

        // Before expiry
        Assert.False(batch.IsExpired(new DateOnly(2026, 12, 30)));

        // On expiry date
        Assert.True(batch.IsExpired(new DateOnly(2026, 12, 31)));

        // After expiry date
        Assert.True(batch.IsExpired(new DateOnly(2027, 1, 1)));
    }

    [Fact]
    public void Create_WithNegativeQuantity_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Batch.Create(
                medicineId: _medicineId,
                batchCode: "BTH-NEG-QTY",
                quantityOnHand: -5,
                purchaseCost: _purchaseCost,
                sellingPrice: _sellingPrice,
                now: _fixedTime));
    }

    [Fact]
    public void AdjustQuantity_WithPositiveDelta_IncreasesStock()
    {
        var batch = Batch.Create(_medicineId, "BTH-01", 50, _purchaseCost, _sellingPrice, now: _fixedTime);
        var updateTime = _fixedTime.AddHours(1);

        batch.AdjustQuantity(25, updateTime);

        Assert.Equal(75, batch.QuantityOnHand);
        Assert.Equal(updateTime, batch.UpdatedAt);
        Assert.Equal(2u, batch.Version);
    }

    [Fact]
    public void AdjustQuantity_WithNegativeDelta_DecreasesStock()
    {
        var batch = Batch.Create(_medicineId, "BTH-01", 50, _purchaseCost, _sellingPrice, now: _fixedTime);
        var updateTime = _fixedTime.AddHours(1);

        batch.AdjustQuantity(-20, updateTime);

        Assert.Equal(30, batch.QuantityOnHand);
        Assert.Equal(updateTime, batch.UpdatedAt);
        Assert.Equal(2u, batch.Version);
    }

    [Fact]
    public void AdjustQuantity_ResultingInNegativeQuantity_ThrowsInvalidOperationException()
    {
        var batch = Batch.Create(_medicineId, "BTH-01", 10, _purchaseCost, _sellingPrice, now: _fixedTime);

        Assert.Throws<InvalidOperationException>(() => batch.AdjustQuantity(-11, _fixedTime.AddHours(1)));
    }

    [Fact]
    public void SetQuantity_WithValidQuantity_UpdatesStock()
    {
        var batch = Batch.Create(_medicineId, "BTH-01", 50, _purchaseCost, _sellingPrice, now: _fixedTime);
        var updateTime = _fixedTime.AddHours(2);

        batch.SetQuantity(0, updateTime);

        Assert.Equal(0, batch.QuantityOnHand);
        Assert.Equal(updateTime, batch.UpdatedAt);
        Assert.Equal(2u, batch.Version);
    }

    [Fact]
    public void SetQuantity_WithNegativeQuantity_ThrowsArgumentOutOfRangeException()
    {
        var batch = Batch.Create(_medicineId, "BTH-01", 50, _purchaseCost, _sellingPrice, now: _fixedTime);

        Assert.Throws<ArgumentOutOfRangeException>(() => batch.SetQuantity(-1, _fixedTime.AddHours(1)));
    }

    [Fact]
    public void UpdatePricing_UpdatesBothPurchaseCostAndSellingPrice()
    {
        var batch = Batch.Create(_medicineId, "BTH-01", 50, _purchaseCost, _sellingPrice, now: _fixedTime);
        var newCost = Money.FromNpr(11.00m);
        var newPrice = Money.FromNpr(17.50m);
        var updateTime = _fixedTime.AddDays(1);

        batch.UpdatePricing(newCost, newPrice, updateTime);

        Assert.Equal(newCost, batch.PurchaseCost);
        Assert.Equal(newPrice, batch.SellingPrice);
        Assert.Equal(updateTime, batch.UpdatedAt);
        Assert.Equal(2u, batch.Version);
    }

    [Fact]
    public void UpdateLocation_NormalizesLocationAndUpdatesVersion()
    {
        var batch = Batch.Create(_medicineId, "BTH-01", 50, _purchaseCost, _sellingPrice, location: "Shelf 1", now: _fixedTime);
        var updateTime = _fixedTime.AddDays(1);

        batch.UpdateLocation(" Rack 4 - Bin 2 ", updateTime);

        Assert.Equal("Rack 4 - Bin 2", batch.Location);
        Assert.Equal(updateTime, batch.UpdatedAt);
        Assert.Equal(2u, batch.Version);

        batch.UpdateLocation("   ", updateTime.AddHours(1));
        Assert.Null(batch.Location);
    }

    [Fact]
    public void UpdateManufacturerBatchNumber_NormalizesAndUpdatesVersion()
    {
        var batch = Batch.Create(_medicineId, "BTH-01", 50, _purchaseCost, _sellingPrice, now: _fixedTime);
        var updateTime = _fixedTime.AddDays(1);

        batch.UpdateManufacturerBatchNumber(" LOT-999 ", updateTime);
        Assert.Equal("LOT-999", batch.ManufacturerBatchNumber);
        Assert.Equal(2u, batch.Version);

        batch.UpdateManufacturerBatchNumber("   ", updateTime.AddHours(1));
        Assert.Null(batch.ManufacturerBatchNumber);
        Assert.Equal(3u, batch.Version);
    }

    [Fact]
    public void Deactivate_And_Reactivate_UpdatesStatusAndVersion()
    {
        var batch = Batch.Create(_medicineId, "BTH-01", 50, _purchaseCost, _sellingPrice, now: _fixedTime);
        var deactivateTime = _fixedTime.AddDays(1);

        batch.Deactivate(deactivateTime);
        Assert.False(batch.IsActive);
        Assert.Equal(2u, batch.Version);

        var reactivateTime = _fixedTime.AddDays(2);
        batch.Reactivate(reactivateTime);
        Assert.True(batch.IsActive);
        Assert.Equal(3u, batch.Version);
    }
}
