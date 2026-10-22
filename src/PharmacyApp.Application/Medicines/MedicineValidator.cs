using PharmacyApp.Application.Common;
using PharmacyApp.Application.Medicines.Commands;

namespace PharmacyApp.Application.Medicines;

/// <summary>
/// Default implementation of medicine command validation.
/// Enforces required fields, non-negative reorder levels, and sensible length limits.
/// </summary>
public sealed class MedicineValidator : IMedicineValidator
{
    public const int MaxNameLength = 200;
    public const int MaxGenericNameLength = 200;
    public const int MaxDosageFormLength = 100;
    public const int MaxDispensingUnitLength = 100;
    public const int MaxBrandLength = 150;
    public const int MaxStrengthLength = 100;
    public const int MaxBarcodeLength = 100;
    public const int MaxCategoryLength = 100;

    public Result ValidateCreate(CreateMedicineCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return ValidateCore(
            command.Name,
            command.GenericName,
            command.DosageForm,
            command.DispensingUnit,
            command.ReorderLevel,
            command.Brand,
            command.Strength,
            command.Barcode,
            command.Category);
    }

    public Result ValidateUpdate(UpdateMedicineCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Id == Guid.Empty)
        {
            return Result.Failure("Medicine ID cannot be empty.");
        }

        if (command.ExpectedVersion == 0)
        {
            return Result.Failure("Expected version must be greater than zero.");
        }

        return ValidateCore(
            command.Name,
            command.GenericName,
            command.DosageForm,
            command.DispensingUnit,
            command.ReorderLevel,
            command.Brand,
            command.Strength,
            command.Barcode,
            command.Category);
    }

    private static Result ValidateCore(
        string name,
        string genericName,
        string dosageForm,
        string dispensingUnit,
        int reorderLevel,
        string? brand,
        string? strength,
        string? barcode,
        string? category)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure("Medicine commercial name is required.");

        if (name.Trim().Length > MaxNameLength)
            return Result.Failure($"Medicine commercial name cannot exceed {MaxNameLength} characters.");

        if (string.IsNullOrWhiteSpace(genericName))
            return Result.Failure("Generic / active ingredient name is required.");

        if (genericName.Trim().Length > MaxGenericNameLength)
            return Result.Failure($"Generic name cannot exceed {MaxGenericNameLength} characters.");

        if (string.IsNullOrWhiteSpace(dosageForm))
            return Result.Failure("Dosage form is required.");

        if (dosageForm.Trim().Length > MaxDosageFormLength)
            return Result.Failure($"Dosage form cannot exceed {MaxDosageFormLength} characters.");

        if (string.IsNullOrWhiteSpace(dispensingUnit))
            return Result.Failure("Dispensing unit is required.");

        if (dispensingUnit.Trim().Length > MaxDispensingUnitLength)
            return Result.Failure($"Dispensing unit cannot exceed {MaxDispensingUnitLength} characters.");

        if (reorderLevel < 0)
            return Result.Failure("Reorder level cannot be negative.");

        if (!string.IsNullOrWhiteSpace(brand) && brand.Trim().Length > MaxBrandLength)
            return Result.Failure($"Brand cannot exceed {MaxBrandLength} characters.");

        if (!string.IsNullOrWhiteSpace(strength) && strength.Trim().Length > MaxStrengthLength)
            return Result.Failure($"Strength cannot exceed {MaxStrengthLength} characters.");

        if (!string.IsNullOrWhiteSpace(barcode) && barcode.Trim().Length > MaxBarcodeLength)
            return Result.Failure($"Barcode cannot exceed {MaxBarcodeLength} characters.");

        if (!string.IsNullOrWhiteSpace(category) && category.Trim().Length > MaxCategoryLength)
            return Result.Failure($"Category cannot exceed {MaxCategoryLength} characters.");

        return Result.Success();
    }
}
