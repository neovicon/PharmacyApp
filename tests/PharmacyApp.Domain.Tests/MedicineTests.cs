using PharmacyApp.Domain.Entities;
using Xunit;

namespace PharmacyApp.Domain.Tests;

public class MedicineTests
{
    private readonly DateTimeOffset _fixedTime = new(2026, 10, 19, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidParameters_InitializesCorrectly()
    {
        var medicine = Medicine.Create(
            name: "Amoxil",
            genericName: "Amoxicillin",
            dosageForm: "Capsule",
            dispensingUnit: "Capsule",
            reorderLevel: 50,
            brand: "GSK",
            strength: "500 mg",
            barcode: " 8901234567890 ",
            category: "Antibiotics",
            requiresPrescription: true,
            now: _fixedTime);

        Assert.NotEqual(Guid.Empty, medicine.Id);
        Assert.Equal("Amoxil", medicine.Name);
        Assert.Equal("Amoxicillin", medicine.GenericName);
        Assert.Equal("Capsule", medicine.DosageForm);
        Assert.Equal("Capsule", medicine.DispensingUnit);
        Assert.Equal(50, medicine.ReorderLevel);
        Assert.Equal("GSK", medicine.Brand);
        Assert.Equal("500 mg", medicine.Strength);
        Assert.Equal("8901234567890", medicine.Barcode); // Trimmed
        Assert.Equal("Antibiotics", medicine.Category);
        Assert.True(medicine.RequiresPrescription);
        Assert.True(medicine.IsActive);
        Assert.Equal(_fixedTime, medicine.CreatedAt);
        Assert.Equal(_fixedTime, medicine.UpdatedAt);
        Assert.Equal(1u, medicine.Version);
    }

    [Fact]
    public void Create_WithZeroReorderLevel_Succeeds()
    {
        var medicine = Medicine.Create(
            name: "Aspirin",
            genericName: "Acetylsalicylic Acid",
            dosageForm: "Tablet",
            dispensingUnit: "Tablet",
            reorderLevel: 0,
            now: _fixedTime);

        Assert.Equal(0, medicine.ReorderLevel);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrWhitespaceBarcode_NormalizesToNull(string? rawBarcode)
    {
        var medicine = Medicine.Create(
            name: "Cetirizine",
            genericName: "Cetirizine Hydrochloride",
            dosageForm: "Tablet",
            dispensingUnit: "Tablet",
            reorderLevel: 20,
            barcode: rawBarcode,
            now: _fixedTime);

        Assert.Null(medicine.Barcode);
    }

    [Theory]
    [InlineData("", "Amoxicillin", "Capsule", "Capsule")]
    [InlineData("   ", "Amoxicillin", "Capsule", "Capsule")]
    [InlineData(null, "Amoxicillin", "Capsule", "Capsule")]
    [InlineData("Amoxil", "", "Capsule", "Capsule")]
    [InlineData("Amoxil", "   ", "Capsule", "Capsule")]
    [InlineData("Amoxil", null, "Capsule", "Capsule")]
    [InlineData("Amoxil", "Amoxicillin", "", "Capsule")]
    [InlineData("Amoxil", "Amoxicillin", "   ", "Capsule")]
    [InlineData("Amoxil", "Amoxicillin", null, "Capsule")]
    [InlineData("Amoxil", "Amoxicillin", "Capsule", "")]
    [InlineData("Amoxil", "Amoxicillin", "Capsule", "   ")]
    [InlineData("Amoxil", "Amoxicillin", "Capsule", null)]
    public void Create_WithMissingCoreAttributes_ThrowsArgumentException(
        string? name,
        string? genericName,
        string? form,
        string? unit)
    {
        Assert.Throws<ArgumentException>(() =>
            Medicine.Create(name!, genericName!, form!, unit!, reorderLevel: 10, now: _fixedTime));
    }

    [Fact]
    public void Create_WithNegativeReorderLevel_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Medicine.Create(
                name: "Amoxil",
                genericName: "Amoxicillin",
                dosageForm: "Capsule",
                dispensingUnit: "Capsule",
                reorderLevel: -1,
                now: _fixedTime));
    }

    [Fact]
    public void UpdateDetails_UpdatesFields_AndIncrementsVersion()
    {
        var medicine = Medicine.Create(
            name: "OldName",
            genericName: "OldGeneric",
            dosageForm: "Tablet",
            dispensingUnit: "Tablet",
            reorderLevel: 10,
            now: _fixedTime);

        var updateTime = _fixedTime.AddHours(3);

        medicine.UpdateDetails(
            name: "NewName",
            genericName: "NewGeneric",
            dosageForm: "Syrup",
            dispensingUnit: "Bottle",
            reorderLevel: 25,
            brand: "NewBrand",
            strength: "100 mL",
            barcode: " 112233 ",
            category: "Pediatric",
            requiresPrescription: true,
            now: updateTime);

        Assert.Equal("NewName", medicine.Name);
        Assert.Equal("NewGeneric", medicine.GenericName);
        Assert.Equal("Syrup", medicine.DosageForm);
        Assert.Equal("Bottle", medicine.DispensingUnit);
        Assert.Equal(25, medicine.ReorderLevel);
        Assert.Equal("NewBrand", medicine.Brand);
        Assert.Equal("100 mL", medicine.Strength);
        Assert.Equal("112233", medicine.Barcode);
        Assert.Equal("Pediatric", medicine.Category);
        Assert.True(medicine.RequiresPrescription);
        Assert.Equal(updateTime, medicine.UpdatedAt);
        Assert.Equal(2u, medicine.Version);
    }

    [Fact]
    public void Deactivate_And_Reactivate_UpdatesStatusAndVersion()
    {
        var medicine = Medicine.Create(
            name: "Paracetamol",
            genericName: "Paracetamol",
            dosageForm: "Tablet",
            dispensingUnit: "Tablet",
            reorderLevel: 100,
            now: _fixedTime);

        var deactivateTime = _fixedTime.AddDays(1);
        medicine.Deactivate(deactivateTime);

        Assert.False(medicine.IsActive);
        Assert.Equal(deactivateTime, medicine.UpdatedAt);
        Assert.Equal(2u, medicine.Version);

        var reactivateTime = _fixedTime.AddDays(2);
        medicine.Reactivate(reactivateTime);

        Assert.True(medicine.IsActive);
        Assert.Equal(reactivateTime, medicine.UpdatedAt);
        Assert.Equal(3u, medicine.Version);
    }
}
