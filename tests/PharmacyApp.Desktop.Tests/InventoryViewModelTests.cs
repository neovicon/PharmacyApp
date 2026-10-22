using Microsoft.Extensions.DependencyInjection;
using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Authentication;
using PharmacyApp.Application.Common;
using PharmacyApp.Application.Inventory.Commands;
using PharmacyApp.Application.Medicines.Commands;
using PharmacyApp.Desktop.ViewModels.Inventory;
using PharmacyApp.Domain.Enums;
using PharmacyApp.Infrastructure;
using Xunit;

namespace PharmacyApp.Desktop.Tests;

public class InventoryViewModelTests : IDisposable
{
    private readonly IsolatedDesktopTestPathProvider _pathProvider = new();
    private readonly ServiceProvider _provider;
    private readonly ISessionManager _sessionManager;

    public InventoryViewModelTests()
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
    public async Task SubmitAdjustment_ReturnFromCustomer_UpdatesStock()
    {
        // 1. Create medicine and batch
        var createMedHandler = _provider.GetRequiredService<ICommandHandler<CreateMedicineCommand, Guid>>();
        var medId = (await createMedHandler.HandleAsync(new CreateMedicineCommand(
            Name: "Azithromycin 500mg",
            GenericName: "Azithromycin",
            DosageForm: "Tablet",
            DispensingUnit: "Tablets",
            ReorderLevel: 10,
            Category: "Antibiotics",
            Strength: "500mg",
            Brand: "Nepal Pharma"))).Value;

        var receiveBatchHandler = _provider.GetRequiredService<ICommandHandler<ReceiveBatchCommand, Guid>>();
        var batchId = (await receiveBatchHandler.HandleAsync(new ReceiveBatchCommand(
            MedicineId: medId,
            BatchCode: "AZI-2026-01",
            Quantity: 40,
            PurchaseCostNpr: 15m,
            SellingPriceNpr: 25m,
            ExpiryDate: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1))))).Value;

        // 2. Open adjustment modal for Customer Return (index 5)
        var vm = _provider.GetRequiredService<InventoryViewModel>();
        await vm.LoadBatchesAsync();

        vm.SelectedBatch = vm.Batches.First(b => b.Id == batchId);
        vm.OpenAdjustModalCommand.Execute(null);

        Assert.True(vm.IsAdjustModalOpen);

        vm.SelectedAdjustmentTypeIndex = 5; // ReturnFromCustomer
        vm.AdjustNewQuantity = 45; // +5 units restocked
        vm.AdjustReason = "Patient returned 5 unopened tablets";

        await vm.SubmitAdjustmentCommand.ExecuteAsync(null);

        Assert.False(vm.IsAdjustModalOpen);
        Assert.Null(vm.AdjustErrorMessage);

        // 3. Verify in repository
        var batchRepo = _provider.GetRequiredService<IBatchRepository>();
        var updatedBatch = await batchRepo.GetByIdAsync(batchId);
        Assert.NotNull(updatedBatch);
        Assert.Equal(45, updatedBatch.QuantityOnHand);
    }

    [Fact]
    public async Task SubmitAdjustment_ReturnToSupplier_DeductsStock()
    {
        var createMedHandler = _provider.GetRequiredService<ICommandHandler<CreateMedicineCommand, Guid>>();
        var medId = (await createMedHandler.HandleAsync(new CreateMedicineCommand(
            Name: "Pantoprazole 40mg",
            GenericName: "Pantoprazole",
            DosageForm: "Tablet",
            DispensingUnit: "Tablets",
            ReorderLevel: 10,
            Category: "Antacid",
            Strength: "40mg",
            Brand: "Apex"))).Value;

        var receiveBatchHandler = _provider.GetRequiredService<ICommandHandler<ReceiveBatchCommand, Guid>>();
        var batchId = (await receiveBatchHandler.HandleAsync(new ReceiveBatchCommand(
            MedicineId: medId,
            BatchCode: "PAN-2026-01",
            Quantity: 100,
            PurchaseCostNpr: 4m,
            SellingPriceNpr: 7m,
            ExpiryDate: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1))))).Value;

        var vm = _provider.GetRequiredService<InventoryViewModel>();
        await vm.LoadBatchesAsync();

        vm.SelectedBatch = vm.Batches.First(b => b.Id == batchId);
        vm.OpenAdjustModalCommand.Execute(null);

        vm.SelectedAdjustmentTypeIndex = 3; // ReturnToSupplier
        vm.AdjustNewQuantity = 80; // 20 returned to supplier
        vm.AdjustReason = "Wholesale recall batch return";

        await vm.SubmitAdjustmentCommand.ExecuteAsync(null);

        Assert.False(vm.IsAdjustModalOpen);
        Assert.Null(vm.AdjustErrorMessage);

        var batchRepo = _provider.GetRequiredService<IBatchRepository>();
        var updatedBatch = await batchRepo.GetByIdAsync(batchId);
        Assert.NotNull(updatedBatch);
        Assert.Equal(80, updatedBatch.QuantityOnHand);
    }
}
