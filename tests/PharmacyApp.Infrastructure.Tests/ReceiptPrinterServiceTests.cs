using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Sales;
using PharmacyApp.Application.Settings;
using PharmacyApp.Domain.Enums;
using PharmacyApp.Infrastructure.Services;
using Xunit;

namespace PharmacyApp.Infrastructure.Tests;

public class ReceiptPrinterServiceTests : IDisposable
{
    private readonly IsolatedTestPathProvider _pathProvider;
    private readonly JsonPharmacyProfileService _profileService;
    private readonly ReceiptPrinterService _printerService;

    public ReceiptPrinterServiceTests()
    {
        _pathProvider = new IsolatedTestPathProvider();
        _profileService = new JsonPharmacyProfileService(_pathProvider);
        _printerService = new ReceiptPrinterService(_pathProvider, _profileService);
    }

    public void Dispose()
    {
        _pathProvider.Dispose();
    }

    [Fact]
    public void FormatThermalReceipt_ContainsAllReceiptElements()
    {
        var receipt = new SaleReceiptDto(
            TransactionId: Guid.NewGuid(),
            InvoiceNumber: "INV-20261022-PRINT1",
            TimestampUtc: new DateTimeOffset(2026, 10, 22, 14, 0, 0, TimeSpan.Zero),
            CustomerName: "Bishal Thapa",
            PrescriptionNumber: "RX-4488",
            PaymentMethod: PaymentMethod.Cash,
            Lines: new[]
            {
                new SaleReceiptLineDto(Guid.NewGuid(), Guid.NewGuid(), "Paracetamol 500mg", "LOT-99", 2, 10m, 20m),
                new SaleReceiptLineDto(Guid.NewGuid(), Guid.NewGuid(), "Vitamin C 500mg", "LOT-88", 1, 30m, 30m)
            },
            SubTotalNpr: 50m,
            DiscountNpr: 5m,
            TaxNpr: 5.85m,
            TotalAmountNpr: 50.85m,
            CashierName: "Som Bdr Rai");

        string formatted = _printerService.FormatThermalReceipt(receipt);

        Assert.Contains("INV-20261022-PRINT1", formatted);
        Assert.Contains("Bishal Thapa", formatted);
        Assert.Contains("RX-4488", formatted);
        Assert.Contains("Paracetamol 500mg", formatted);
        Assert.Contains("Vitamin C 500mg", formatted);
        Assert.Contains("NPR 50.00", formatted);
        Assert.Contains("NPR 5.00", formatted);
        Assert.Contains("NPR 50.85", formatted);
    }

    [Fact]
    public async Task ExportReceiptToFileAsync_WritesFileToDisk()
    {
        var receipt = new SaleReceiptDto(
            TransactionId: Guid.NewGuid(),
            InvoiceNumber: "INV-FILE-EXPORT",
            TimestampUtc: DateTimeOffset.UtcNow,
            CustomerName: null,
            PrescriptionNumber: null,
            PaymentMethod: PaymentMethod.Cash,
            Lines: new[]
            {
                new SaleReceiptLineDto(Guid.NewGuid(), Guid.NewGuid(), "Flexon Tablet", "LOT-1", 1, 15m, 15m)
            },
            SubTotalNpr: 15m,
            DiscountNpr: 0m,
            TaxNpr: 0m,
            TotalAmountNpr: 15m,
            CashierName: "Cashier");

        string filePath = await _printerService.ExportReceiptToFileAsync(receipt);

        Assert.True(File.Exists(filePath));
        string content = await File.ReadAllTextAsync(filePath);
        Assert.Contains("INV-FILE-EXPORT", content);
        Assert.Contains("Flexon Tablet", content);
    }
}
