using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Medicines;
using PharmacyApp.Application.Medicines.Commands;
using PharmacyApp.Application.Tests.TestDoubles;
using Xunit;

namespace PharmacyApp.Application.Tests;

public class CreateMedicineCommandHandlerTests
{
    private readonly TestMedicineRepository _repository = new();
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));
    private readonly MedicineValidator _validator = new();
    private readonly CreateMedicineCommandHandler _handler;

    public CreateMedicineCommandHandlerTests()
    {
        _handler = new CreateMedicineCommandHandler(_repository, _dateTimeProvider, _validator);
    }

    [Fact]
    public async Task HandleAsync_WithValidCommand_CreatesMedicineAndReturnsId()
    {
        var command = new CreateMedicineCommand(
            Name: "Amoxicillin 500mg",
            GenericName: "Amoxicillin",
            DosageForm: "Capsule",
            DispensingUnit: "Box",
            ReorderLevel: 20,
            Brand: "Amoxil",
            Strength: "500mg",
            Barcode: "9876543210987",
            Category: "Antibiotic",
            RequiresPrescription: true);

        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value);

        var created = await _repository.GetByIdAsync(result.Value);
        Assert.NotNull(created);
        Assert.Equal("Amoxicillin 500mg", created.Name);
        Assert.Equal("Amoxicillin", created.GenericName);
        Assert.Equal("Capsule", created.DosageForm);
        Assert.Equal("Box", created.DispensingUnit);
        Assert.Equal(20, created.ReorderLevel);
        Assert.Equal("Amoxil", created.Brand);
        Assert.Equal("500mg", created.Strength);
        Assert.Equal("9876543210987", created.Barcode);
        Assert.Equal("Antibiotic", created.Category);
        Assert.True(created.RequiresPrescription);
        Assert.True(created.IsActive);
        Assert.Equal(1u, created.Version);
        Assert.Equal(_dateTimeProvider.UtcNow, created.CreatedAt);
        Assert.Equal(_dateTimeProvider.UtcNow, created.UpdatedAt);
    }

    [Fact]
    public async Task HandleAsync_WithMinimalRequiredFields_CreatesSuccessfully()
    {
        var command = new CreateMedicineCommand(
            Name: "Cetirizine 10mg",
            GenericName: "Cetirizine",
            DosageForm: "Tablet",
            DispensingUnit: "Strip",
            ReorderLevel: 0);

        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
        var created = await _repository.GetByIdAsync(result.Value);
        Assert.NotNull(created);
        Assert.Null(created.Brand);
        Assert.Null(created.Strength);
        Assert.Null(created.Barcode);
        Assert.Null(created.Category);
        Assert.False(created.RequiresPrescription);
        Assert.True(created.IsActive);
        Assert.Equal(0, created.ReorderLevel);
    }

    [Theory]
    [InlineData("", "Paracetamol", "Tablet", "Strip", 10, "commercial name is required")]
    [InlineData("Paracetamol", "", "Tablet", "Strip", 10, "Generic / active ingredient name is required")]
    [InlineData("Paracetamol", "Paracetamol", "", "Strip", 10, "Dosage form is required")]
    [InlineData("Paracetamol", "Paracetamol", "Tablet", "", 10, "Dispensing unit is required")]
    [InlineData("Paracetamol", "Paracetamol", "Tablet", "Strip", -5, "Reorder level cannot be negative")]
    public async Task HandleAsync_WithInvalidFields_ReturnsValidationFailure(
        string name,
        string genericName,
        string dosageForm,
        string dispensingUnit,
        int reorderLevel,
        string expectedError)
    {
        var command = new CreateMedicineCommand(
            Name: name,
            GenericName: genericName,
            DosageForm: dosageForm,
            DispensingUnit: dispensingUnit,
            ReorderLevel: reorderLevel);

        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Contains(expectedError, result.Error);
    }

    [Fact]
    public async Task HandleAsync_WhenBarcodeAlreadyExists_ReturnsFailureWithoutModifyingState()
    {
        var firstCommand = new CreateMedicineCommand(
            Name: "Medicine A",
            GenericName: "Generic A",
            DosageForm: "Tablet",
            DispensingUnit: "Strip",
            ReorderLevel: 5,
            Barcode: "DUPLICATE-123");

        var firstResult = await _handler.HandleAsync(firstCommand);
        Assert.True(firstResult.IsSuccess);

        var duplicateCommand = new CreateMedicineCommand(
            Name: "Medicine B",
            GenericName: "Generic B",
            DosageForm: "Syrup",
            DispensingUnit: "Bottle",
            ReorderLevel: 10,
            Barcode: "duplicate-123"); // case-insensitive duplicate check

        var duplicateResult = await _handler.HandleAsync(duplicateCommand);

        Assert.True(duplicateResult.IsFailure);
        Assert.Contains("already exists", duplicateResult.Error);
        Assert.Single(_repository.Medicines);
    }
}
