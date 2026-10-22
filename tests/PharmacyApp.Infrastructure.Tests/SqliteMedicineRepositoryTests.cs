using Microsoft.Data.Sqlite;
using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Exceptions;
using PharmacyApp.Domain.Entities;
using PharmacyApp.Infrastructure.Persistence;
using Xunit;

namespace PharmacyApp.Infrastructure.Tests;

public class SqliteMedicineRepositoryTests : IDisposable
{
    private readonly IsolatedTestPathProvider _pathProvider;
    private readonly string _connectionString;
    private readonly SqliteDatabaseInitializer _initializer;
    private readonly SqliteMedicineRepository _repository;

    public SqliteMedicineRepositoryTests()
    {
        _pathProvider = new IsolatedTestPathProvider();
        _connectionString = $"Data Source={Path.Combine(_pathProvider.DataDirectory, "pharmacy.db")};Cache=Shared;Foreign Keys=True;";
        _initializer = new SqliteDatabaseInitializer(_connectionString);
        _initializer.InitializeAsync().GetAwaiter().GetResult();
        _repository = new SqliteMedicineRepository(_connectionString);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        _pathProvider.Dispose();
    }

    [Fact]
    public async Task AddAndGetByIdAsync_PersistsAndReconstitutesAllFields()
    {
        var timestamp = new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);
        var medicine = Medicine.Create(
            name: "Amoxicillin 500mg Capsule",
            genericName: "Amoxicillin Trihydrate",
            dosageForm: "Capsule",
            dispensingUnit: "Cap",
            reorderLevel: 20,
            brand: "Moxilin",
            strength: "500mg",
            barcode: "8901234567890",
            category: "Antibiotic",
            requiresPrescription: true,
            now: timestamp);

        await _repository.AddAsync(medicine);

        var retrieved = await _repository.GetByIdAsync(medicine.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(medicine.Id, retrieved.Id);
        Assert.Equal("Amoxicillin 500mg Capsule", retrieved.Name);
        Assert.Equal("Amoxicillin Trihydrate", retrieved.GenericName);
        Assert.Equal("Capsule", retrieved.DosageForm);
        Assert.Equal("Cap", retrieved.DispensingUnit);
        Assert.Equal(20, retrieved.ReorderLevel);
        Assert.Equal("Moxilin", retrieved.Brand);
        Assert.Equal("500mg", retrieved.Strength);
        Assert.Equal("8901234567890", retrieved.Barcode);
        Assert.Equal("Antibiotic", retrieved.Category);
        Assert.True(retrieved.RequiresPrescription);
        Assert.True(retrieved.IsActive);
        Assert.Equal(timestamp, retrieved.CreatedAt);
        Assert.Equal(timestamp, retrieved.UpdatedAt);
        Assert.Equal(1u, retrieved.Version);
    }

    [Fact]
    public async Task OptionalFields_CanBeNull()
    {
        var medicine = Medicine.Create(
            name: "Paracetamol 500mg",
            genericName: "Paracetamol",
            dosageForm: "Tablet",
            dispensingUnit: "Tab",
            reorderLevel: 50,
            brand: null,
            strength: null,
            barcode: null,
            category: null,
            requiresPrescription: false);

        await _repository.AddAsync(medicine);

        var retrieved = await _repository.GetByIdAsync(medicine.Id);

        Assert.NotNull(retrieved);
        Assert.Null(retrieved.Brand);
        Assert.Null(retrieved.Strength);
        Assert.Null(retrieved.Barcode);
        Assert.Null(retrieved.Category);
        Assert.False(retrieved.RequiresPrescription);
        Assert.True(retrieved.IsActive);
    }

