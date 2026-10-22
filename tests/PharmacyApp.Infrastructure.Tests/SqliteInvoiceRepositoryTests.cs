using Microsoft.Data.Sqlite;
using PharmacyApp.Domain.Entities;
using PharmacyApp.Domain.Enums;
using PharmacyApp.Domain.ValueObjects;
using PharmacyApp.Infrastructure.Persistence;
using Xunit;

namespace PharmacyApp.Infrastructure.Tests;

public class SqliteInvoiceRepositoryTests : IDisposable
{
    private readonly IsolatedTestPathProvider _pathProvider;
    private readonly string _connectionString;
    private readonly SqliteDatabaseInitializer _initializer;
    private readonly SqliteMedicineRepository _medicineRepository;
    private readonly SqliteBatchRepository _batchRepository;
    private readonly SqliteInvoiceRepository _repository;
    private readonly Medicine _seedMedicine;
    private readonly Batch _seedBatch;

    public SqliteInvoiceRepositoryTests()
    {
        _pathProvider = new IsolatedTestPathProvider();
        _connectionString = $"Data Source={Path.Combine(_pathProvider.DataDirectory, "pharmacy.db")};Cache=Shared;Foreign Keys=True;";
        _initializer = new SqliteDatabaseInitializer(_connectionString);
        _initializer.InitializeAsync().GetAwaiter().GetResult();

        _medicineRepository = new SqliteMedicineRepository(_connectionString);
        _batchRepository = new SqliteBatchRepository(_connectionString);
        _repository = new SqliteInvoiceRepository(_connectionString);

        _seedMedicine = Medicine.Create("Cetirizine 10mg", "Cetirizine", "Tablet", "Strip", 15);
        _medicineRepository.AddAsync(_seedMedicine).GetAwaiter().GetResult();

        _seedBatch = Batch.Create(
            medicineId: _seedMedicine.Id,
            batchCode: "LOT-CET-01",
            quantityOnHand: 200,
            purchaseCost: Money.FromNpr(10m),
            sellingPrice: Money.FromNpr(15m),
            expiryDate: new DateOnly(2028, 6, 30));
        _batchRepository.AddAsync(_seedBatch).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        _pathProvider.Dispose();
    }

    [Fact]
    public async Task AddAndGetByIdAsync_PersistsAllFieldsAndCalculatesCorrectTotals()
    {
        var now = DateTimeOffset.UtcNow;
        var invoice = Invoice.Create(
            invoiceNumber: "INV-20261022-TEST1",
            userId: Guid.NewGuid(),
            paymentMethod: PaymentMethod.DigitalWalletQr,
            subTotal: Money.FromNpr(45m),
            discount: Money.FromNpr(5m),
            tax: Money.FromNpr(5.2m),
            totalAmount: Money.FromNpr(45.2m),
            customerName: "Sita Sharma",
            prescriptionNumber: "RX-9921",
            notes: "Paid via Fonepay QR",
            now: now);

        var item = InvoiceItem.Create(
            invoiceId: invoice.Id,
            batchId: _seedBatch.Id,
            medicineId: _seedMedicine.Id,
            medicineName: _seedMedicine.Name,
            batchCode: _seedBatch.BatchCode,
            quantity: 3,
            unitPrice: Money.FromNpr(15m));

        invoice.AddItem(item);

        await _repository.AddAsync(invoice);

        var fetched = await _repository.GetByIdAsync(invoice.Id);

        Assert.NotNull(fetched);
        Assert.Equal("INV-20261022-TEST1", fetched.InvoiceNumber);
        Assert.Equal("Sita Sharma", fetched.CustomerName);
        Assert.Equal("RX-9921", fetched.PrescriptionNumber);
        Assert.Equal(PaymentMethod.DigitalWalletQr, fetched.PaymentMethod);
        Assert.Equal(45m, fetched.SubTotal.Amount);
        Assert.Equal(5m, fetched.Discount.Amount);
        Assert.Equal(5.2m, fetched.Tax.Amount);
        Assert.Equal(45.2m, fetched.TotalAmount.Amount);
        Assert.Single(fetched.Items);
        Assert.Equal("Cetirizine 10mg", fetched.Items[0].MedicineName);
        Assert.Equal(3, fetched.Items[0].Quantity);
        Assert.Equal(45m, fetched.Items[0].LineTotal.Amount);
    }

    [Fact]
    public async Task GetByInvoiceNumberAsync_ReturnsMatchingInvoice()
    {
        var now = DateTimeOffset.UtcNow;
        var invoice = Invoice.Create(
            invoiceNumber: "INV-EXACT-MATCH",
            userId: Guid.NewGuid(),
            paymentMethod: PaymentMethod.Cash,
            subTotal: Money.FromNpr(30m),
            discount: Money.Zero,
            tax: Money.Zero,
            totalAmount: Money.FromNpr(30m),
            customerName: "Test Customer",
            prescriptionNumber: null,
            notes: null,
            now: now);

        await _repository.AddAsync(invoice);

        var fetched = await _repository.GetByInvoiceNumberAsync("INV-EXACT-MATCH");

        Assert.NotNull(fetched);
        Assert.Equal(invoice.Id, fetched.Id);
    }

    [Fact]
    public async Task ListRecentAsync_WithSearchTerm_FiltersCorrectly()
    {
        var now = DateTimeOffset.UtcNow;
        var inv1 = Invoice.Create("INV-ALPHA-1", Guid.NewGuid(), PaymentMethod.Cash, Money.FromNpr(10m), Money.Zero, Money.Zero, Money.FromNpr(10m), "Hari Prasad", null, null, now);
        var inv2 = Invoice.Create("INV-BETA-2", Guid.NewGuid(), PaymentMethod.Card, Money.FromNpr(20m), Money.Zero, Money.Zero, Money.FromNpr(20m), "Gita Maya", null, null, now);

        await _repository.AddAsync(inv1);
        await _repository.AddAsync(inv2);

        var results = await _repository.ListRecentAsync(10, "ALPHA");

        Assert.Single(results);
        Assert.Equal("INV-ALPHA-1", results[0].InvoiceNumber);
    }

    [Fact]
    public async Task GetTotalSalesAmountAsync_AggregatesCompletedInvoices()
    {
        var now = DateTimeOffset.UtcNow;
        var inv1 = Invoice.Create("INV-SUM-1", Guid.NewGuid(), PaymentMethod.Cash, Money.FromNpr(100m), Money.Zero, Money.Zero, Money.FromNpr(100m), null, null, null, now);
        var inv2 = Invoice.Create("INV-SUM-2", Guid.NewGuid(), PaymentMethod.Card, Money.FromNpr(150m), Money.Zero, Money.Zero, Money.FromNpr(150m), null, null, null, now);

        await _repository.AddAsync(inv1);
        await _repository.AddAsync(inv2);

        var total = await _repository.GetTotalSalesAmountAsync();

        Assert.Equal(250m, total);
    }
}
