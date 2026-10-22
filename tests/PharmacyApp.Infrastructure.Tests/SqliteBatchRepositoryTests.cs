using Microsoft.Data.Sqlite;
using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Exceptions;
using PharmacyApp.Domain.Entities;
using PharmacyApp.Domain.ValueObjects;
using PharmacyApp.Infrastructure.Persistence;
using Xunit;

namespace PharmacyApp.Infrastructure.Tests;

public class SqliteBatchRepositoryTests : IDisposable
{
    private readonly IsolatedTestPathProvider _pathProvider;
    private readonly string _connectionString;
    private readonly SqliteDatabaseInitializer _initializer;
    private readonly SqliteMedicineRepository _medicineRepository;
    private readonly SqliteBatchRepository _repository;
    private readonly Medicine _seedMedicine;

    public SqliteBatchRepositoryTests()
    {
        _pathProvider = new IsolatedTestPathProvider();
        _connectionString = $"Data Source={Path.Combine(_pathProvider.DataDirectory, "pharmacy.db")};Cache=Shared;Foreign Keys=True;";
        _initializer = new SqliteDatabaseInitializer(_connectionString);
        _initializer.InitializeAsync().GetAwaiter().GetResult();
        _medicineRepository = new SqliteMedicineRepository(_connectionString);
        _repository = new SqliteBatchRepository(_connectionString);

        _seedMedicine = Medicine.Create("Paracetamol 500mg", "Paracetamol", "Tablet", "Strip", 20);
        _medicineRepository.AddAsync(_seedMedicine).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        _pathProvider.Dispose();
    }

