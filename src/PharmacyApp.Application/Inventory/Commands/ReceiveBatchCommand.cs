using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Common;
using PharmacyApp.Domain.Entities;
using PharmacyApp.Domain.Enums;
using PharmacyApp.Domain.ValueObjects;

namespace PharmacyApp.Application.Inventory.Commands;

/// <summary>
/// Command to receive new stock inventory for a medicine, either creating a new batch
/// or adding to an existing batch, while generating an immutable audit ledger entry.
/// </summary>
public sealed record ReceiveBatchCommand(
    Guid MedicineId,
    string BatchCode,
    int Quantity,
    decimal PurchaseCostNpr,
    decimal SellingPriceNpr,
    string? ManufacturerBatchNumber = null,
    DateOnly? ExpiryDate = null,
    string? Location = null,
    string? Reason = null,
    bool IsPurchaseOrder = false);

public sealed class ReceiveBatchCommandHandler : ICommandHandler<ReceiveBatchCommand, Guid>
{
    private readonly IMedicineRepository _medicineRepository;
    private readonly IBatchRepository _batchRepository;
    private readonly IStockAdjustmentRepository _stockAdjustmentRepository;
    private readonly ITransactionRunner _transactionRunner;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ReceiveBatchCommandHandler(
        IMedicineRepository medicineRepository,
        IBatchRepository batchRepository,
        IStockAdjustmentRepository stockAdjustmentRepository,
        ITransactionRunner transactionRunner,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider)
    {
        _medicineRepository = medicineRepository;
        _batchRepository = batchRepository;
        _stockAdjustmentRepository = stockAdjustmentRepository;
        _transactionRunner = transactionRunner;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<Guid>> HandleAsync(ReceiveBatchCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.MedicineId == Guid.Empty)
            return Result.Failure<Guid>("Medicine ID cannot be empty.");

        if (string.IsNullOrWhiteSpace(command.BatchCode))
            return Result.Failure<Guid>("Batch code is required.");

        if (command.Quantity <= 0)
            return Result.Failure<Guid>("Received quantity must be greater than zero.");

        if (command.PurchaseCostNpr < 0)
            return Result.Failure<Guid>("Purchase cost cannot be negative.");

        if (command.SellingPriceNpr < 0)
            return Result.Failure<Guid>("Selling price cannot be negative.");

        var medicine = await _medicineRepository.GetByIdAsync(command.MedicineId, cancellationToken);
        if (medicine is null)
            return Result.Failure<Guid>($"Medicine with ID '{command.MedicineId}' was not found.");

        if (!medicine.IsActive)
            return Result.Failure<Guid>($"Cannot receive stock for inactive medicine '{medicine.Name}'.");

        string normalizedBatchCode = command.BatchCode.Trim().ToUpperInvariant();
        DateTimeOffset now = _dateTimeProvider.UtcNow;
        Guid userId = _currentUserService.IsAuthenticated ? _currentUserService.UserId : Guid.Empty;

        if (userId == Guid.Empty)
        {
            userId = command.MedicineId;
        }

        var purchaseCost = Money.FromNpr(decimal.Round(command.PurchaseCostNpr, 2));
        var sellingPrice = Money.FromNpr(decimal.Round(command.SellingPriceNpr, 2));
        var adjustmentType = command.IsPurchaseOrder
            ? StockAdjustmentType.PurchaseReceipt
            : StockAdjustmentType.InitialStock;

        string reason = string.IsNullOrWhiteSpace(command.Reason)
            ? (command.IsPurchaseOrder ? $"Purchase order receipt of {command.Quantity} units" : $"Initial stock intake of {command.Quantity} units")
            : command.Reason.Trim();

        return await _transactionRunner.ExecuteInTransactionAsync(async tx =>
        {
            var existingBatch = await _batchRepository.GetByBatchCodeAsync(normalizedBatchCode, cancellationToken, tx);
            if (existingBatch is not null)
            {
                if (existingBatch.MedicineId != command.MedicineId)
                {
                    return Result.Failure<Guid>(
                        $"Batch code '{normalizedBatchCode}' already belongs to a different medicine.");
                }

                uint initialVersion = existingBatch.Version;
                int prevQty = existingBatch.QuantityOnHand;
                existingBatch.AdjustQuantity(command.Quantity, now);
                existingBatch.UpdatePricing(purchaseCost, sellingPrice, now);
                if (!string.IsNullOrWhiteSpace(command.Location))
                {
                    existingBatch.UpdateLocation(command.Location, now);
                }

                await _batchRepository.UpdateAsync(existingBatch, initialVersion, cancellationToken, tx);

                var adjustment = StockAdjustment.Create(
                    batchId: existingBatch.Id,
                    medicineId: existingBatch.MedicineId,
                    userId: userId,
                    adjustmentType: adjustmentType,
                    previousQuantity: prevQty,
                    newQuantity: existingBatch.QuantityOnHand,
                    reason: reason,
                    timestampUtc: now);

                await _stockAdjustmentRepository.AddAsync(adjustment, cancellationToken, tx);
                return Result.Success(existingBatch.Id);
            }
            else
            {
                var newBatch = Batch.Create(
                    medicineId: command.MedicineId,
                    batchCode: normalizedBatchCode,
                    quantityOnHand: command.Quantity,
                    purchaseCost: purchaseCost,
                    sellingPrice: sellingPrice,
                    manufacturerBatchNumber: command.ManufacturerBatchNumber,
                    expiryDate: command.ExpiryDate,
                    location: command.Location,
                    now: now);

                await _batchRepository.AddAsync(newBatch, cancellationToken, tx);

                var adjustment = StockAdjustment.Create(
                    batchId: newBatch.Id,
                    medicineId: newBatch.MedicineId,
                    userId: userId,
                    adjustmentType: adjustmentType,
                    previousQuantity: 0,
                    newQuantity: command.Quantity,
                    reason: reason,
                    timestampUtc: now);

                await _stockAdjustmentRepository.AddAsync(adjustment, cancellationToken, tx);
                return Result.Success(newBatch.Id);
            }
        }, cancellationToken);
    }
}
