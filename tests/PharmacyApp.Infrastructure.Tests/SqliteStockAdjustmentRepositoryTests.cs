using Microsoft.Data.Sqlite;
using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Exceptions;
using PharmacyApp.Domain.Entities;
using PharmacyApp.Domain.Enums;
using PharmacyApp.Domain.ValueObjects;
using PharmacyApp.Infrastructure.Persistence;
using Xunit;

namespace PharmacyApp.Infrastructure.Tests;

public class SqliteStockAdjustmentRepositoryTests : IDisposable
{
    private readonly IsolatedTestPathProvider _pathProvider;
    private readonly string _connectionString;
    private readonly SqliteDatabaseInitializer _initializer;
    private readonly SqliteMedicineRepository _medicineRepository;
    private readonly SqliteBatchRepository _batchRepository;
    private readonly SqliteUserRepository _userRepository;
    private readonly SqliteStockAdjustmentRepository _repository;

    private readonly Medicine _seedMedicine;
    private readonly Batch _seedBatch;
    private readonly User _seedUser;

    public SqliteStockAdjustmentRepositoryTests()
    {
        _pathProvider = new IsolatedTestPathProvider();
        _connectionString = $"Data Source={Path.Combine(_pathProvider.DataDirectory, "pharmacy.db")};Cache=Shared;Foreign Keys=True;";
        _initializer = new SqliteDatabaseInitializer(_connectionString);
        _initializer.InitializeAsync().GetAwaiter().GetResult();

        _medicineRepository = new SqliteMedicineRepository(_connectionString);
        _batchRepository = new SqliteBatchRepository(_connectionString);
        _userRepository = new SqliteUserRepository(_connectionString);
        _repository = new SqliteStockAdjustmentRepository(_connectionString);

        _seedMedicine = Medicine.Create("Amoxicillin 500mg", "Amoxicillin", "Capsule", "Cap", 50);
        _medicineRepository.AddAsync(_seedMedicine).GetAwaiter().GetResult();

        _seedBatch = Batch.Create(_seedMedicine.Id, "BTH-ADJ-TEST", 100, Money.FromNpr(10m), Money.FromNpr(15m));
        _batchRepository.AddAsync(_seedBatch).GetAwaiter().GetResult();

        _seedUser = User.Create("pharmacist_john", "John Doe", "hash", UserRole.Pharmacist, DateTimeOffset.UtcNow);
        _userRepository.AddAsync(_seedUser).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        _pathProvider.Dispose();
    }

    [Fact]
    public async Task AddAndGetByIdAsync_PersistsAllLedgerFieldsCorrectly()
    {
        var timestamp = new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
        var adjustment = StockAdjustment.Create(
            batchId: _seedBatch.Id,
            medicineId: _seedMedicine.Id,
            userId: _seedUser.Id,
            adjustmentType: StockAdjustmentType.PhysicalCountCorrection,
            previousQuantity: 100,
            newQuantity: 95,
            reason: "Audit discrepancy: 5 units expired/damaged",
            timestampUtc: timestamp);

        await _repository.AddAsync(adjustment);

        var retrieved = await _repository.GetByIdAsync(adjustment.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(adjustment.Id, retrieved.Id);
        Assert.Equal(_seedBatch.Id, retrieved.BatchId);
        Assert.Equal(_seedMedicine.Id, retrieved.MedicineId);
        Assert.Equal(_seedUser.Id, retrieved.UserId);
        Assert.Equal(StockAdjustmentType.PhysicalCountCorrection, retrieved.AdjustmentType);
        Assert.Equal(100, retrieved.PreviousQuantity);
        Assert.Equal(-5, retrieved.QuantityDelta);
        Assert.Equal(95, retrieved.NewQuantity);
        Assert.Equal("Audit discrepancy: 5 units expired/damaged", retrieved.Reason);
        Assert.Equal(timestamp, retrieved.TimestampUtc);
    }

    [Fact]
    public async Task GetByBatchId_And_GetByMedicineId_ReturnOrderedList()
    {
        var t1 = new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);
        var t2 = new DateTimeOffset(2026, 10, 20, 11, 0, 0, TimeSpan.Zero);

        var adj1 = StockAdjustment.Create(_seedBatch.Id, _seedMedicine.Id, _seedUser.Id, StockAdjustmentType.PhysicalCountCorrection, 100, 110, "First Adjustment", t1);
        var adj2 = StockAdjustment.Create(_seedBatch.Id, _seedMedicine.Id, _seedUser.Id, StockAdjustmentType.Damaged, 110, 105, "Second Adjustment", t2);

        await _repository.AddAsync(adj1);
        await _repository.AddAsync(adj2);

        var byBatch = await _repository.GetByBatchIdAsync(_seedBatch.Id);
        Assert.Equal(2, byBatch.Count);
        Assert.Equal("Second Adjustment", byBatch[0].Reason); // Newest first
        Assert.Equal("First Adjustment", byBatch[1].Reason);

        var byMedicine = await _repository.GetByMedicineIdAsync(_seedMedicine.Id);
        Assert.Equal(2, byMedicine.Count);
        Assert.Equal("Second Adjustment", byMedicine[0].Reason);
        Assert.Equal("First Adjustment", byMedicine[1].Reason);
    }

