using Microsoft.Extensions.DependencyInjection;
using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Authentication;
using PharmacyApp.Application.Common;
using PharmacyApp.Application.Inventory.Commands;
using PharmacyApp.Application.Medicines.Commands;
using PharmacyApp.Desktop.ViewModels.Sales;
using PharmacyApp.Domain.Enums;
using PharmacyApp.Infrastructure;
using Xunit;

namespace PharmacyApp.Desktop.Tests;

public class SalesViewModelTests : IDisposable
{
    private readonly IsolatedDesktopTestPathProvider _pathProvider = new();
    private readonly ServiceProvider _provider;
    private readonly ISessionManager _sessionManager;

    public SalesViewModelTests()
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
    public async Task CustomerReturn_RestocksBatch_AndCalculatesRefund()
    {
        // 1. Create a medicine and receive a batch
        var createMedHandler = _provider.GetRequiredService<ICommandHandler<CreateMedicineCommand, Guid>>();
        var medId = (await createMedHandler.HandleAsync(new CreateMedicineCommand(
            Name: "Ibuprofen 400mg",
            GenericName: "Ibuprofen",
            DosageForm: "Tablet",
            DispensingUnit: "Tablets",
            ReorderLevel: 20,
            Category: "NSAIDs",
            Strength: "400mg",
            Brand: "Nepal Pharma"))).Value;

        var receiveBatchHandler = _provider.GetRequiredService<ICommandHandler<ReceiveBatchCommand, Guid>>();
        var batchId = (await receiveBatchHandler.HandleAsync(new ReceiveBatchCommand(
            MedicineId: medId,
            BatchCode: "IBU-2026-01",
            Quantity: 50,
            PurchaseCostNpr: 5.00m,
            SellingPriceNpr: 8.00m,
            ExpiryDate: DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(12))))).Value;

        // 2. Open return modal in SalesViewModel
        var vm = _provider.GetRequiredService<SalesViewModel>();
        await vm.OpenReturnModalCommand.ExecuteAsync(null);

        Assert.True(vm.IsReturnModalOpen);
        Assert.NotEmpty(vm.ReturnBatches);

        vm.ReturnSelectedBatch = vm.ReturnBatches.First(b => b.Id == batchId);
        vm.ReturnQuantity = 5;
        vm.ReturnCustomerName = "Hari Krishna";
        vm.ReturnReason = "Unopened blister pack returned within 24h";

        Assert.Equal(40.00m, vm.ReturnRefundTotalNpr); // 5 * 8.00 = 40.00

        // 3. Process return
        await vm.ProcessReturnCommand.ExecuteAsync(null);

        Assert.False(vm.IsReturnModalOpen);
        Assert.Null(vm.ReturnErrorMessage);

