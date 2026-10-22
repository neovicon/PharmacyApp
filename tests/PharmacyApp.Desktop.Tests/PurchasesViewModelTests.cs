using Microsoft.Extensions.DependencyInjection;
using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Authentication;
using PharmacyApp.Application.Common;
using PharmacyApp.Application.Medicines.Commands;
using PharmacyApp.Desktop.ViewModels.Purchases;
using PharmacyApp.Domain.Enums;
using PharmacyApp.Infrastructure;
using Xunit;

namespace PharmacyApp.Desktop.Tests;

public class PurchasesViewModelTests : IDisposable
{
    private readonly IsolatedDesktopTestPathProvider _pathProvider = new();
    private readonly ServiceProvider _provider;
    private readonly ISessionManager _sessionManager;

    public PurchasesViewModelTests()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure();
        services.AddSingleton<IAppPathProvider>(_pathProvider);
        services.AddDesktopServices();

        _provider = services.BuildServiceProvider();
        var initializer = _provider.GetRequiredService<PharmacyApp.Infrastructure.Persistence.SqliteDatabaseInitializer>();
        initializer.InitializeAsync().GetAwaiter().GetResult();

        var createAdminHandler = _provider.GetRequiredService<ICommandHandler<PharmacyApp.Application.Authentication.Commands.CreateInitialAdminCommand, Guid>>();
        var adminId = createAdminHandler.HandleAsync(new PharmacyApp.Application.Authentication.Commands.CreateInitialAdminCommand("admin", "System Admin", "AdminPass123!", "AdminPass123!")).GetAwaiter().GetResult().Value;

