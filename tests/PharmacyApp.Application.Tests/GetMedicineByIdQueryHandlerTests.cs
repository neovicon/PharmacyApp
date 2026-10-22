using PharmacyApp.Application.Medicines.Queries;
using PharmacyApp.Application.Tests.TestDoubles;
using PharmacyApp.Domain.Entities;
using Xunit;

namespace PharmacyApp.Application.Tests;

public class GetMedicineByIdQueryHandlerTests
{
    private readonly TestMedicineRepository _repository = new();
    private readonly GetMedicineByIdQueryHandler _idHandler;
    private readonly GetMedicineByBarcodeQueryHandler _barcodeHandler;

    public GetMedicineByIdQueryHandlerTests()
    {
        _idHandler = new GetMedicineByIdQueryHandler(_repository);
        _barcodeHandler = new GetMedicineByBarcodeQueryHandler(_repository);
    }

    private async Task<Medicine> SeedMedicineAsync(string name = "Metformin 500mg", string barcode = "123456789")
    {
        var medicine = Medicine.Create(
            name: name,
            genericName: "Metformin",
            dosageForm: "Tablet",
            dispensingUnit: "Strip",
            reorderLevel: 50,
            barcode: barcode);

        await _repository.AddAsync(medicine);
        return medicine;
    }

    [Fact]
    public async Task HandleAsync_WithExistingId_ReturnsMedicineDto()
    {
        var seeded = await SeedMedicineAsync();

        var query = new GetMedicineByIdQuery(seeded.Id);
        var result = await _idHandler.HandleAsync(query);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(seeded.Id, result.Value.Id);
        Assert.Equal("Metformin 500mg", result.Value.Name);
        Assert.Equal("Metformin", result.Value.GenericName);
        Assert.Equal("123456789", result.Value.Barcode);
    }

    [Fact]
    public async Task HandleAsync_WithEmptyId_ReturnsFailure()
    {
        var query = new GetMedicineByIdQuery(Guid.Empty);
        var result = await _idHandler.HandleAsync(query);

        Assert.True(result.IsFailure);
        Assert.Contains("Medicine ID cannot be empty", result.Error);
    }

    [Fact]
    public async Task HandleAsync_WithNonExistentId_ReturnsFailure()
    {
        var nonExistentId = Guid.NewGuid();
        var query = new GetMedicineByIdQuery(nonExistentId);
        var result = await _idHandler.HandleAsync(query);

        Assert.True(result.IsFailure);
        Assert.Contains($"Medicine with ID '{nonExistentId}' was not found", result.Error);
    }

    [Fact]
    public async Task HandleAsync_WithExistingBarcode_ReturnsMedicineDto()
    {
        var seeded = await SeedMedicineAsync(barcode: "BAR-100200");

        var query = new GetMedicineByBarcodeQuery("bar-100200"); // case-insensitive lookup
        var result = await _barcodeHandler.HandleAsync(query);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(seeded.Id, result.Value.Id);
        Assert.Equal("BAR-100200", result.Value.Barcode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_WithEmptyBarcode_ReturnsFailure(string? barcode)
    {
        var query = new GetMedicineByBarcodeQuery(barcode!);
        var result = await _barcodeHandler.HandleAsync(query);

        Assert.True(result.IsFailure);
        Assert.Contains("Barcode cannot be empty", result.Error);
    }

    [Fact]
    public async Task HandleAsync_WithNonExistentBarcode_ReturnsFailure()
    {
        var query = new GetMedicineByBarcodeQuery("NOT-FOUND-BARCODE");
        var result = await _barcodeHandler.HandleAsync(query);

        Assert.True(result.IsFailure);
        Assert.Contains("was not found", result.Error);
    }
}