    [Theory]
    [InlineData("8901112223334", "8901112223334")]
    [InlineData("BAR-12345", "bar-12345")]
    [InlineData("BAR-12345", "  bar-12345  ")]
    public async Task GetByBarcodeAsync_FindsMedicineCaseInsensitive(string storedBarcode, string lookupBarcode)
    {
        var medicine = Medicine.Create("Med", "Gen", "Tab", "Tab", 10, barcode: storedBarcode);
        await _repository.AddAsync(medicine);

        var retrieved = await _repository.GetByBarcodeAsync(lookupBarcode);

        Assert.NotNull(retrieved);
        Assert.Equal(medicine.Id, retrieved.Id);
    }

    [Fact]
    public async Task GetByBarcodeAsync_WithNullOrEmpty_ReturnsNull()
    {
        Assert.Null(await _repository.GetByBarcodeAsync(""));
        Assert.Null(await _repository.GetByBarcodeAsync("   "));
    }

    [Fact]
    public async Task ListAsync_FiltersByActiveState_AndOrdersByName()
    {
        var medB = Medicine.Create("Brufen 400", "Ibuprofen", "Tablet", "Tab", 10);
        var medA = Medicine.Create("Amoxicillin", "Amoxicillin", "Capsule", "Cap", 10);
        var medC = Medicine.Create("Cetirizine", "Cetirizine", "Tablet", "Tab", 10);
        medC.Deactivate(DateTimeOffset.UtcNow);

        await _repository.AddAsync(medB);
        await _repository.AddAsync(medA);
        await _repository.AddAsync(medC);

        // List active only
        var active = await _repository.ListAsync(isActive: true);
        Assert.Equal(2, active.Count);
        Assert.Equal("Amoxicillin", active[0].Name);
        Assert.Equal("Brufen 400", active[1].Name);

        // List inactive only
        var inactive = await _repository.ListAsync(isActive: false);
        Assert.Single(inactive);
        Assert.Equal("Cetirizine", inactive[0].Name);

        // List all
        var all = await _repository.ListAsync(isActive: null);
        Assert.Equal(3, all.Count);
        Assert.Equal("Amoxicillin", all[0].Name);
        Assert.Equal("Brufen 400", all[1].Name);
        Assert.Equal("Cetirizine", all[2].Name);
    }

