using PharmacyApp.Application.Medicines;
using PharmacyApp.Application.Medicines.Commands;
using Xunit;

namespace PharmacyApp.Application.Tests;

public class MedicineValidatorTests
{
    private readonly MedicineValidator _validator = new();

    [Fact]
    public void ValidateCreate_ValidCommand_ReturnsSuccess()
    {
        var command = new CreateMedicineCommand(
            Name: "Paracetamol 500mg",
            GenericName: "Paracetamol",
            DosageForm: "Tablet",
            DispensingUnit: "Strip",
            ReorderLevel: 10,
            Brand: "Panadol",
            Strength: "500mg",
            Barcode: "8901234567890",
            Category: "Analgesic",
            RequiresPrescription: false);

        var result = _validator.ValidateCreate(command);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateCreate_EmptyName_ReturnsFailure(string? name)
    {
        var command = new CreateMedicineCommand(
            Name: name!,
            GenericName: "Paracetamol",
            DosageForm: "Tablet",
            DispensingUnit: "Strip",
            ReorderLevel: 10);

        var result = _validator.ValidateCreate(command);

        Assert.True(result.IsFailure);
        Assert.Contains("Medicine commercial name is required", result.Error);
    }

    [Fact]
    public void ValidateCreate_NameExceedingMaxLength_ReturnsFailure()
    {
        var longName = new string('A', 201);
        var command = new CreateMedicineCommand(
            Name: longName,
            GenericName: "Paracetamol",
            DosageForm: "Tablet",
            DispensingUnit: "Strip",
            ReorderLevel: 10);

        var result = _validator.ValidateCreate(command);

        Assert.True(result.IsFailure);
        Assert.Contains("cannot exceed 200 characters", result.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateCreate_EmptyGenericName_ReturnsFailure(string? genericName)
    {
        var command = new CreateMedicineCommand(
            Name: "Paracetamol 500mg",
            GenericName: genericName!,
            DosageForm: "Tablet",
            DispensingUnit: "Strip",
            ReorderLevel: 10);

        var result = _validator.ValidateCreate(command);

        Assert.True(result.IsFailure);
        Assert.Contains("Generic / active ingredient name is required", result.Error);
    }

    [Fact]
    public void ValidateCreate_GenericNameExceedingMaxLength_ReturnsFailure()
    {
        var longGeneric = new string('G', 201);
        var command = new CreateMedicineCommand(
            Name: "Paracetamol 500mg",
            GenericName: longGeneric,
            DosageForm: "Tablet",
            DispensingUnit: "Strip",
            ReorderLevel: 10);

        var result = _validator.ValidateCreate(command);

        Assert.True(result.IsFailure);
        Assert.Contains("cannot exceed 200 characters", result.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateCreate_EmptyDosageForm_ReturnsFailure(string? dosageForm)
    {
        var command = new CreateMedicineCommand(
            Name: "Paracetamol 500mg",
            GenericName: "Paracetamol",
            DosageForm: dosageForm!,
            DispensingUnit: "Strip",
            ReorderLevel: 10);

        var result = _validator.ValidateCreate(command);

        Assert.True(result.IsFailure);
        Assert.Contains("Dosage form is required", result.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateCreate_EmptyDispensingUnit_ReturnsFailure(string? dispensingUnit)
    {
        var command = new CreateMedicineCommand(
            Name: "Paracetamol 500mg",
            GenericName: "Paracetamol",
            DosageForm: "Tablet",
            DispensingUnit: dispensingUnit!,
            ReorderLevel: 10);

        var result = _validator.ValidateCreate(command);

        Assert.True(result.IsFailure);
        Assert.Contains("Dispensing unit is required", result.Error);
    }

    [Fact]
    public void ValidateCreate_NegativeReorderLevel_ReturnsFailure()
    {
        var command = new CreateMedicineCommand(
            Name: "Paracetamol 500mg",
            GenericName: "Paracetamol",
            DosageForm: "Tablet",
            DispensingUnit: "Strip",
            ReorderLevel: -1);

        var result = _validator.ValidateCreate(command);

        Assert.True(result.IsFailure);
        Assert.Contains("Reorder level cannot be negative", result.Error);
    }

    [Fact]
    public void ValidateCreate_BrandExceedingMaxLength_ReturnsFailure()
    {
        var command = new CreateMedicineCommand(
            Name: "Paracetamol 500mg",
            GenericName: "Paracetamol",
            DosageForm: "Tablet",
            DispensingUnit: "Strip",
            ReorderLevel: 5,
            Brand: new string('B', 151));

        var result = _validator.ValidateCreate(command);

        Assert.True(result.IsFailure);
        Assert.Contains("Brand cannot exceed 150 characters", result.Error);
    }

    [Fact]
    public void ValidateUpdate_EmptyId_ReturnsFailure()
    {
        var command = new UpdateMedicineCommand(
            Id: Guid.Empty,
            Name: "Paracetamol 500mg",
            GenericName: "Paracetamol",
            DosageForm: "Tablet",
            DispensingUnit: "Strip",
            ReorderLevel: 10,
            ExpectedVersion: 1);

        var result = _validator.ValidateUpdate(command);

        Assert.True(result.IsFailure);
        Assert.Contains("Medicine ID cannot be empty", result.Error);
    }

    [Fact]
    public void ValidateUpdate_ZeroExpectedVersion_ReturnsFailure()
    {
        var command = new UpdateMedicineCommand(
            Id: Guid.NewGuid(),
            Name: "Paracetamol 500mg",
            GenericName: "Paracetamol",
            DosageForm: "Tablet",
            DispensingUnit: "Strip",
            ReorderLevel: 10,
            ExpectedVersion: 0);

        var result = _validator.ValidateUpdate(command);

        Assert.True(result.IsFailure);
        Assert.Contains("Expected version must be greater than zero", result.Error);
    }

    [Fact]
    public void ValidateUpdate_ValidCommand_ReturnsSuccess()
    {
        var command = new UpdateMedicineCommand(
            Id: Guid.NewGuid(),
            Name: "Amoxicillin 500mg",
            GenericName: "Amoxicillin",
            DosageForm: "Capsule",
            DispensingUnit: "Box",
            ReorderLevel: 25,
            ExpectedVersion: 2,
            Brand: "Moxatag",
            Strength: "500mg",
            Barcode: "1234567890123",
            Category: "Antibiotic",
            RequiresPrescription: true,
            IsActive: true);

        var result = _validator.ValidateUpdate(command);

        Assert.True(result.IsSuccess);
    }
}