        _sessionManager = _provider.GetRequiredService<ISessionManager>();
        _sessionManager.StartSession(new UserSessionDto(
            adminId,
            "admin",
            "System Admin",
            UserRole.Admin));
    }

    public void Dispose()
    {
        _provider.Dispose();
        _pathProvider.Dispose();
    }

    [Fact]
    public async Task InitializeAsync_LoadsMedicinesAndSuppliers()
    {
        // 1. Create a medicine first
        var createMedHandler = _provider.GetRequiredService<ICommandHandler<CreateMedicineCommand, Guid>>();
        await createMedHandler.HandleAsync(new CreateMedicineCommand(
            Name: "Amoxicillin 500mg",
            GenericName: "Amoxicillin",
            DosageForm: "Capsule",
            DispensingUnit: "Capsules",
            ReorderLevel: 20,
            Category: "Antibiotics",
            Strength: "500mg",
            Brand: "Nepal Pharmaceuticals"));

        var vm = _provider.GetRequiredService<PurchasesViewModel>();
        await vm.InitializeAsync();

        Assert.NotEmpty(vm.AvailableMedicines);
        Assert.NotNull(vm.SelectedMedicine);
        Assert.Equal("Amoxicillin 500mg", vm.SelectedMedicine.Name);
    }

    [Fact]
    public async Task AddSupplier_SavesNewSupplier_AndAutoSelectsIt()
    {
        var vm = _provider.GetRequiredService<PurchasesViewModel>();
        await vm.InitializeAsync();

        vm.OpenAddSupplierModalCommand.Execute(null);
        Assert.True(vm.IsAddSupplierModalOpen);

        vm.NewSupplierName = "Apex Pharma Wholesale Pvt Ltd";
        vm.NewSupplierContactPerson = "Sunil Thapa";
        vm.NewSupplierPhone = "9841000000";
        vm.NewSupplierPan = "601234567";
        vm.NewSupplierAddress = "Tripureshwor, Kathmandu";

        await vm.SaveNewSupplierCommand.ExecuteAsync(null);

        Assert.False(vm.IsAddSupplierModalOpen);
        Assert.NotNull(vm.SelectedSupplier);
        Assert.Equal("Apex Pharma Wholesale Pvt Ltd", vm.SelectedSupplier.Name);
        Assert.Contains(vm.AvailableSuppliers, s => s.Name == "Apex Pharma Wholesale Pvt Ltd");
    }

    [Fact]
    public async Task AddSupplier_WithEmptyName_ShowsValidationError()
    {
        var vm = _provider.GetRequiredService<PurchasesViewModel>();
        vm.OpenAddSupplierModalCommand.Execute(null);
        vm.NewSupplierName = "   ";

        await vm.SaveNewSupplierCommand.ExecuteAsync(null);

        Assert.True(vm.IsAddSupplierModalOpen);
        Assert.NotNull(vm.AddSupplierErrorMessage);
        Assert.Contains("required", vm.AddSupplierErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitPurchase_WithValidDataAndSupplier_ReceivesBatchSuccessfully()
    {
        // 1. Create a medicine
        var createMedHandler = _provider.GetRequiredService<ICommandHandler<CreateMedicineCommand, Guid>>();
        var medId = (await createMedHandler.HandleAsync(new CreateMedicineCommand(
            Name: "Paracetamol 500mg",
            GenericName: "Paracetamol",
            DosageForm: "Tablet",
            DispensingUnit: "Tablets",
            ReorderLevel: 50,
            Category: "Analgesics",
            Strength: "500mg",
            Brand: "Deurali Janta"))).Value;

        var vm = _provider.GetRequiredService<PurchasesViewModel>();
        await vm.InitializeAsync();

        // 2. Add and select supplier
        vm.NewSupplierName = "Deurali Janta Distributors";
        await vm.SaveNewSupplierCommand.ExecuteAsync(null);

        // 3. Configure purchase intake
        vm.SelectedMedicine = vm.AvailableMedicines.First(m => m.Id == medId);
        vm.BatchCode = "DJ-BATCH-01";
        vm.Quantity = 500;
        vm.PurchaseCostNpr = 2.50m;
        vm.SellingPriceNpr = 4.00m;
        vm.ManufacturerBatchNumber = "LOT-7788";
        vm.StorageLocation = "Shelf B-2";
        vm.Notes = "Invoice #INV-2026-99";

        await vm.SubmitPurchaseCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorMessage);
        Assert.NotEmpty(vm.RecentBatches);
        var recentBatch = vm.RecentBatches.First(b => b.BatchCode == "DJ-BATCH-01");
        Assert.Equal(500, recentBatch.QuantityOnHand);
        Assert.Equal(2.50m, recentBatch.PurchaseCostNpr);
        Assert.Equal(4.00m, recentBatch.SellingPriceNpr);
    }

    [Fact]
    public async Task SubmitPurchase_ValidationChecks()
    {
        var vm = _provider.GetRequiredService<PurchasesViewModel>();
        await vm.InitializeAsync();

        // Without medicine
        vm.SelectedMedicine = null;
        await vm.SubmitPurchaseCommand.ExecuteAsync(null);
        Assert.NotNull(vm.ErrorMessage);

        // With medicine but empty batch code
        var createMedHandler = _provider.GetRequiredService<ICommandHandler<CreateMedicineCommand, Guid>>();
        await createMedHandler.HandleAsync(new CreateMedicineCommand(
            Name: "Cetirizine 10mg",
            GenericName: "Cetirizine",
            DosageForm: "Tablet",
            DispensingUnit: "Tablets",
            ReorderLevel: 20,
            Category: "Antihistamines",
            Strength: "10mg",
            Brand: "National Healthcare"));

        await vm.InitializeAsync();
        vm.BatchCode = "";
        await vm.SubmitPurchaseCommand.ExecuteAsync(null);
        Assert.Contains("batch code", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Negative quantity
        vm.BatchCode = "B-001";
        vm.Quantity = -10;
        await vm.SubmitPurchaseCommand.ExecuteAsync(null);
        Assert.Contains("greater than zero", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Negative price
        vm.Quantity = 10;
        vm.PurchaseCostNpr = -5m;
        await vm.SubmitPurchaseCommand.ExecuteAsync(null);
        Assert.Contains("negative", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
