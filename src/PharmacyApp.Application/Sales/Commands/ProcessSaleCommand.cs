using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Common;
using PharmacyApp.Domain.Entities;
using PharmacyApp.Domain.Enums;
using PharmacyApp.Domain.ValueObjects;

namespace PharmacyApp.Application.Sales.Commands;

public sealed record SaleLineItemRequest(
    Guid BatchId,
    int Quantity);

/// <summary>
/// Command to process a sale / dispensing transaction, deducting stock atomically,
/// persisting sales invoices, enforcing strict safety rules (preventing expired or
/// unknown-expiry dispensing, preventing negative stock), and creating immutable audit ledger records.
/// </summary>
public sealed record ProcessSaleCommand(
    IReadOnlyList<SaleLineItemRequest> Items,
    string? CustomerName = null,
    string? PrescriptionNumber = null,
    string? Notes = null,
    PaymentMethod PaymentMethod = PaymentMethod.Cash,
    decimal DiscountAmountNpr = 0m,
    decimal TaxPercentage = 0m);

public sealed class ProcessSaleCommandHandler : ICommandHandler<ProcessSaleCommand, SaleReceiptDto>
{
    private readonly IBatchRepository _batchRepository;
    private readonly IMedicineRepository _medicineRepository;
    private readonly IStockAdjustmentRepository _stockAdjustmentRepository;
    private readonly ITransactionRunner _transactionRunner;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IInvoiceRepository? _invoiceRepository;

    public ProcessSaleCommandHandler(
        IBatchRepository batchRepository,
        IMedicineRepository medicineRepository,
        IStockAdjustmentRepository stockAdjustmentRepository,
        ITransactionRunner transactionRunner,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        IInvoiceRepository? invoiceRepository = null)
    {
        _batchRepository = batchRepository;
        _medicineRepository = medicineRepository;
        _stockAdjustmentRepository = stockAdjustmentRepository;
        _transactionRunner = transactionRunner;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _invoiceRepository = invoiceRepository;
    }

    public async Task<Result<SaleReceiptDto>> HandleAsync(ProcessSaleCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Items is null || command.Items.Count == 0)
        {
            return Result.Failure<SaleReceiptDto>("Sale must contain at least one item.");
        }

        DateTimeOffset now = _dateTimeProvider.UtcNow;
        DateOnly today = DateOnly.FromDateTime(now.Date);
        Guid userId = _currentUserService.IsAuthenticated ? _currentUserService.UserId : Guid.Empty;
        string? cashierName = _currentUserService.IsAuthenticated ? _currentUserService.DisplayName : null;

