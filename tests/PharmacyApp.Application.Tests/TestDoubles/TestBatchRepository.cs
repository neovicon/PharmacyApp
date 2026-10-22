using System.Data.Common;
using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Exceptions;
using PharmacyApp.Domain.Entities;

namespace PharmacyApp.Application.Tests.TestDoubles;

public sealed class TestBatchRepository : IBatchRepository
{
    private readonly List<Batch> _batches = new();

    public IReadOnlyList<Batch> Batches => _batches.Select(Clone).ToList();

    public Task<Batch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        var match = _batches.FirstOrDefault(b => b.Id == id);
        return Task.FromResult(match != null ? Clone(match) : null);
    }

    public Task<Batch?> GetByBatchCodeAsync(string batchCode, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        if (string.IsNullOrWhiteSpace(batchCode))
            return Task.FromResult<Batch?>(null);

        var match = _batches.FirstOrDefault(b => string.Equals(b.BatchCode, batchCode.Trim(), StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(match != null ? Clone(match) : null);
    }

    public Task<IReadOnlyList<Batch>> GetByMedicineIdAsync(Guid medicineId, bool? isActive = null, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        var query = _batches.Where(b => b.MedicineId == medicineId);
        if (isActive.HasValue)
        {
            query = query.Where(b => b.IsActive == isActive.Value);
        }

        var result = query
            .OrderBy(b => b.ExpiryDate == null)
            .ThenBy(b => b.ExpiryDate)
            .Select(Clone)
            .ToList();

        return Task.FromResult<IReadOnlyList<Batch>>(result);
    }

    public Task<IReadOnlyList<Batch>> ListAsync(BatchFilter? filter = null, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        var query = _batches.AsEnumerable();

        if (filter != null)
        {
            if (filter.MedicineId.HasValue)
                query = query.Where(b => b.MedicineId == filter.MedicineId.Value);

            if (filter.IsActive.HasValue)
                query = query.Where(b => b.IsActive == filter.IsActive.Value);

            if (filter.InStockOnly.HasValue)
            {
                query = filter.InStockOnly.Value
                    ? query.Where(b => b.QuantityOnHand > 0)
                    : query.Where(b => b.QuantityOnHand == 0);
            }

            if (filter.ExpiredAsOf.HasValue)
            {
                query = query.Where(b => b.ExpiryDate.HasValue && b.ExpiryDate.Value <= filter.ExpiredAsOf.Value);
            }

            if (filter.ValidAsOf.HasValue)
            {
                query = query.Where(b => b.ExpiryDate.HasValue && b.ExpiryDate.Value > filter.ValidAsOf.Value);
            }

            if (filter.HasUnknownExpiry.HasValue && filter.HasUnknownExpiry.Value)
            {
                query = query.Where(b => b.ExpiryDate == null);
            }
        }

        var result = query
            .OrderBy(b => b.ExpiryDate == null)
            .ThenBy(b => b.ExpiryDate)
            .Select(Clone)
            .ToList();

        return Task.FromResult<IReadOnlyList<Batch>>(result);
    }

    public Task<IReadOnlyList<Batch>> GetExpiredBatchesAsync(DateOnly asOfDate, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        var matches = _batches
            .Where(b => b.IsActive && b.ExpiryDate.HasValue && b.ExpiryDate.Value <= asOfDate)
            .OrderBy(b => b.ExpiryDate)
            .Select(Clone)
            .ToList();

        return Task.FromResult<IReadOnlyList<Batch>>(matches);
    }

    public Task AddAsync(Batch batch, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(batch);

        if (_batches.Any(b => string.Equals(b.BatchCode, batch.BatchCode, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DuplicateBatchCodeException(batch.BatchCode);
        }

        _batches.Add(Clone(batch));
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Batch batch, uint? expectedVersion = null, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var index = _batches.FindIndex(b => b.Id == batch.Id);
        if (index < 0)
        {
            throw new EntityNotFoundException($"Batch with ID '{batch.Id}' was not found.");
        }

        var existing = _batches[index];
        uint checkVersion = expectedVersion ?? (batch.Version > 1 ? batch.Version - 1 : 1);

        if (existing.Version != checkVersion)
        {
            throw new ConcurrencyException(
                $"Optimistic concurrency violation: Batch with ID '{batch.Id}' was modified by another operation. Expected version {checkVersion}, but current version is {existing.Version}.");
        }

        _batches[index] = Clone(batch);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsBatchCodeAsync(string batchCode, Guid? excludeBatchId = null, CancellationToken cancellationToken = default, DbTransaction? transaction = null)
    {
        if (string.IsNullOrWhiteSpace(batchCode))
            return Task.FromResult(false);

        bool exists = _batches.Any(b =>
            (!excludeBatchId.HasValue || b.Id != excludeBatchId.Value) &&
            string.Equals(b.BatchCode, batchCode.Trim(), StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(exists);
    }

    private static Batch Clone(Batch b) => Batch.Load(
        b.Id,
        b.MedicineId,
        b.BatchCode,
        b.ManufacturerBatchNumber,
        b.ExpiryDate,
        b.QuantityOnHand,
        b.PurchaseCost,
        b.SellingPrice,
        b.Location,
        b.IsActive,
        b.CreatedAt,
        b.UpdatedAt,
        b.Version);
}
