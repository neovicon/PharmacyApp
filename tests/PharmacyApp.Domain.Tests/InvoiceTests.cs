using PharmacyApp.Domain.Entities;
using PharmacyApp.Domain.Enums;
using PharmacyApp.Domain.ValueObjects;
using Xunit;

namespace PharmacyApp.Domain.Tests;

public class InvoiceTests
{
    private readonly DateTimeOffset _now = new(2026, 10, 22, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Create_ValidInputs_InitializesCorrectly()
    {
        var invoice = Invoice.Create(
            invoiceNumber: "INV-20261022-001",
            userId: Guid.NewGuid(),
            paymentMethod: PaymentMethod.Cash,
            subTotal: Money.FromNpr(100m),
            discount: Money.FromNpr(10m),
            tax: Money.FromNpr(11.7m),
            totalAmount: Money.FromNpr(101.7m),
            customerName: "Ram Bahadur",
            prescriptionNumber: "RX-12345",
            notes: "Routine dispensing",
            now: _now);

        Assert.NotEqual(Guid.Empty, invoice.Id);
        Assert.Equal("INV-20261022-001", invoice.InvoiceNumber);
        Assert.Equal("Ram Bahadur", invoice.CustomerName);
        Assert.Equal("RX-12345", invoice.PrescriptionNumber);
        Assert.Equal(PaymentMethod.Cash, invoice.PaymentMethod);
        Assert.Equal(100m, invoice.SubTotal.Amount);
        Assert.Equal(10m, invoice.Discount.Amount);
        Assert.Equal(11.7m, invoice.Tax.Amount);
        Assert.Equal(101.7m, invoice.TotalAmount.Amount);
        Assert.Equal(InvoiceStatus.Completed, invoice.Status);
        Assert.Equal(1u, invoice.Version);
        Assert.Equal(_now, invoice.CreatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_EmptyInvoiceNumber_ThrowsArgumentException(string? invalidNum)
    {
        Assert.Throws<ArgumentException>(() => Invoice.Create(
            invoiceNumber: invalidNum!,
            userId: Guid.NewGuid(),
            paymentMethod: PaymentMethod.Cash,
            subTotal: Money.FromNpr(50m),
            discount: Money.Zero,
            tax: Money.Zero,
            totalAmount: Money.FromNpr(50m),
            customerName: null,
            prescriptionNumber: null,
            notes: null,
            now: _now));
    }

    [Fact]
    public void Create_EmptyUserId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Invoice.Create(
            invoiceNumber: "INV-001",
            userId: Guid.Empty,
            paymentMethod: PaymentMethod.Cash,
            subTotal: Money.FromNpr(50m),
            discount: Money.Zero,
            tax: Money.Zero,
            totalAmount: Money.FromNpr(50m),
            customerName: null,
            prescriptionNumber: null,
            notes: null,
            now: _now));
    }

    [Fact]
    public void AddItems_AddsToReadOnlyItemsCollection()
    {
        var invoice = Invoice.Create(
            invoiceNumber: "INV-002",
            userId: Guid.NewGuid(),
            paymentMethod: PaymentMethod.Card,
            subTotal: Money.FromNpr(60m),
            discount: Money.Zero,
            tax: Money.Zero,
            totalAmount: Money.FromNpr(60m),
            customerName: null,
            prescriptionNumber: null,
            notes: null,
            now: _now);

        var item = InvoiceItem.Create(
            invoiceId: invoice.Id,
            batchId: Guid.NewGuid(),
            medicineId: Guid.NewGuid(),
            medicineName: "Paracetamol 500mg",
            batchCode: "B-001",
            quantity: 3,
            unitPrice: Money.FromNpr(20m));

        invoice.AddItem(item);

        Assert.Single(invoice.Items);
        Assert.Equal("Paracetamol 500mg", invoice.Items[0].MedicineName);
        Assert.Equal(60m, invoice.Items[0].LineTotal.Amount);
    }

    [Fact]
    public void MarkCancelled_UpdatesStatusAndVersion()
    {
        var invoice = Invoice.Create(
            invoiceNumber: "INV-003",
            userId: Guid.NewGuid(),
            paymentMethod: PaymentMethod.Cash,
            subTotal: Money.FromNpr(50m),
            discount: Money.Zero,
            tax: Money.Zero,
            totalAmount: Money.FromNpr(50m),
            customerName: null,
            prescriptionNumber: null,
            notes: "Initial",
            now: _now);

        DateTimeOffset cancelTime = _now.AddMinutes(5);
        invoice.MarkCancelled("Customer changed mind", cancelTime);

        Assert.Equal(InvoiceStatus.Cancelled, invoice.Status);
        Assert.Contains("Customer changed mind", invoice.Notes);
        Assert.Equal(cancelTime, invoice.UpdatedAt);
        Assert.Equal(2u, invoice.Version);
    }
}