    [Fact]
    public async Task AddAndGetByIdAsync_PersistsAllFieldsAndPreservesPaisaPrecision()
    {
        var timestamp = new DateTimeOffset(2026, 10, 20, 11, 0, 0, TimeSpan.Zero);
        var expiry = new DateOnly(2027, 6, 30);
        var cost = Money.FromNpr(150.75m); // 15075 paisa
        var price = Money.FromNpr(200.50m); // 20050 paisa

        var batch = Batch.Create(
            medicineId: _seedMedicine.Id,
            batchCode: "BTH-2026-A1",
            quantityOnHand: 150,
            purchaseCost: cost,
            sellingPrice: price,
            manufacturerBatchNumber: "MFG-8877",
            expiryDate: expiry,
            location: "Shelf B-3",
            now: timestamp);

        await _repository.AddAsync(batch);

        var retrieved = await _repository.GetByIdAsync(batch.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(batch.Id, retrieved.Id);
        Assert.Equal(_seedMedicine.Id, retrieved.MedicineId);
        Assert.Equal("BTH-2026-A1", retrieved.BatchCode);
        Assert.Equal("MFG-8877", retrieved.ManufacturerBatchNumber);
        Assert.Equal(expiry, retrieved.ExpiryDate);
        Assert.Equal(150, retrieved.QuantityOnHand);
        Assert.Equal(cost, retrieved.PurchaseCost);
        Assert.Equal(15075L, retrieved.PurchaseCost.ToPaisa());
        Assert.Equal(price, retrieved.SellingPrice);
        Assert.Equal(20050L, retrieved.SellingPrice.ToPaisa());
        Assert.Equal("Shelf B-3", retrieved.Location);
        Assert.True(retrieved.IsActive);
        Assert.Equal(timestamp, retrieved.CreatedAt);
        Assert.Equal(timestamp, retrieved.UpdatedAt);
        Assert.Equal(1u, retrieved.Version);
    }

    [Fact]
    public async Task NullableFields_ArePersistedAsNull()
    {
        var batch = Batch.Create(
            medicineId: _seedMedicine.Id,
            batchCode: "BTH-MINIMAL",
            quantityOnHand: 50,
            purchaseCost: Money.FromNpr(10m),
            sellingPrice: Money.FromNpr(15m),
            manufacturerBatchNumber: null,
            expiryDate: null,
            location: null);

        await _repository.AddAsync(batch);

        var retrieved = await _repository.GetByIdAsync(batch.Id);

        Assert.NotNull(retrieved);
        Assert.Null(retrieved.ManufacturerBatchNumber);
        Assert.Null(retrieved.ExpiryDate);
        Assert.Null(retrieved.Location);
        Assert.False(retrieved.HasKnownExpiry);
    }

    [Theory]
    [InlineData("BTH-990", "BTH-990")]
    [InlineData("BTH-990", "bth-990")]
    [InlineData("BTH-990", "  bth-990  ")]
    public async Task GetByBatchCodeAsync_IsCaseInsensitive(string storedCode, string queryCode)
    {
        var batch = Batch.Create(_seedMedicine.Id, storedCode, 10, Money.FromNpr(1m), Money.FromNpr(2m));
        await _repository.AddAsync(batch);

        var retrieved = await _repository.GetByBatchCodeAsync(queryCode);

        Assert.NotNull(retrieved);
        Assert.Equal(batch.Id, retrieved.Id);
    }

    [Fact]
    public async Task AddAsync_DuplicateBatchCode_ThrowsDuplicateBatchCodeException()
    {
        var batch1 = Batch.Create(_seedMedicine.Id, "DUP-CODE", 10, Money.FromNpr(5m), Money.FromNpr(10m));
        await _repository.AddAsync(batch1);

        var batch2 = Batch.Create(_seedMedicine.Id, "DUP-CODE", 20, Money.FromNpr(5m), Money.FromNpr(10m));

        var ex = await Assert.ThrowsAsync<DuplicateBatchCodeException>(() => _repository.AddAsync(batch2));
        Assert.Equal("DUP-CODE", ex.BatchCode);
    }

    [Fact]
    public async Task AddAsync_InvalidMedicineId_ThrowsForeignKeyConstraintException()
    {
        Guid nonExistentMedicineId = Guid.NewGuid();
        var batch = Batch.Create(nonExistentMedicineId, "ORPHAN-BATCH", 10, Money.FromNpr(5m), Money.FromNpr(10m));

        await Assert.ThrowsAsync<ForeignKeyConstraintException>(() => _repository.AddAsync(batch));
    }

    [Fact]
    public async Task GetByMedicineIdAsync_OrdersKnownExpiryFirstThenUnknownExpiry()
    {
        var b1 = Batch.Create(_seedMedicine.Id, "BATCH-LATE", 10, Money.FromNpr(1m), Money.FromNpr(2m), expiryDate: new DateOnly(2028, 1, 1));
        var b2 = Batch.Create(_seedMedicine.Id, "BATCH-UNKNOWN", 10, Money.FromNpr(1m), Money.FromNpr(2m), expiryDate: null);
        var b3 = Batch.Create(_seedMedicine.Id, "BATCH-EARLY", 10, Money.FromNpr(1m), Money.FromNpr(2m), expiryDate: new DateOnly(2026, 12, 1));

        await _repository.AddAsync(b1);
        await _repository.AddAsync(b2);
        await _repository.AddAsync(b3);

        var batches = await _repository.GetByMedicineIdAsync(_seedMedicine.Id);

        Assert.Equal(3, batches.Count);
        Assert.Equal("BATCH-EARLY", batches[0].BatchCode);
        Assert.Equal("BATCH-LATE", batches[1].BatchCode);
        Assert.Equal("BATCH-UNKNOWN", batches[2].BatchCode);
    }

    [Fact]
    public async Task Filtering_ValidAsOf_ExcludesUnknownExpiry()
    {
        var today = new DateOnly(2026, 10, 20);
        var futureValid = Batch.Create(_seedMedicine.Id, "VALID-FUTURE", 10, Money.FromNpr(1m), Money.FromNpr(2m), expiryDate: new DateOnly(2027, 1, 1));
        var alreadyExpired = Batch.Create(_seedMedicine.Id, "EXPIRED-PAST", 10, Money.FromNpr(1m), Money.FromNpr(2m), expiryDate: new DateOnly(2026, 5, 1));
        var unknownExpiry = Batch.Create(_seedMedicine.Id, "UNKNOWN-EXPIRY", 10, Money.FromNpr(1m), Money.FromNpr(2m), expiryDate: null);

        await _repository.AddAsync(futureValid);
        await _repository.AddAsync(alreadyExpired);
        await _repository.AddAsync(unknownExpiry);

        // When querying valid (safe) batches as of today, unknown expiry must NEVER be included
        var validBatches = await _repository.ListAsync(new BatchFilter { ValidAsOf = today });

        Assert.Single(validBatches);
        Assert.Equal("VALID-FUTURE", validBatches[0].BatchCode);
    }

    [Fact]
    public async Task Filtering_ExpiredAsOf_ReturnsOnlyConfirmedExpired_ExcludesUnknown()
    {
        var today = new DateOnly(2026, 10, 20);
        var futureValid = Batch.Create(_seedMedicine.Id, "VALID-FUTURE", 10, Money.FromNpr(1m), Money.FromNpr(2m), expiryDate: new DateOnly(2027, 1, 1));
        var alreadyExpired = Batch.Create(_seedMedicine.Id, "EXPIRED-PAST", 10, Money.FromNpr(1m), Money.FromNpr(2m), expiryDate: new DateOnly(2026, 5, 1));
        var unknownExpiry = Batch.Create(_seedMedicine.Id, "UNKNOWN-EXPIRY", 10, Money.FromNpr(1m), Money.FromNpr(2m), expiryDate: null);

        await _repository.AddAsync(futureValid);
        await _repository.AddAsync(alreadyExpired);
        await _repository.AddAsync(unknownExpiry);

        var expiredBatches = await _repository.GetExpiredBatchesAsync(today);

        Assert.Single(expiredBatches);
        Assert.Equal("EXPIRED-PAST", expiredBatches[0].BatchCode);
    }

    [Fact]
    public async Task Filtering_HasUnknownExpiry_ReturnsOnlyUnknownBatches()
    {
        var known = Batch.Create(_seedMedicine.Id, "KNOWN-BATCH", 10, Money.FromNpr(1m), Money.FromNpr(2m), expiryDate: new DateOnly(2027, 1, 1));
        var unknown = Batch.Create(_seedMedicine.Id, "UNKNOWN-BATCH", 10, Money.FromNpr(1m), Money.FromNpr(2m), expiryDate: null);

        await _repository.AddAsync(known);
        await _repository.AddAsync(unknown);

        var result = await _repository.ListAsync(new BatchFilter { HasUnknownExpiry = true });

        Assert.Single(result);
        Assert.Equal("UNKNOWN-BATCH", result[0].BatchCode);
    }

    [Fact]
    public async Task Filtering_InStockOnly_DistinguishesZeroFromPositiveStock()
    {
        var inStock = Batch.Create(_seedMedicine.Id, "IN-STOCK", 25, Money.FromNpr(1m), Money.FromNpr(2m));
        var outOfStock = Batch.Create(_seedMedicine.Id, "OUT-OF-STOCK", 0, Money.FromNpr(1m), Money.FromNpr(2m));

        await _repository.AddAsync(inStock);
        await _repository.AddAsync(outOfStock);

        var inStockOnly = await _repository.ListAsync(new BatchFilter { InStockOnly = true });
        Assert.Single(inStockOnly);
        Assert.Equal("IN-STOCK", inStockOnly[0].BatchCode);

        var outOfStockOnly = await _repository.ListAsync(new BatchFilter { InStockOnly = false });
        Assert.Single(outOfStockOnly);
        Assert.Equal("OUT-OF-STOCK", outOfStockOnly[0].BatchCode);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesPropertiesAndIncrementsVersion()
    {
        var now = DateTimeOffset.UtcNow;
        var batch = Batch.Create(_seedMedicine.Id, "UPDATE-ME", 100, Money.FromNpr(10m), Money.FromNpr(15m), now: now);
        await _repository.AddAsync(batch);

        var updateTime = now.AddHours(1);
        batch.UpdatePricing(Money.FromNpr(12.50m), Money.FromNpr(18.75m), updateTime);
        batch.UpdateLocation("Rack 9", updateTime);
        batch.UpdateManufacturerBatchNumber("MFG-NEW-1", updateTime);
        batch.SetQuantity(85, updateTime);

        await _repository.UpdateAsync(batch, expectedVersion: 1);

        var retrieved = await _repository.GetByIdAsync(batch.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(Money.FromNpr(12.50m), retrieved.PurchaseCost);
        Assert.Equal(1250L, retrieved.PurchaseCost.ToPaisa());
        Assert.Equal(Money.FromNpr(18.75m), retrieved.SellingPrice);
        Assert.Equal(1875L, retrieved.SellingPrice.ToPaisa());
        Assert.Equal("Rack 9", retrieved.Location);
        Assert.Equal("MFG-NEW-1", retrieved.ManufacturerBatchNumber);
        Assert.Equal(85, retrieved.QuantityOnHand);
        Assert.Equal(updateTime, retrieved.UpdatedAt);
        Assert.Equal(5u, retrieved.Version);
    }

    [Fact]
    public async Task UpdateAsync_WithConflictingVersion_ThrowsConcurrencyException()
    {
        var batch = Batch.Create(_seedMedicine.Id, "BTH-CONCURRENCY", 100, Money.FromNpr(5m), Money.FromNpr(10m));
        await _repository.AddAsync(batch);

        // Simulate thread 1 loading batch (version 1)
        var batchThread1 = await _repository.GetByIdAsync(batch.Id);
        Assert.NotNull(batchThread1);

        // Simulate thread 2 loading batch (version 1) and updating it
        var batchThread2 = await _repository.GetByIdAsync(batch.Id);
        Assert.NotNull(batchThread2);
        batchThread2.SetQuantity(80, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(batchThread2);

        // Verify version in DB is now 2
        var inDb = await _repository.GetByIdAsync(batch.Id);
        Assert.NotNull(inDb);
        Assert.Equal(2u, inDb.Version);
        Assert.Equal(80, inDb.QuantityOnHand);

        // Now thread 1 attempts to update with stale version 1
        batchThread1.SetQuantity(90, DateTimeOffset.UtcNow);
        var ex = await Assert.ThrowsAsync<ConcurrencyException>(() => _repository.UpdateAsync(batchThread1, expectedVersion: 1));
        Assert.Contains("Optimistic concurrency violation", ex.Message);

        // Verify thread 2's data was preserved
        var finalDb = await _repository.GetByIdAsync(batch.Id);
        Assert.NotNull(finalDb);
        Assert.Equal(80, finalDb.QuantityOnHand);
        Assert.Equal(2u, finalDb.Version);
    }

    [Fact]
    public async Task UpdateAsync_DuplicateBatchCodeConflict_ThrowsDuplicateBatchCodeException()
    {
        var b1 = Batch.Create(_seedMedicine.Id, "BATCH-ALPHA", 10, Money.FromNpr(1m), Money.FromNpr(2m));
        var b2 = Batch.Create(_seedMedicine.Id, "BATCH-BETA", 10, Money.FromNpr(1m), Money.FromNpr(2m));
        await _repository.AddAsync(b1);
        await _repository.AddAsync(b2);

        // Attempt to update b2 with b1's BatchCode by direct SQL or if modified
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Batches SET BatchCode = 'BATCH-ALPHA' WHERE Id = @Id;";
        cmd.Parameters.AddWithValue("@Id", b2.Id.ToString());

        var ex = await Assert.ThrowsAsync<SqliteException>(() => cmd.ExecuteNonQueryAsync());
        Assert.Contains("UNIQUE constraint failed: Batches.BatchCode", ex.Message);
    }
}
