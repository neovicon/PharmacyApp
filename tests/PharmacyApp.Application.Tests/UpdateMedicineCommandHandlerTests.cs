using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Medicines;
using PharmacyApp.Application.Medicines.Commands;
using PharmacyApp.Application.Tests.TestDoubles;
using PharmacyApp.Domain.Entities;
using Xunit;

namespace PharmacyApp.Application.Tests;

public class UpdateMedicineCommandHandlerTests
{
    private readonly TestMedicineRepository _repository = new();
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));
    private readonly MedicineValidator _validator = new();
    private readonly UpdateMedicineCommandHandler _handler;

    public UpdateMedicineCommandHandlerTests()
    {
        _handler = new UpdateMedicineCommandHandler(_repository, _dateTimeProvider, _validator);
    }

    private async Task<Medicine> CreateSampleMedicineAsync(string name = "Ibuprofen 400mg", string? barcode = "111222333444")
    {
        var medicine = Medicine.Create(
            name: name,
            genericName: "Ibuprofen",
            dosageForm: "Tablet",
            dispensingUnit: "Strip",
            reorderLevel: 15,
            brand: "Advil",
            strength: "400mg",
            barcode: barcode,
            category: "NSAID",
            requiresPrescription: false,
            now: _dateTimeProvider.UtcNow);

        await _repository.AddAsync(medicine);
        return medicine;
    }

    [Fact]
    public async Task HandleAsync_WithValidDetailsAndMatchingVersion_UpdatesSuccessfully()
    {
        var existing = await CreateSampleMedicineAsync();

        _dateTimeProvider.UtcNow = _dateTimeProvider.UtcNow.AddMinutes(30);

        var command = new UpdateMedicineCommand(
            Id: existing.Id,
            Name: "Ibuprofen Forte 400mg",
            GenericName: "Ibuprofen Acid",
            DosageForm: "Coated Tablet",
            DispensingUnit: "Box",
            ReorderLevel: 25,
            ExpectedVersion: 1, // Matches current Version
            Brand: "Brufen",
            Strength: "400mg",
            Barcode: "999888777666",
            Category: "Anti-inflammatory",
            RequiresPrescription: true,
            IsActive: true);

        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsSuccess);

        var updated = await _repository.GetByIdAsync(existing.Id);
        Assert.NotNull(updated);
        Assert.Equal("Ibuprofen Forte 400mg", updated.Name);
        Assert.Equal("Ibuprofen Acid", updated.GenericName);
        Assert.Equal("Coated Tablet", updated.DosageForm);
        Assert.Equal("Box", updated.DispensingUnit);
        Assert.Equal(25, updated.ReorderLevel);
        Assert.Equal("Brufen", updated.Brand);
        Assert.Equal("999888777666", updated.Barcode);
        Assert.Equal("Anti-inflammatory", updated.Category);
        Assert.True(updated.RequiresPrescription);
        Assert.True(updated.IsActive);
        Assert.True(updated.Version > 1);
        Assert.Equal(_dateTimeProvider.UtcNow, updated.UpdatedAt);
    }

    [Fact]
    public async Task HandleAsync_WithStaleVersion_ReturnsConcurrencyFailure()
    {
        var existing = await CreateSampleMedicineAsync();

        var staleCommand = new UpdateMedicineCommand(
            Id: existing.Id,
            Name: "New Name",
            GenericName: "New Generic",
            DosageForm: "Tablet",
            DispensingUnit: "Strip",
            ReorderLevel: 10,
            ExpectedVersion: 99, // Stale / mismatching version!
            IsActive: true);

        var result = await _handler.HandleAsync(staleCommand);

        Assert.True(result.IsFailure);
        Assert.Contains("Optimistic concurrency violation", result.Error);
    }

    [Fact]
    public async Task HandleAsync_WhenMedicineNotFound_ReturnsFailure()
    {
        var nonExistentId = Guid.NewGuid();
        var command = new UpdateMedicineCommand(
            Id: nonExistentId,
            Name: "Non-existent",
            GenericName: "Generic",
            DosageForm: "Tablet",
            DispensingUnit: "Strip",
            ReorderLevel: 10,
            ExpectedVersion: 1);

        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Contains($"Medicine with ID '{nonExistentId}' was not found", result.Error);
    }

    [Fact]
    public async Task HandleAsync_WhenBarcodeCollidesWithAnotherMedicine_ReturnsFailure()
    {
        var med1 = await CreateSampleMedicineAsync("Medicine 1", "BARCODE-AAA");
        var med2 = await CreateSampleMedicineAsync("Medicine 2", "BARCODE-BBB");

        var command = new UpdateMedicineCommand(
            Id: med2.Id,
            Name: "Medicine 2 Updated",
            GenericName: "Generic",
            DosageForm: "Tablet",
            DispensingUnit: "Strip",
            ReorderLevel: 10,
            ExpectedVersion: 1,
            Barcode: "barcode-aaa"); // conflicts with med1!

        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Contains("already exists", result.Error);
    }

    [Fact]
    public async Task HandleAsync_KeepingSameBarcodeOnSameMedicine_Succeeds()
    {
        var med = await CreateSampleMedicineAsync("Medicine 1", "SAME-BARCODE");

        var command = new UpdateMedicineCommand(
            Id: med.Id,
            Name: "Medicine 1 Updated",
            GenericName: "Generic",
            DosageForm: "Tablet",
            DispensingUnit: "Strip",
            ReorderLevel: 10,
            ExpectedVersion: 1,
            Barcode: "same-barcode"); // self-barcode match

        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task HandleAsync_CanDeactivateAndReactivateMedicine()
    {
        var med = await CreateSampleMedicineAsync();
        Assert.True(med.IsActive);

        // Deactivate
        var deactivateCommand = new UpdateMedicineCommand(
            Id: med.Id,
            Name: med.Name,
            GenericName: med.GenericName,
            DosageForm: med.DosageForm,
            DispensingUnit: med.DispensingUnit,
            ReorderLevel: med.ReorderLevel,
            ExpectedVersion: med.Version,
            IsActive: false);

        var result = await _handler.HandleAsync(deactivateCommand);
        Assert.True(result.IsSuccess);

        var deactivated = await _repository.GetByIdAsync(med.Id);
        Assert.NotNull(deactivated);
        Assert.False(deactivated.IsActive);

        // Reactivate
        var reactivateCommand = new UpdateMedicineCommand(
            Id: med.Id,
            Name: med.Name,
            GenericName: med.GenericName,
            DosageForm: med.DosageForm,
            DispensingUnit: med.DispensingUnit,
            ReorderLevel: med.ReorderLevel,
            ExpectedVersion: deactivated.Version,
            IsActive: true);

        var reactivateResult = await _handler.HandleAsync(reactivateCommand);
        Assert.True(reactivateResult.IsSuccess);

        var reactivated = await _repository.GetByIdAsync(med.Id);
        Assert.NotNull(reactivated);
        Assert.True(reactivated.IsActive);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidInput_ReturnsValidationFailure()
    {
        var med = await CreateSampleMedicineAsync();

        var invalidCommand = new UpdateMedicineCommand(
            Id: med.Id,
            Name: "", // Empty name!
            GenericName: "Generic",
            DosageForm: "Tablet",
            DispensingUnit: "Strip",
            ReorderLevel: -5,
            ExpectedVersion: 1);

        var result = await _handler.HandleAsync(invalidCommand);

        Assert.True(result.IsFailure);
        Assert.Contains("commercial name is required", result.Error);
    }
}