    [Fact]
    public async Task SearchAsync_FindsMatchesAcrossNameGenericBrandCategoryAndBarcode()
    {
        var med1 = Medicine.Create("Panadol", "Paracetamol", "Tablet", "Tab", 10, brand: "GSK", category: "Analgesic", barcode: "111111");
        var med2 = Medicine.Create("Cipro", "Ciprofloxacin", "Tablet", "Tab", 10, brand: "Bayer", category: "Antibiotic", barcode: "222222");
        var med3 = Medicine.Create("Napa", "Paracetamol", "Syrup", "Bottle", 5, brand: "Beximco", category: "Analgesic", barcode: "333333");

        await _repository.AddAsync(med1);
        await _repository.AddAsync(med2);
        await _repository.AddAsync(med3);

        // Search by GenericName
        var byGeneric = await _repository.SearchAsync("paracetamol");
        Assert.Equal(2, byGeneric.Count);

        // Search by Brand
        var byBrand = await _repository.SearchAsync("Bayer");
        Assert.Single(byBrand);
        Assert.Equal("Cipro", byBrand[0].Name);

        // Search by Category
        var byCategory = await _repository.SearchAsync("antibiotic");
        Assert.Single(byCategory);
        Assert.Equal("Cipro", byCategory[0].Name);

        // Search by exact Barcode
        var byBarcode = await _repository.SearchAsync("111111");
        Assert.Single(byBarcode);
        Assert.Equal("Panadol", byBarcode[0].Name);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesFieldsAndIncrementsVersion()
    {
        var now = DateTimeOffset.UtcNow;
        var med = Medicine.Create("Old Name", "Old Generic", "Tab", "Tab", 10, now: now);
        await _repository.AddAsync(med);

        var updateTime = now.AddHours(2);
        med.UpdateDetails("New Name", "New Generic", "Cap", "Cap", 25, "New Brand", "500mg", "999888", "New Cat", true, updateTime);

        await _repository.UpdateAsync(med);

        var retrieved = await _repository.GetByIdAsync(med.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("New Name", retrieved.Name);
        Assert.Equal("New Generic", retrieved.GenericName);
        Assert.Equal("Cap", retrieved.DosageForm);
        Assert.Equal("Cap", retrieved.DispensingUnit);
        Assert.Equal(25, retrieved.ReorderLevel);
        Assert.Equal("New Brand", retrieved.Brand);
        Assert.Equal("500mg", retrieved.Strength);
        Assert.Equal("999888", retrieved.Barcode);
        Assert.Equal("New Cat", retrieved.Category);
        Assert.True(retrieved.RequiresPrescription);
        Assert.Equal(updateTime, retrieved.UpdatedAt);
        Assert.Equal(2u, retrieved.Version);
    }

    [Fact]
    public async Task UpdateAsync_WhenMedicineDoesNotExist_ThrowsEntityNotFoundException()
    {
        var nonExistent = Medicine.Create("Ghost", "Ghost", "Tab", "Tab", 5);

        await Assert.ThrowsAsync<EntityNotFoundException>(() => _repository.UpdateAsync(nonExistent));
    }

    [Fact]
    public async Task AddAsync_DuplicateBarcode_ThrowsDuplicateBarcodeException_AndDoesNotOverwrite()
    {
        var med1 = Medicine.Create("Med One", "Gen One", "Tab", "Tab", 10, barcode: "SAME_BARCODE");
        await _repository.AddAsync(med1);

        var med2 = Medicine.Create("Med Two", "Gen Two", "Cap", "Cap", 20, barcode: "SAME_BARCODE");

        var ex = await Assert.ThrowsAsync<DuplicateBarcodeException>(() => _repository.AddAsync(med2));
        Assert.Equal("SAME_BARCODE", ex.Barcode);

        // Verify Med One was not overwritten or corrupted
        var retrieved1 = await _repository.GetByIdAsync(med1.Id);
        Assert.NotNull(retrieved1);
        Assert.Equal("Med One", retrieved1.Name);

        var retrieved2 = await _repository.GetByIdAsync(med2.Id);
        Assert.Null(retrieved2);
    }

    [Fact]
    public async Task UpdateAsync_ConflictWithExistingBarcode_ThrowsDuplicateBarcodeException()
    {
        var med1 = Medicine.Create("Med One", "Gen One", "Tab", "Tab", 10, barcode: "BARCODE_A");
        var med2 = Medicine.Create("Med Two", "Gen Two", "Tab", "Tab", 10, barcode: "BARCODE_B");
        await _repository.AddAsync(med1);
        await _repository.AddAsync(med2);

        // Attempt to update med2 to use med1's barcode
        med2.UpdateDetails("Med Two", "Gen Two", "Tab", "Tab", 10, null, null, "BARCODE_A", null, false, DateTimeOffset.UtcNow);

        var ex = await Assert.ThrowsAsync<DuplicateBarcodeException>(() => _repository.UpdateAsync(med2));
        Assert.Equal("BARCODE_A", ex.Barcode);
    }

    [Fact]
    public async Task MultipleMedicines_WithNullBarcode_AreAllowed()
    {
        var med1 = Medicine.Create("Med One", "Gen One", "Tab", "Tab", 10, barcode: null);
        var med2 = Medicine.Create("Med Two", "Gen Two", "Tab", "Tab", 10, barcode: null);

        await _repository.AddAsync(med1);
        await _repository.AddAsync(med2);

        Assert.NotNull(await _repository.GetByIdAsync(med1.Id));
        Assert.NotNull(await _repository.GetByIdAsync(med2.Id));
    }

    [Fact]
    public async Task ExistsBarcodeAsync_CorrectlyReportsExistenceAndExclusion()
    {
        var med = Medicine.Create("Med", "Gen", "Tab", "Tab", 10, barcode: "EXISTING_BARCODE");
        await _repository.AddAsync(med);

        Assert.True(await _repository.ExistsBarcodeAsync("EXISTING_BARCODE"));
        Assert.True(await _repository.ExistsBarcodeAsync("existing_barcode"));
        Assert.False(await _repository.ExistsBarcodeAsync("NON_EXISTENT"));
        Assert.False(await _repository.ExistsBarcodeAsync(null!));

        // When excluding med.Id, should report false
        Assert.False(await _repository.ExistsBarcodeAsync("EXISTING_BARCODE", excludeMedicineId: med.Id));
    }

    [Fact]
    public async Task UpdateAsync_WithConflictingVersion_ThrowsConcurrencyException()
    {
        var med = Medicine.Create("Med", "Gen", "Tab", "Tab", 10);
        await _repository.AddAsync(med);

        // Load entity to simulate thread 1
        var medThread1 = await _repository.GetByIdAsync(med.Id);
        Assert.NotNull(medThread1);

        // Simulate thread 2 updating the entity first
        var medThread2 = await _repository.GetByIdAsync(med.Id);
        Assert.NotNull(medThread2);
        medThread2.UpdateDetails("Updated By Thread 2", "Gen", "Tab", "Tab", 15, null, null, null, null, false, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(medThread2);

        // Verify version in DB is now 2
        var inDb = await _repository.GetByIdAsync(med.Id);
        Assert.NotNull(inDb);
        Assert.Equal(2u, inDb.Version);

        // Now thread 1 attempts to update with stale version 1
        medThread1.UpdateDetails("Updated By Thread 1", "Gen", "Tab", "Tab", 20, null, null, null, null, false, DateTimeOffset.UtcNow);
        var ex = await Assert.ThrowsAsync<ConcurrencyException>(() => _repository.UpdateAsync(medThread1, expectedVersion: 1));
        Assert.Contains("Optimistic concurrency violation", ex.Message);

        // Verify thread 2's data was not overwritten
        var finalDb = await _repository.GetByIdAsync(med.Id);
        Assert.NotNull(finalDb);
        Assert.Equal("Updated By Thread 2", finalDb.Name);
        Assert.Equal(2u, finalDb.Version);
    }

    [Fact]
    public async Task SearchAsync_TreatsWildcardsAndEscapeCharactersLiterally()
    {
        var m1 = Medicine.Create("Alcohol 70% Solution", "Ethanol", "Liquid", "Bottle", 5);
        var m2 = Medicine.Create("Alcohol 700ml Bottle", "Ethanol", "Liquid", "Bottle", 5);
        var m3 = Medicine.Create("Amox_500", "Amoxicillin", "Capsule", "Cap", 5);
        var m4 = Medicine.Create("Amox-500", "Amoxicillin", "Capsule", "Cap", 5);
        var m5 = Medicine.Create(@"Special\Formula", "Custom", "Liquid", "Bottle", 5);

        await _repository.AddAsync(m1);
        await _repository.AddAsync(m2);
        await _repository.AddAsync(m3);
        await _repository.AddAsync(m4);
        await _repository.AddAsync(m5);

        // Search for literal '%'
        var percentResults = await _repository.SearchAsync("70%");
        Assert.Single(percentResults);
        Assert.Equal("Alcohol 70% Solution", percentResults[0].Name);

        // Search for literal '_'
        var underscoreResults = await _repository.SearchAsync("Amox_500");
        Assert.Single(underscoreResults);
        Assert.Equal("Amox_500", underscoreResults[0].Name);

        // Search for literal '\'
        var backslashResults = await _repository.SearchAsync(@"Special\Formula");
        Assert.Single(backslashResults);
        Assert.Equal(@"Special\Formula", backslashResults[0].Name);
    }
}
