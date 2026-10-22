using PharmacyApp.Application.Sales.Commands;
using PharmacyApp.Application.Tests.TestDoubles;
using PharmacyApp.Domain.Entities;
using PharmacyApp.Domain.Enums;
using PharmacyApp.Domain.ValueObjects;
using Xunit;

namespace PharmacyApp.Application.Tests;

public class ProcessSaleCommandHandlerTests
{
    private readonly TestMedicineRepository _medicineRepository = new();
    private readonly TestBatchRepository _batchRepository = new();
    private readonly TestStockAdjustmentRepository _stockAdjustmentRepository = new();
    private readonly TestTransactionRunner _transactionRunner = new();
    private readonly TestCurrentUserService _currentUserService = new();
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero));
    private readonly ProcessSaleCommandHandler _handler;

    public ProcessSaleCommandHandlerTests()
    {
        _handler = new ProcessSaleCommandHandler(
            _batchRepository,
            _medicineRepository,
            _stockAdjustmentRepository,
            _transactionRunner,
            _currentUserService,
            _dateTimeProvider);
    }

    private async Task<(Medicine Med, Batch Batch)> SeedMedicineAndBatchAsync(
        int qty = 100,
        decimal sellingPrice = 25m,
        DateOnly? expiry = null,
        bool requiresPrescription = false)
    {
        var med = Medicine.Create(
            name: "Amoxicillin 500mg",
            genericName: "Amoxicillin",
            dosageForm: "Capsule",
            dispensingUnit: "Strip",
            reorderLevel: 20,
            requiresPrescription: requiresPrescription);

        await _medicineRepository.AddAsync(med);

        var batch = Batch.Create(
            medicineId: med.Id,
            batchCode: "BATCH-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            quantityOnHand: qty,
            purchaseCost: Money.FromNpr(15m),
            sellingPrice: Money.FromNpr(sellingPrice),
            expiryDate: expiry ?? new DateOnly(2027, 12, 31));

        await _batchRepository.AddAsync(batch);
        return (med, batch);
    }

    [Fact]
    public async Task HandleAsync_ValidSale_DeductsStockGeneratesReceiptAndRecordsAudit()
    {
        var (med, batch) = await SeedMedicineAndBatchAsync(qty: 50, sellingPrice: 20m);

        var command = new ProcessSaleCommand(
            Items: new[] { new SaleLineItemRequest(batch.Id, 5) },
            CustomerName: "John Doe");

        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(100m, result.Value.TotalAmountNpr);
        Assert.Single(result.Value.Lines);
        Assert.Equal(5, result.Value.Lines[0].Quantity);
        Assert.Equal(20m, result.Value.Lines[0].UnitPriceNpr);
        Assert.Equal(100m, result.Value.Lines[0].LineTotalNpr);

        var updatedBatch = await _batchRepository.GetByIdAsync(batch.Id);
        Assert.NotNull(updatedBatch);
        Assert.Equal(45, updatedBatch.QuantityOnHand);

        var adjustments = await _stockAdjustmentRepository.GetByBatchIdAsync(batch.Id);
        Assert.Single(adjustments);
        Assert.Equal(StockAdjustmentType.SaleDispense, adjustments[0].AdjustmentType);
        Assert.Equal(50, adjustments[0].PreviousQuantity);
        Assert.Equal(45, adjustments[0].NewQuantity);
        Assert.Equal(-5, adjustments[0].QuantityDelta);
    }

    [Fact]
    public async Task HandleAsync_WhenSellingMoreThanQuantityOnHand_RejectsToPreventNegativeStock()
    {
        var (med, batch) = await SeedMedicineAndBatchAsync(qty: 10);

        var command = new ProcessSaleCommand(
            Items: new[] { new SaleLineItemRequest(batch.Id, 15) }); // 15 > 10

        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Contains("Insufficient stock", result.Error);

        var unaffectedBatch = await _batchRepository.GetByIdAsync(batch.Id);
        Assert.NotNull(unaffectedBatch);
        Assert.Equal(10, unaffectedBatch.QuantityOnHand);
    }

    [Fact]
    public async Task HandleAsync_WhenBatchIsExpired_RejectsWithSafetyViolation()
    {
        // Expired yesterday
        var yesterday = new DateOnly(2026, 10, 19);
        var (med, batch) = await SeedMedicineAndBatchAsync(qty: 50, expiry: yesterday);

        var command = new ProcessSaleCommand(
            Items: new[] { new SaleLineItemRequest(batch.Id, 2) });

        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Contains("Safety violation", result.Error);
        Assert.Contains("expired", result.Error);
    }

    [Fact]
    public async Task HandleAsync_WhenBatchHasUnknownExpiry_RejectsWithSafetyViolation()
    {
        var med = Medicine.Create("Aspirin", "Aspirin", "Tablet", "Strip", 10);
        await _medicineRepository.AddAsync(med);

        // Batch with NULL expiry date
        var batch = Batch.Create(
            med.Id,
            "BATCH-NO-EXPIRY",
            quantityOnHand: 20,
            purchaseCost: Money.FromNpr(5m),
            sellingPrice: Money.FromNpr(10m),
            expiryDate: null);

        await _batchRepository.AddAsync(batch);

        var command = new ProcessSaleCommand(
            Items: new[] { new SaleLineItemRequest(batch.Id, 1) });

        var result = await _handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Contains("Safety violation", result.Error);
        Assert.Contains("unknown expiry date", result.Error);
    }

    [Fact]
    public async Task HandleAsync_WhenPrescriptionRequiredAndMissing_RejectsSale()
    {
        var (med, batch) = await SeedMedicineAndBatchAsync(qty: 20, requiresPrescription: true);

        var commandWithoutRx = new ProcessSaleCommand(
            Items: new[] { new SaleLineItemRequest(batch.Id, 2) },
            PrescriptionNumber: null);

        var result = await _handler.HandleAsync(commandWithoutRx);

        Assert.True(result.IsFailure);
        Assert.Contains("Prescription required", result.Error);

        var commandWithRx = new ProcessSaleCommand(
            Items: new[] { new SaleLineItemRequest(batch.Id, 2) },
            PrescriptionNumber: "RX-2026-9999");

        var successResult = await _handler.HandleAsync(commandWithRx);
        Assert.True(successResult.IsSuccess);
    }

    [Fact]
    public async Task HandleAsync_WithInvoiceRepository_PersistsInvoiceAndAppliesDiscountAndTax()
    {
        var invoiceRepo = new TestInvoiceRepository();
        var handler = new ProcessSaleCommandHandler(
            _batchRepository,
            _medicineRepository,
            _stockAdjustmentRepository,
            _transactionRunner,
            _currentUserService,
            _dateTimeProvider,
            invoiceRepo);

        var (med, batch) = await SeedMedicineAndBatchAsync(qty: 50, sellingPrice: 100m);

        var command = new ProcessSaleCommand(
            Items: new[] { new SaleLineItemRequest(batch.Id, 2) },
            CustomerName: "Anil Karki",
            PaymentMethod: PaymentMethod.DigitalWalletQr,
            DiscountAmountNpr: 20m,
            TaxPercentage: 10m);

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal(200m, result.Value.SubTotalNpr);
        Assert.Equal(20m, result.Value.DiscountNpr);
        Assert.Equal(18m, result.Value.TaxNpr); // (200 - 20) * 0.10 = 18
        Assert.Equal(198m, result.Value.TotalAmountNpr); // 180 + 18 = 198
        Assert.Equal(PaymentMethod.DigitalWalletQr, result.Value.PaymentMethod);

        Assert.Single(invoiceRepo.Invoices);
        var savedInvoice = invoiceRepo.Invoices[0];
        Assert.Equal(result.Value.InvoiceNumber, savedInvoice.InvoiceNumber);
        Assert.Equal(198m, savedInvoice.TotalAmount.Amount);
        Assert.Single(savedInvoice.Items);
        Assert.Equal(2, savedInvoice.Items[0].Quantity);
    }
}
