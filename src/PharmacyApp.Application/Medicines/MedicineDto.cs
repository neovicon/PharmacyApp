using PharmacyApp.Domain.Entities;

namespace PharmacyApp.Application.Medicines;

/// <summary>
/// Data transfer object representing a master medicine catalogue item.
/// </summary>
public sealed record MedicineDto(
    Guid Id,
    string Name,
    string GenericName,
    string? Brand,
    string? Strength,
    string DosageForm,
    string DispensingUnit,
    string? Barcode,
    string? Category,
    int ReorderLevel,
    bool RequiresPrescription,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint Version)
{
    /// <summary>
    /// Projects a domain <see cref="Medicine"/> entity to a <see cref="MedicineDto"/>.
    /// </summary>
    public static MedicineDto FromDomain(Medicine medicine)
    {
        ArgumentNullException.ThrowIfNull(medicine);

        return new MedicineDto(
            medicine.Id,
            medicine.Name,
            medicine.GenericName,
            medicine.Brand,
            medicine.Strength,
            medicine.DosageForm,
            medicine.DispensingUnit,
            medicine.Barcode,
            medicine.Category,
            medicine.ReorderLevel,
            medicine.RequiresPrescription,
            medicine.IsActive,
            medicine.CreatedAt,
            medicine.UpdatedAt,
            medicine.Version);
    }
}
