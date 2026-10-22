using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Common;
using PharmacyApp.Domain.Entities;
using PharmacyApp.Domain.Enums;

namespace PharmacyApp.Application.Inventory.Commands;

/// <summary>
/// Command to adjust batch inventory quantity (e.g. damages, expiry write-off, audit corrections)
/// with mandatory reason recording, role permission enforcement, and optimistic concurrency version checking.
/// </summary>
public sealed record AdjustStockCommand(
    Guid BatchId,
    int NewQuantity,
    StockAdjustmentType AdjustmentType,
    string Reason,
    uint ExpectedVersion);

public sealed class AdjustStockCommandHandler : ICommandHandler<AdjustStockCommand>
{
    private readonly IBatchRepository _batchRepository;
    private readonly IStockAdjustmentRepository _stockAdjustmentRepository;
    private readonly ITransactionRunner _transactionRunner;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public AdjustStockCommandHandler(
        IBatchRepository batchRepository,
        IStockAdjustmentRepository stockAdjustmentRepository,
        ITransactionRunner transactionRunner,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider)
    {
        _batchRepository = batchRepository;
        _stockAdjustmentRepository = stockAdjustmentRepository;
        _transactionRunner = transactionRunner;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result> HandleAsync(AdjustStockCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.BatchId == Guid.Empty)
            return Result.Failure("Batch ID cannot be empty.");

        if (command.NewQuantity < 0)
            return Result.Failure("New quantity cannot be negative.");

        if (string.IsNullOrWhiteSpace(command.Reason))
            return Result.Failure("Adjustment reason is mandatory for compliance auditing.");

        if (command.ExpectedVersion == 0)
            return Result.Failure("Expected version must be greater than zero.");

        // Role authorization check: Only Admin or Pharmacist can make manual adjustments
        if (_currentUserService.IsAuthenticated &&
            _currentUserService.Role != UserRole.Admin &&
            _currentUserService.Role != UserRole.Pharmacist)
        {
            return Result.Failure("Access denied: only Pharmacists and Administrators are permitted to adjust stock.");
        }

        DateTimeOffset now = _dateTimeProvider.UtcNow;
        Guid userId = _currentUserService.IsAuthenticated ? _currentUserService.UserId : Guid.Empty;

        return await _transactionRunner.ExecuteInTransactionAsync(async tx =>
        {
            var batch = await _batchRepository.GetByIdAsync(command.BatchId, cancellationToken, tx);
            if (batch is null)
                return Result.Failure($"Batch with ID '{command.BatchId}' was not found.");

            if (batch.Version != command.ExpectedVersion)
            {
                return Result.Failure(
                    $"Optimistic concurrency violation: Batch was modified by another operation. Expected version {command.ExpectedVersion}, but current database version is {batch.Version}.");
            }

            int prevQty = batch.QuantityOnHand;
            if (prevQty == command.NewQuantity)
            {
                return Result.Failure("New quantity must be different from current quantity.");
            }

            batch.SetQuantity(command.NewQuantity, now);
            await _batchRepository.UpdateAsync(batch, expectedVersion: command.ExpectedVersion, cancellationToken, tx);

            var adjustment = StockAdjustment.Create(
                batchId: batch.Id,
                medicineId: batch.MedicineId,
                userId: userId == Guid.Empty ? batch.MedicineId : userId,
                adjustmentType: command.AdjustmentType,
                previousQuantity: prevQty,
                newQuantity: command.NewQuantity,
                reason: command.Reason.Trim(),
                timestampUtc: now);

            await _stockAdjustmentRepository.AddAsync(adjustment, cancellationToken, tx);
            return Result.Success();
        }, cancellationToken);
    }
}
