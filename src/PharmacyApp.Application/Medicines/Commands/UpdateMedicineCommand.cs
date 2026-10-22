using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Common;
using PharmacyApp.Application.Exceptions;
using PharmacyApp.Domain.Entities;

namespace PharmacyApp.Application.Medicines.Commands;

/// <summary>
/// Command to update catalogue details of an existing medicine with optimistic concurrency version check.
/// </summary>
public sealed record UpdateMedicineCommand(
    Guid Id,
    string Name,
    string GenericName,
    string DosageForm,
    string DispensingUnit,
    int ReorderLevel,
    uint ExpectedVersion,
    string? Brand = null,
    string? Strength = null,
    string? Barcode = null,
    string? Category = null,
    bool RequiresPrescription = false,
    bool? IsActive = null);

/// <summary>
/// Handles updating an existing medicine, enforcing validation, barcode uniqueness,
/// and optimistic concurrency protection.
/// </summary>
public sealed class UpdateMedicineCommandHandler : ICommandHandler<UpdateMedicineCommand>
{
    private readonly IMedicineRepository _medicineRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IMedicineValidator _validator;

    public UpdateMedicineCommandHandler(
        IMedicineRepository medicineRepository,
        IDateTimeProvider dateTimeProvider,
        IMedicineValidator validator)
    {
        _medicineRepository = medicineRepository;
        _dateTimeProvider = dateTimeProvider;
        _validator = validator;
    }

    public async Task<Result> HandleAsync(UpdateMedicineCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = _validator.ValidateUpdate(command);
        if (validation.IsFailure)
        {
            return Result.Failure(validation.Error!);
        }

        var medicine = await _medicineRepository.GetByIdAsync(command.Id, cancellationToken);
        if (medicine == null)
        {
            return Result.Failure($"Medicine with ID '{command.Id}' was not found.");
        }

        // Fast-fail concurrency check in application layer
        if (medicine.Version != command.ExpectedVersion)
        {
            return Result.Failure(
                $"Optimistic concurrency violation: Medicine with ID '{command.Id}' was modified by another operation. Expected version {command.ExpectedVersion}, but current database version is {medicine.Version}.");
        }

        // Verify barcode uniqueness against other medicines if specified
        if (!string.IsNullOrWhiteSpace(command.Barcode))
        {
            bool duplicateBarcode = await _medicineRepository.ExistsBarcodeAsync(
                command.Barcode.Trim(),
                excludeMedicineId: command.Id,
                cancellationToken: cancellationToken);

            if (duplicateBarcode)
            {
                return Result.Failure($"A medicine with barcode '{command.Barcode.Trim()}' already exists.");
            }
        }

        DateTimeOffset now = _dateTimeProvider.UtcNow;

        try
        {
            medicine.UpdateDetails(
                name: command.Name,
                genericName: command.GenericName,
                dosageForm: command.DosageForm,
                dispensingUnit: command.DispensingUnit,
                reorderLevel: command.ReorderLevel,
                brand: command.Brand,
                strength: command.Strength,
                barcode: command.Barcode,
                category: command.Category,
                requiresPrescription: command.RequiresPrescription,
                now: now);

            if (command.IsActive.HasValue && command.IsActive.Value != medicine.IsActive)
            {
                if (command.IsActive.Value)
                    medicine.Reactivate(now);
                else
                    medicine.Deactivate(now);
            }

            // Enforce version check at persistence layer
            await _medicineRepository.UpdateAsync(medicine, expectedVersion: command.ExpectedVersion, cancellationToken: cancellationToken);
            return Result.Success();
        }
        catch (ConcurrencyException ex)
        {
            return Result.Failure(ex.Message);
        }
        catch (DuplicateBarcodeException ex)
        {
            return Result.Failure(ex.Message);
        }
        catch (EntityNotFoundException ex)
        {
            return Result.Failure(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure(ex.Message);
        }
    }
}
