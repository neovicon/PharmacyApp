using PharmacyApp.Application.Medicines.Queries;
using PharmacyApp.Application.Tests.TestDoubles;
using PharmacyApp.Domain.Entities;
using Xunit;

namespace PharmacyApp.Application.Tests;

public class SearchAndListMedicinesQueryHandlerTests
{
    private readonly TestMedicineRepository _repository = new();
    private readonly SearchMedicinesQueryHandler _searchHandler;
    private readonly ListMedicinesQueryHandler _listHandler;

    public SearchAndListMedicinesQueryHandlerTests()
    {
        _searchHandler = new SearchMedicinesQueryHandler(_repository);
        _listHandler = new ListMedicinesQueryHandler(_repository);
    }

    private async Task SeedDataAsync()
    {
        var m1 = Medicine.Create("Amoxicillin 500mg", "Amoxicillin", "Capsule", "Box", 20, brand: "Moxatag", category: "Antibiotic", barcode: "111001");
        var m2 = Medicine.Create("Azithromycin 250mg", "Azithromycin", "Tablet", "Strip", 15, brand: "Zithromax", category: "Antibiotic", barcode: "111002");
        var m3 = Medicine.Create("Paracetamol 500mg", "Paracetamol", "Tablet", "Strip", 50, brand: "Panadol", category: "Analgesic", barcode: "111003");
        var m4 = Medicine.Create("Ibuprofen 400mg", "Ibuprofen", "Tablet", "Strip", 30, brand: "Advil", category: "NSAID", barcode: "111004");
        m4.Deactivate(DateTimeOffset.UtcNow); // inactive

        await _repository.AddAsync(m1);
        await _repository.AddAsync(m2);
        await _repository.AddAsync(m3);
        await _repository.AddAsync(m4);
    }

    [Fact]
    public async Task SearchAsync_ByNameOrGeneric_ReturnsMatchingItems()
    {
        await SeedDataAsync();

        var query = new SearchMedicinesQuery("amox");
        var result = await _searchHandler.HandleAsync(query);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal("Amoxicillin 500mg", result.Value[0].Name);
    }

    [Fact]
    public async Task SearchAsync_ByCategory_ReturnsAllMatchingCategory()
    {
        await SeedDataAsync();

        var query = new SearchMedicinesQuery("Antibiotic");
        var result = await _searchHandler.HandleAsync(query);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
    }

    [Fact]
    public async Task SearchAsync_ByBarcode_ReturnsExactMatch()
    {
        await SeedDataAsync();

        var query = new SearchMedicinesQuery("111003");
        var result = await _searchHandler.HandleAsync(query);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal("Paracetamol 500mg", result.Value[0].Name);
    }

    [Fact]
    public async Task SearchAsync_FilteredByActive_ExcludesInactive()
    {
        await SeedDataAsync();

        // Search NSAID which has 1 inactive item
        var queryActive = new SearchMedicinesQuery("NSAID", IsActive: true);
        var activeResult = await _searchHandler.HandleAsync(queryActive);

        Assert.True(activeResult.IsSuccess);
        Assert.Empty(activeResult.Value);

        var queryAll = new SearchMedicinesQuery("NSAID", IsActive: null);
        var allResult = await _searchHandler.HandleAsync(queryAll);

        Assert.True(allResult.IsSuccess);
        Assert.Single(allResult.Value);
        Assert.Equal("Ibuprofen 400mg", allResult.Value[0].Name);
    }

    [Fact]
    public async Task ListAsync_ReturnsAllMedicinesOrderedByName()
    {
        await SeedDataAsync();

        var query = new ListMedicinesQuery();
        var result = await _listHandler.HandleAsync(query);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value.Count);
        Assert.Equal("Amoxicillin 500mg", result.Value[0].Name);
        Assert.Equal("Azithromycin 250mg", result.Value[1].Name);
        Assert.Equal("Ibuprofen 400mg", result.Value[2].Name);
        Assert.Equal("Paracetamol 500mg", result.Value[3].Name);
    }

    [Fact]
    public async Task ListAsync_FilteredByActive_ReturnsOnlyActive()
    {
        await SeedDataAsync();

        var query = new ListMedicinesQuery(IsActive: true);
        var result = await _listHandler.HandleAsync(query);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Count);
        Assert.All(result.Value, m => Assert.True(m.IsActive));
    }
}
