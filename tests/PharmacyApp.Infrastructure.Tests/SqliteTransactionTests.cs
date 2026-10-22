using Microsoft.Data.Sqlite;
using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Exceptions;
using PharmacyApp.Domain.Entities;
using PharmacyApp.Domain.Enums;
using PharmacyApp.Domain.ValueObjects;
using PharmacyApp.Infrastructure.Persistence;
using PharmacyApp.Infrastructure.Security;
using Xunit;

namespace PharmacyApp.Infrastructure.Tests;

public class SqliteTransactionTests : IDisposable
{
    private readonly IsolatedTestPathProvider _pathProvider;
    private readonly string _connectionString;
    private readonly SqliteDatabaseInitializer _initializer;
    private readonly SqliteTransactionRunner _transactionRunner;
    private readonly SqliteMedicineRepository _medicineRepository;
    private readonly SqliteBatchRepository _batchRepository;
    private readonly SqliteStockAdjustmentRepository _adjustmentRepository;
    private readonly SqliteUserRepository _userRepository;

    public SqliteTransactionTests()
    {
        _pathProvider = new IsolatedTestPathProvider();
        _connectionString = $"Data Source={Path.Combine(_pathProvider.DataDirectory, "pharmacy.db")};Cache=Shared;Foreign Keys=True;";
        _initializer = new SqliteDatabaseInitializer(_connectionString);
        _initializer.InitializeAsync().GetAwaiter().GetResult();

        _transactionRunner = new SqliteTransactionRunner(_connectionString);
        _medicineRepository = new SqliteMedicineRepository(_connectionString);
        _batchRepository = new SqliteBatchRepository(_connectionString);
        _adjustmentRepository = new SqliteStockAdjustmentRepository(_connectionString);
        _userRepository = new SqliteUserRepository(_connectionString);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        _pathProvider.Dispose();
    }

    [Fact]
    public async Task MultiStepWrite_WhenSubsequentStepFails_RollsBackAllChanges()
    {
        // 1. Setup seed user and medicine
        var user = User.Create("admin", "Admin", "hash", UserRole.Admin, DateTimeOffset.UtcNow);
        await _userRepository.AddAsync(user);

        var med = Medicine.Create("Paracetamol 500mg", "Paracetamol", "Tablet", "Strip", 10);
        await _medicineRepository.AddAsync(med);

        var batch = Batch.Create(med.Id, "BTH-TX-1", 100, Money.FromNpr(10m), Money.FromNpr(15m));
        await _batchRepository.AddAsync(batch);

        // 2. Execute a transaction where step 1 modifies batch quantity, but step 2 fails with invalid FK
        Guid invalidUserId = Guid.NewGuid(); // Foreign key violation on StockAdjustments.UserId
        batch.SetQuantity(75, DateTimeOffset.UtcNow);

        var invalidAdjustment = StockAdjustment.Create(
            batch.Id,
            med.Id,
            invalidUserId,
            StockAdjustmentType.PhysicalCountCorrection,
            100,
            75,
            "Reconciliation with invalid user");

        await Assert.ThrowsAsync<ForeignKeyConstraintException>(() =>
            _transactionRunner.ExecuteInTransactionAsync(async tx =>
            {
                await _batchRepository.UpdateAsync(batch, transaction: tx);
                await _adjustmentRepository.AddAsync(invalidAdjustment, transaction: tx);
            }));

        // 3. Verify batch in database was rolled back to original 100 quantity
        var reloadedBatch = await _batchRepository.GetByIdAsync(batch.Id);
        Assert.NotNull(reloadedBatch);
        Assert.Equal(100, reloadedBatch.QuantityOnHand);

        // 4. Verify no adjustment record was saved
        var adjustments = await _adjustmentRepository.GetByBatchIdAsync(batch.Id);
        Assert.Empty(adjustments);
    }

    [Fact]
    public async Task MultiStepWrite_WhenAllStepsSucceed_CommitsTogether()
    {
        var user = User.Create("admin", "Admin", "hash", UserRole.Admin, DateTimeOffset.UtcNow);
        await _userRepository.AddAsync(user);

        var med = Medicine.Create("Amoxicillin 500mg", "Amoxicillin", "Capsule", "Cap", 10);
        await _medicineRepository.AddAsync(med);

        var batch = Batch.Create(med.Id, "BTH-TX-2", 50, Money.FromNpr(10m), Money.FromNpr(15m));
        await _batchRepository.AddAsync(batch);

        // Update quantity and record adjustment inside transaction
        batch.SetQuantity(60, DateTimeOffset.UtcNow);
        var adjustment = StockAdjustment.Create(
            batch.Id,
            med.Id,
            user.Id,
            StockAdjustmentType.PhysicalCountCorrection,
            50,
            60,
            "Valid adjustment");

        await _transactionRunner.ExecuteInTransactionAsync(async tx =>
        {
            await _batchRepository.UpdateAsync(batch, transaction: tx);
            await _adjustmentRepository.AddAsync(adjustment, transaction: tx);
        });

        // Verify both batch and adjustment are committed
        var reloadedBatch = await _batchRepository.GetByIdAsync(batch.Id);
        Assert.NotNull(reloadedBatch);
        Assert.Equal(60, reloadedBatch.QuantityOnHand);

        var adjustments = await _adjustmentRepository.GetByBatchIdAsync(batch.Id);
        Assert.Single(adjustments);
        Assert.Equal(60, adjustments[0].NewQuantity);
    }

    [Fact]
    public async Task PreservationOfExistingUserRecordsAndAuthenticationBehavior()
    {
        var hasher = new BCryptPasswordHasher();
        string rawPassword = "SecurePassword123!";
        string passwordHash = hasher.HashPassword(rawPassword);

        var user = User.Create("pharmacist_auth", "Auth Pharmacist", passwordHash, UserRole.Pharmacist, DateTimeOffset.UtcNow);
        await _userRepository.AddAsync(user);

        // Perform medicine and batch operations on the same database
        var med = Medicine.Create("Metformin 500mg", "Metformin", "Tablet", "Strip", 20);
        await _medicineRepository.AddAsync(med);

        var batch = Batch.Create(med.Id, "BTH-MET-1", 200, Money.FromNpr(5m), Money.FromNpr(8m));
        await _batchRepository.AddAsync(batch);

        // Verify user retrieval and authentication still work perfectly
        var loadedUser = await _userRepository.GetByUsernameAsync("pharmacist_auth");
        Assert.NotNull(loadedUser);
        Assert.Equal(user.Id, loadedUser.Id);
        Assert.True(hasher.VerifyPassword(rawPassword, loadedUser.PasswordHash));
        Assert.True(loadedUser.IsActive);
        Assert.Equal(UserRole.Pharmacist, loadedUser.Role);
    }
}
