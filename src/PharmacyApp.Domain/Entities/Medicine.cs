using PharmacyApp.Domain.Common;

namespace PharmacyApp.Domain.Entities;

/// <summary>
/// Represents a master pharmaceutical product in the catalogue.
/// Distinct from a physical batch/lot.
/// </summary>
public sealed class Medicine : EntityBase
{
    public string Name { get; private set; }
    public string GenericName { get; private set; }
    public string? Brand { get; private set; }
    public string? Strength { get; private set; }
    public string DosageForm { get; private set; }
    public string DispensingUnit { get; private set; }
    public string? Barcode { get; private set; }
    public string? Category { get; private set; }
    public int ReorderLevel { get; private set; }
    public bool RequiresPrescription { get; private set; }
    public bool IsActive { get; private set; }

    private Medicine(
        Guid id,
        string name,
        string genericName,
        string? brand,
        string? strength,
        string dosageForm,
        string dispensingUnit,
        string? barcode,
        string? category,
        int reorderLevel,
        bool requiresPrescription,
        bool isActive,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        uint version)
    {
        Id = id;
        Name = name;
        GenericName = genericName;
        Brand = brand;
        Strength = strength;
        DosageForm = dosageForm;
        DispensingUnit = dispensingUnit;
        Barcode = barcode;
        Category = category;
        ReorderLevel = reorderLevel;
        RequiresPrescription = requiresPrescription;
        IsActive = isActive;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        Version = version;
    }

    /// <summary>
    /// Factory method for creating a new Medicine catalogue item.
    /// Reorder level must be explicitly supplied by the caller and validated as non-negative;
    /// it is never silently defaulted to an unapproved placeholder.
    /// </summary>
    public static Medicine Create(
        string name,
        string genericName,
        string dosageForm,
        string dispensingUnit,
        int reorderLevel,
        string? brand = null,
        string? strength = null,
        string? barcode = null,
        string? category = null,
        bool requiresPrescription = false,
        DateTimeOffset now = default)
    {
        ValidateCoreAttributes(name, genericName, dosageForm, dispensingUnit, reorderLevel);

        DateTimeOffset timestamp = now == default ? DateTimeOffset.UtcNow : now;

        return new Medicine(
            id: Guid.NewGuid(),
            name: name.Trim(),
            genericName: genericName.Trim(),
            brand: NormalizeOptionalString(brand),
            strength: NormalizeOptionalString(strength),
            dosageForm: dosageForm.Trim(),
            dispensingUnit: dispensingUnit.Trim(),
            barcode: NormalizeBarcode(barcode),
            category: NormalizeOptionalString(category),
            reorderLevel: reorderLevel,
            requiresPrescription: requiresPrescription,
            isActive: true,
            createdAt: timestamp,
            updatedAt: timestamp,
            version: 1);
    }

    /// <summary>
    /// Factory method used to reconstitute an existing Medicine from persistence storage.
    /// </summary>
    public static Medicine Load(
        Guid id,
        string name,
        string genericName,
        string? brand,
        string? strength,
        string dosageForm,
        string dispensingUnit,
        string? barcode,
        string? category,
        int reorderLevel,
        bool requiresPrescription,
        bool isActive,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        uint version)
    {
        ValidateCoreAttributes(name, genericName, dosageForm, dispensingUnit, reorderLevel);

        return new Medicine(
            id,
            name.Trim(),
            genericName.Trim(),
            NormalizeOptionalString(brand),
            NormalizeOptionalString(strength),
            dosageForm.Trim(),
            dispensingUnit.Trim(),
            NormalizeBarcode(barcode),
            NormalizeOptionalString(category),
            reorderLevel,
            requiresPrescription,
            isActive,
            createdAt,
            updatedAt,
            version);
    }

    /// <summary>
    /// Updates catalogue details for this medicine, recording the update timestamp and incrementing the version.
    /// </summary>
    public void UpdateDetails(
        string name,
        string genericName,
        string dosageForm,
        string dispensingUnit,
        int reorderLevel,
        string? brand,
        string? strength,
        string? barcode,
        string? category,
        bool requiresPrescription,
        DateTimeOffset now)
    {
        ValidateCoreAttributes(name, genericName, dosageForm, dispensingUnit, reorderLevel);

        Name = name.Trim();
        GenericName = genericName.Trim();
        DosageForm = dosageForm.Trim();
        DispensingUnit = dispensingUnit.Trim();
        Brand = NormalizeOptionalString(brand);
        Strength = NormalizeOptionalString(strength);
        Barcode = NormalizeBarcode(barcode);
        Category = NormalizeOptionalString(category);
        ReorderLevel = reorderLevel;
        RequiresPrescription = requiresPrescription;
        UpdatedAt = now;
        Version++;
    }

    /// <summary>
    /// Deactivates this medicine (soft delete). Historical records and batches remain accessible.
    /// </summary>
    public void Deactivate(DateTimeOffset now)
    {
        IsActive = false;
        UpdatedAt = now;
        Version++;
    }

    /// <summary>
    /// Reactivates this medicine, allowing new batches and dispensing.
    /// </summary>
    public void Reactivate(DateTimeOffset now)
    {
        IsActive = true;
        UpdatedAt = now;
        Version++;
    }

    private static void ValidateCoreAttributes(
        string name,
        string genericName,
        string dosageForm,
        string dispensingUnit,
        int reorderLevel)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Medicine commercial name cannot be empty.", nameof(name));

        if (string.IsNullOrWhiteSpace(genericName))
            throw new ArgumentException("Generic / active ingredient name cannot be empty.", nameof(genericName));

        if (string.IsNullOrWhiteSpace(dosageForm))
            throw new ArgumentException("Dosage form cannot be empty.", nameof(dosageForm));

        if (string.IsNullOrWhiteSpace(dispensingUnit))
            throw new ArgumentException("Dispensing unit cannot be empty.", nameof(dispensingUnit));

        if (reorderLevel < 0)
            throw new ArgumentOutOfRangeException(nameof(reorderLevel), reorderLevel, "Reorder level cannot be negative.");
    }

    private static string? NormalizeOptionalString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim();
    }

    private static string? NormalizeBarcode(string? barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return null;

        return barcode.Trim();
    }
}
