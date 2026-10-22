using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Common;
using PharmacyApp.Application.Exceptions;
using PharmacyApp.Domain.Entities;

namespace PharmacyApp.Application.Medicines.Commands;

/// <summary>
/// Command to create a new medicine catalogue entry.
/// </summary>
public sealed record CreateMedicineCommand(
    string Name,
    string GenericName,
    string DosageForm,
    string DispensingUnit,
    int ReorderLevel,
    string? Brand = null,
    string? Strength = null,
    string? Barcode = null,
    string? Category = null,
    bool RequiresPrescription = false);

/// <summary>
/// Handles creation of a new medicine catalogue item, enforcing validation,
/// barcode uniqueness, and domain invariants.
/// </summary>
public sealed class CreateMedicineCommandHandler : ICommandHandler<CreateMedicineCommand, Guid>
{
    private readonly IMedicineRepository _medicineRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IMedicineValidator _validator;

    public CreateMedicineCommandHandler(
        IMedicineRepository medicineRepository,
        IDateTimeProvider dateTimeProvider,
        IMedicineValidator validator)
    {
        _medicineRepository = medicineRepository;
        _dateTimeProvider = dateTimeProvider;
        _validator = validator;
    }

    public async Task<Result<Guid>> HandleAsync(CreateMedicineCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = _validator.ValidateCreate(command);
        if (validation.IsFailure)
        {
            return Result.Failure<Guid>(validation.Error!);
        }

        if (!string.IsNullOrWhiteSpace(command.Barcode))
        {
            bool exists = await _medicineRepository.ExistsBarcodeAsync(command.Barcode.Trim(), cancellationToken: cancellationToken);
            if (exists)
            {
                return Result.Failure<Guid>($"A medicine with barcode '{command.Barcode.Trim()}' already exists.");
            }
        }

        DateTimeOffset now = _dateTimeProvider.UtcNow;

        try
        {
            var medicine = Medicine.Create(
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

            await _medicineRepository.AddAsync(medicine, cancellationToken);
            return Result.Success(medicine.Id);
        }
        catch (DuplicateBarcodeException ex)
        {
            return Result.Failure<Guid>(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<Guid>(ex.Message);
        }
    }
}