        return await _transactionRunner.ExecuteInTransactionAsync(async tx =>
        {
            var receiptLines = new List<SaleReceiptLineDto>();
            var invoiceItems = new List<InvoiceItem>();
            decimal subTotal = 0;

            string invoiceNumber = $"INV-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
            Guid invoiceId = Guid.NewGuid();

            foreach (var item in command.Items)
            {
                if (item.Quantity <= 0)
                {
                    return Result.Failure<SaleReceiptDto>("Dispensing quantity must be greater than zero.");
                }

                var batch = await _batchRepository.GetByIdAsync(item.BatchId, cancellationToken, tx);
                if (batch is null)
                {
                    return Result.Failure<SaleReceiptDto>($"Batch with ID '{item.BatchId}' was not found.");
                }

                if (!batch.IsActive)
                {
                    return Result.Failure<SaleReceiptDto>($"Batch '{batch.BatchCode}' is inactive and cannot be dispensed.");
                }

                // Mandatory Safety: Prevent expired batches from being dispensed
                if (batch.IsExpired(today))
                {
                    return Result.Failure<SaleReceiptDto>(
                        $"Safety violation: Batch '{batch.BatchCode}' expired on {batch.ExpiryDate:yyyy-MM-dd} and cannot be dispensed.");
                }

                // Mandatory Safety: Prevent unknown-expiry batches from being treated as safe to dispense
                if (!batch.HasKnownExpiry)
                {
                    return Result.Failure<SaleReceiptDto>(
                        $"Safety violation: Batch '{batch.BatchCode}' has an unknown expiry date and cannot be verified safe to dispense.");
                }

                // Mandatory Safety: Prevent negative stock
                if (batch.QuantityOnHand < item.Quantity)
                {
                    return Result.Failure<SaleReceiptDto>(
                        $"Insufficient stock for batch '{batch.BatchCode}'. Requested: {item.Quantity}, Available: {batch.QuantityOnHand}.");
                }

                var medicine = await _medicineRepository.GetByIdAsync(batch.MedicineId, cancellationToken, tx);
                string medicineName = medicine?.Name ?? "Unknown Medicine";

                // Prescription enforcement warning / guard
                if (medicine is not null && medicine.RequiresPrescription && string.IsNullOrWhiteSpace(command.PrescriptionNumber))
                {
                    return Result.Failure<SaleReceiptDto>(
                        $"Prescription required: Medicine '{medicine.Name}' requires a prescription number.");
                }

                uint initialVersion = batch.Version;
                int prevQty = batch.QuantityOnHand;
                batch.AdjustQuantity(-item.Quantity, now);
                await _batchRepository.UpdateAsync(batch, initialVersion, cancellationToken, tx);

                // Record immutable audit ledger entry
                string reason = string.IsNullOrWhiteSpace(command.PrescriptionNumber)
                    ? $"Sale {invoiceNumber}: Dispensed {item.Quantity} units"
                    : $"Sale {invoiceNumber}: Dispensed {item.Quantity} units against prescription #{command.PrescriptionNumber.Trim()}";

                var adjustment = StockAdjustment.Create(
                    batchId: batch.Id,
                    medicineId: batch.MedicineId,
                    userId: userId == Guid.Empty ? batch.MedicineId : userId,
                    adjustmentType: StockAdjustmentType.SaleDispense,
                    previousQuantity: prevQty,
                    newQuantity: batch.QuantityOnHand,
                    reason: reason,
                    timestampUtc: now);

                await _stockAdjustmentRepository.AddAsync(adjustment, cancellationToken, tx);

                decimal unitPrice = batch.SellingPrice.Amount;
                decimal lineTotal = unitPrice * item.Quantity;
                subTotal += lineTotal;

                receiptLines.Add(new SaleReceiptLineDto(
                    BatchId: batch.Id,
                    MedicineId: batch.MedicineId,
                    MedicineName: medicineName,
                    BatchCode: batch.BatchCode,
                    Quantity: item.Quantity,
                    UnitPriceNpr: unitPrice,
                    LineTotalNpr: lineTotal));

                invoiceItems.Add(InvoiceItem.Create(
                    invoiceId: invoiceId,
                    batchId: batch.Id,
                    medicineId: batch.MedicineId,
                    medicineName: medicineName,
                    batchCode: batch.BatchCode,
                    quantity: item.Quantity,
                    unitPrice: batch.SellingPrice));
            }

            // Calculate discount and taxes
            decimal discount = Math.Max(0, Math.Min(command.DiscountAmountNpr, subTotal));
            decimal taxableAmount = subTotal - discount;
            decimal tax = command.TaxPercentage > 0
                ? Math.Round(taxableAmount * (command.TaxPercentage / 100m), 2)
                : 0m;
            decimal grandTotal = taxableAmount + tax;

            // Persist invoice to SQLite if repository available
            if (_invoiceRepository is not null)
            {
                var invoice = Invoice.Create(
                    invoiceNumber: invoiceNumber,
                    userId: userId == Guid.Empty ? invoiceId : userId,
                    paymentMethod: command.PaymentMethod,
                    subTotal: Money.RoundToPaisa(subTotal),
                    discount: Money.RoundToPaisa(discount),
                    tax: Money.RoundToPaisa(tax),
                    totalAmount: Money.RoundToPaisa(grandTotal),
                    customerName: command.CustomerName,
                    prescriptionNumber: command.PrescriptionNumber,
                    notes: command.Notes,
                    now: now);

                invoice.AddItems(invoiceItems);
                await _invoiceRepository.AddAsync(invoice, cancellationToken, tx);
            }

            var receipt = new SaleReceiptDto(
                TransactionId: invoiceId,
                InvoiceNumber: invoiceNumber,
                TimestampUtc: now,
                CustomerName: command.CustomerName?.Trim(),
                PrescriptionNumber: command.PrescriptionNumber?.Trim(),
                PaymentMethod: command.PaymentMethod,
                Lines: receiptLines,
                SubTotalNpr: subTotal,
                DiscountNpr: discount,
                TaxNpr: tax,
                TotalAmountNpr: grandTotal,
                CashierName: cashierName);

            return Result.Success(receipt);
        }, cancellationToken);
    }
}
