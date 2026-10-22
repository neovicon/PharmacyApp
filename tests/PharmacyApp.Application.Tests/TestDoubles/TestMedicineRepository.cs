using System.Data.Common;
using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Exceptions;
using PharmacyApp.Domain.Entities;

namespace PharmacyApp.Application.Tests.TestDoubles;

/// <summary>
/// In-memory test double for <see cref="IMedicineRepository"/>.
/// Faithfully reproduces persistence isolation, uniqueness constraints, and optimistic concurrency checking.
/// </summary>
public sealed class TestMedicineRepository : IMedicineRepository
{
    private readonly List<Medicine> _medicines = new();

    public IReadOnlyList<Medicine> Medicines => _medicines.Select(Clone).ToList();

    public Task<Medicine?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        var match = _medicines.FirstOrDefault(m => m.Id == id);
        return Task.FromResult(match != null ? Clone(match) : null);
    }

    public Task<Medicine?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return Task.FromResult<Medicine?>(null);

        var match = _medicines.FirstOrDefault(m => string.Equals(m.Barcode, barcode.Trim(), StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(match != null ? Clone(match) : null);
    }

    public Task<IReadOnlyList<Medicine>> ListAsync(bool? isActive = null, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        var query = _medicines.AsEnumerable();
        if (isActive.HasValue)
        {
            query = query.Where(m => m.IsActive == isActive.Value);
        }

        var result = query
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .Select(Clone)
            .ToList();

        return Task.FromResult<IReadOnlyList<Medicine>>(result);
    }

    public Task<IReadOnlyList<Medicine>> SearchAsync(string query, bool? isActive = null, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return ListAsync(isActive, cancellationToken, transaction);
        }

        string trimmed = query.Trim();
        var matches = _medicines
            .Where(m => !isActive.HasValue || m.IsActive == isActive.Value)
            .Where(m =>
                m.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                m.GenericName.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                (m.Brand != null && m.Brand.Contains(trimmed, StringComparison.OrdinalIgnoreCase)) ||
                (m.Category != null && m.Category.Contains(trimmed, StringComparison.OrdinalIgnoreCase)) ||
                (m.Barcode != null && string.Equals(m.Barcode, trimmed, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .Select(Clone)
            .ToList();

        return Task.FromResult<IReadOnlyList<Medicine>>(matches);
    }

    public Task AddAsync(Medicine medicine, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(medicine);

        if (!string.IsNullOrWhiteSpace(medicine.Barcode))
        {
            if (_medicines.Any(m => string.Equals(m.Barcode, medicine.Barcode, StringComparison.OrdinalIgnoreCase)))
            {
                throw new DuplicateBarcodeException(medicine.Barcode);
            }
        }

        if (_medicines.Any(m => m.Id == medicine.Id))
        {
            throw new InvalidOperationException($"Medicine with ID '{medicine.Id}' already exists.");
        }

        _medicines.Add(Clone(medicine));
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Medicine medicine, uint? expectedVersion = null, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(medicine);

        if (!string.IsNullOrWhiteSpace(medicine.Barcode))
        {
            if (_medicines.Any(m => m.Id != medicine.Id && string.Equals(m.Barcode, medicine.Barcode, StringComparison.OrdinalIgnoreCase)))
            {
                throw new DuplicateBarcodeException(medicine.Barcode);
            }
        }

        var index = _medicines.FindIndex(m => m.Id == medicine.Id);
        if (index < 0)
        {
            throw new EntityNotFoundException($"Medicine with ID '{medicine.Id}' was not found.");
        }

        var existing = _medicines[index];
        uint checkVersion = expectedVersion ?? (medicine.Version > 1 ? medicine.Version - 1 : 1);

        if (existing.Version != checkVersion)
        {
            throw new ConcurrencyException(
                $"Optimistic concurrency violation: Medicine with ID '{medicine.Id}' was modified by another operation. Expected version {checkVersion}, but current version is {existing.Version}.");
        }

        _medicines[index] = Clone(medicine);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsBarcodeAsync(string barcode, Guid? excludeMedicineId = null, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return Task.FromResult(false);

        bool exists = _medicines.Any(m =>
            (!excludeMedicineId.HasValue || m.Id != excludeMedicineId.Value) &&
            string.Equals(m.Barcode, barcode.Trim(), StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(exists);
    }

    private static Medicine Clone(Medicine m) => Medicine.Load(
        m.Id,
        m.Name,
        m.GenericName,
        m.Brand,
        m.Strength,
        m.DosageForm,
        m.DispensingUnit,
        m.Barcode,
        m.Category,
        m.ReorderLevel,
        m.RequiresPrescription,
        m.IsActive,
        m.CreatedAt,
        m.UpdatedAt,
        m.Version);
}