        // 4. Verify batch quantity was restocked from 50 to 55
        var batchRepo = _provider.GetRequiredService<IBatchRepository>();
        var updatedBatch = await batchRepo.GetByIdAsync(batchId);
        Assert.NotNull(updatedBatch);
        Assert.Equal(55, updatedBatch.QuantityOnHand);
    }

    [Fact]
    public async Task CustomerReturn_ValidationFailures()
    {
        var vm = _provider.GetRequiredService<SalesViewModel>();
        await vm.OpenReturnModalCommand.ExecuteAsync(null);

        // Null batch
        vm.ReturnSelectedBatch = null;
        await vm.ProcessReturnCommand.ExecuteAsync(null);
        Assert.NotNull(vm.ReturnErrorMessage);
        Assert.Contains("select a medicine batch", vm.ReturnErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Zero or negative quantity
        var createMedHandler = _provider.GetRequiredService<ICommandHandler<CreateMedicineCommand, Guid>>();
        var medId = (await createMedHandler.HandleAsync(new CreateMedicineCommand(
            Name: "Metformin 500mg",
            GenericName: "Metformin",
            DosageForm: "Tablet",
            DispensingUnit: "Tablets",
            ReorderLevel: 20,
            Category: "Antidiabetic",
            Strength: "500mg",
            Brand: "Sun Pharma"))).Value;

        var receiveBatchHandler = _provider.GetRequiredService<ICommandHandler<ReceiveBatchCommand, Guid>>();
        await receiveBatchHandler.HandleAsync(new ReceiveBatchCommand(
            MedicineId: medId,
            BatchCode: "MET-2026-01",
            Quantity: 100,
            PurchaseCostNpr: 2.00m,
            SellingPriceNpr: 3.50m,
            ExpiryDate: DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(12))));

        await vm.OpenReturnModalCommand.ExecuteAsync(null);
        vm.ReturnSelectedBatch = vm.ReturnBatches.First();
        vm.ReturnQuantity = 0;
        await vm.ProcessReturnCommand.ExecuteAsync(null);
        Assert.Contains("at least 1", vm.ReturnErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Empty reason
        vm.ReturnQuantity = 2;
        vm.ReturnReason = "";
        await vm.ProcessReturnCommand.ExecuteAsync(null);
        Assert.Contains("reason", vm.ReturnErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddToCart_BlocksExpiredBatch()
    {
        var createMedHandler = _provider.GetRequiredService<ICommandHandler<CreateMedicineCommand, Guid>>();
        var medId = (await createMedHandler.HandleAsync(new CreateMedicineCommand(
            Name: "Expired Cough Syrup",
            GenericName: "Dextromethorphan",
            DosageForm: "Syrup",
            DispensingUnit: "Bottles",
            ReorderLevel: 10,
            Category: "Cough & Cold",
            Strength: "100ml",
            Brand: "Pharma"))).Value;

        var receiveBatchHandler = _provider.GetRequiredService<ICommandHandler<ReceiveBatchCommand, Guid>>();
        await receiveBatchHandler.HandleAsync(new ReceiveBatchCommand(
            MedicineId: medId,
            BatchCode: "EXP-999",
            Quantity: 20,
            PurchaseCostNpr: 10m,
            SellingPriceNpr: 20m,
            ExpiryDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10))));

        var vm = _provider.GetRequiredService<SalesViewModel>();
        await vm.LoadInitialMedicinesAsync();

        vm.SelectedMedicine = vm.SearchResults.First(m => m.Id == medId);
        if (vm.SelectedBatch != null)
        {
            vm.AddToCartCommand.Execute(null);
            Assert.Contains("expired", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(vm.CartItems);
        }
    }

    [Fact]
    public async Task CompleteSale_GeneratesInvoiceAndReceipt_AllowsReprintAndExport()
    {
        var createMedHandler = _provider.GetRequiredService<ICommandHandler<CreateMedicineCommand, Guid>>();
        var medId = (await createMedHandler.HandleAsync(new CreateMedicineCommand(
            Name: "Amoxicillin 250mg",
            GenericName: "Amoxicillin",
            DosageForm: "Capsule",
            DispensingUnit: "Strips",
            ReorderLevel: 10,
            Category: "Antibiotic",
            Strength: "250mg",
            Brand: "Medica"))).Value;

        var receiveBatchHandler = _provider.GetRequiredService<ICommandHandler<ReceiveBatchCommand, Guid>>();
        await receiveBatchHandler.HandleAsync(new ReceiveBatchCommand(
            MedicineId: medId,
            BatchCode: "LOT-AMX-99",
            Quantity: 50,
            PurchaseCostNpr: 10m,
            SellingPriceNpr: 20m,
            ExpiryDate: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2))));

        var vm = _provider.GetRequiredService<SalesViewModel>();
        await vm.LoadInitialMedicinesAsync();

        vm.SelectedMedicine = vm.SearchResults.First(m => m.Id == medId);
        Assert.NotNull(vm.SelectedBatch);

        vm.QuantityToDispense = 2;
        vm.AddToCartCommand.Execute(null);
        Assert.Single(vm.CartItems);

        vm.SelectedPaymentMethod = PaymentMethod.Card;
        vm.CustomerName = "Deepak Silwal";
        vm.DiscountAmountNpr = 5m; // 40 - 5 = 35

        await vm.CompleteSaleCommand.ExecuteAsync(null);

        Assert.True(vm.IsReceiptOpen);
        Assert.NotNull(vm.LastReceipt);
        Assert.StartsWith("INV-", vm.LastReceipt.InvoiceNumber);
        Assert.Equal(40m, vm.LastReceipt.SubTotalNpr);
        Assert.Equal(5m, vm.LastReceipt.DiscountNpr);
        Assert.Equal(35m, vm.LastReceipt.TotalAmountNpr);

        // Test receipt print/export
        await vm.PrintOrExportReceiptCommand.ExecuteAsync(null);

        // Test history search and reprint
        await vm.OpenHistoryModalCommand.ExecuteAsync(null);
        Assert.True(vm.IsHistoryModalOpen);
        Assert.Contains(vm.RecentInvoices, i => i.InvoiceNumber == vm.LastReceipt.InvoiceNumber);

        var historyInv = vm.RecentInvoices.First(i => i.InvoiceNumber == vm.LastReceipt.InvoiceNumber);
        vm.ReprintInvoiceReceiptCommand.Execute(historyInv);
        Assert.False(vm.IsHistoryModalOpen);
        Assert.True(vm.IsReceiptOpen);
        Assert.Equal(historyInv.InvoiceNumber, vm.LastReceipt.InvoiceNumber);
    }
}