    [Fact]
    public async Task ListRecentAsync_RespectsLimit()
    {
        for (int i = 1; i <= 5; i++)
        {
            var adj = StockAdjustment.Create(
                _seedBatch.Id,
                _seedMedicine.Id,
                _seedUser.Id,
                StockAdjustmentType.PhysicalCountCorrection,
                100,
                100 + i,
                $"Adjustment {i}",
                DateTimeOffset.UtcNow.AddMinutes(i));
            await _repository.AddAsync(adj);
        }

        var recent = await _repository.ListRecentAsync(limit: 3);
        Assert.Equal(3, recent.Count);
        Assert.Equal("Adjustment 5", recent[0].Reason);
        Assert.Equal("Adjustment 4", recent[1].Reason);
        Assert.Equal("Adjustment 3", recent[2].Reason);
    }

    [Fact]
    public async Task AddAsync_InvalidBatchOrMedicineFK_ThrowsForeignKeyConstraintException()
    {
        Guid nonExistentBatch = Guid.NewGuid();
        var adjustment = StockAdjustment.Create(
            batchId: nonExistentBatch,
            medicineId: _seedMedicine.Id,
            userId: _seedUser.Id,
            adjustmentType: StockAdjustmentType.PhysicalCountCorrection,
            previousQuantity: 10,
            newQuantity: 15,
            reason: "Orphan adjustment");

        await Assert.ThrowsAsync<ForeignKeyConstraintException>(() => _repository.AddAsync(adjustment));
    }

    [Fact]
    public async Task AddAsync_InvalidUserId_ThrowsForeignKeyConstraintException()
    {
        Guid nonExistentUser = Guid.NewGuid();
        var adjustment = StockAdjustment.Create(
            batchId: _seedBatch.Id,
            medicineId: _seedMedicine.Id,
            userId: nonExistentUser,
            adjustmentType: StockAdjustmentType.PhysicalCountCorrection,
            previousQuantity: 100,
            newQuantity: 105,
            reason: "Invalid user");

        await Assert.ThrowsAsync<ForeignKeyConstraintException>(() => _repository.AddAsync(adjustment));
    }

    [Fact]
    public async Task CheckConstraint_ArithmeticIntegrity_EnforcedByDatabase()
    {
        // Direct SQL insert attempting to bypass domain validation with arithmetic mismatch
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO StockAdjustments (Id, BatchId, MedicineId, AdjustmentType, PreviousQuantity, QuantityDelta, NewQuantity, Reason, UserId, Timestamp)
            VALUES ('adj-bad-math', @BatchId, @MedicineId, 1, 50, 10, 999, 'Fraudulent delta', @UserId, 'now');
            """;
        cmd.Parameters.AddWithValue("@BatchId", _seedBatch.Id.ToString());
        cmd.Parameters.AddWithValue("@MedicineId", _seedMedicine.Id.ToString());
        cmd.Parameters.AddWithValue("@UserId", _seedUser.Id.ToString());

        var ex = await Assert.ThrowsAsync<SqliteException>(() => cmd.ExecuteNonQueryAsync());
        Assert.Contains("CHECK constraint failed: CK_StockAdjustments_Arithmetic", ex.Message);
    }

    [Fact]
    public async Task ImmutabilityTrigger_PreventsUpdateOfExistingStockAdjustment()
    {
        var adjustment = StockAdjustment.Create(
            _seedBatch.Id,
            _seedMedicine.Id,
            _seedUser.Id,
            StockAdjustmentType.PhysicalCountCorrection,
            100,
            90,
            "Count adjustment");
        await _repository.AddAsync(adjustment);

        // Attempting direct UPDATE must be blocked by trigger
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE StockAdjustments SET Reason = 'Tampered Reason' WHERE Id = @Id;";
        cmd.Parameters.AddWithValue("@Id", adjustment.Id.ToString());

        var ex = await Assert.ThrowsAsync<SqliteException>(() => cmd.ExecuteNonQueryAsync());
        Assert.Contains("StockAdjustments ledger entries are immutable and cannot be updated", ex.Message);
    }

    [Fact]
    public async Task ImmutabilityTrigger_PreventsDeleteOfExistingStockAdjustment()
    {
        var adjustment = StockAdjustment.Create(
            _seedBatch.Id,
            _seedMedicine.Id,
            _seedUser.Id,
            StockAdjustmentType.PhysicalCountCorrection,
            100,
            90,
            "Count adjustment");
        await _repository.AddAsync(adjustment);

        // Attempting direct DELETE must be blocked by trigger
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM StockAdjustments WHERE Id = @Id;";
        cmd.Parameters.AddWithValue("@Id", adjustment.Id.ToString());

        var ex = await Assert.ThrowsAsync<SqliteException>(() => cmd.ExecuteNonQueryAsync());
        Assert.Contains("StockAdjustments ledger entries are immutable and cannot be deleted", ex.Message);
    }
}
