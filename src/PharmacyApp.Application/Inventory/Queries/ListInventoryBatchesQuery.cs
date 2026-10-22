using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Common;

namespace PharmacyApp.Application.Inventory.Queries;

public sealed record ListInventoryBatchesQuery(
    Guid? MedicineId = null,
    bool? InStockOnly = null,
    bool? IsActive = null);

public sealed class ListInventoryBatchesQueryHandler : IQueryHandler<ListInventoryBatchesQuery, IReadOnlyList<BatchDto>>
{
    private readonly IBatchRepository _batchRepository;
    private readonly IMedicineRepository _medicineRepository;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ListInventoryBatchesQueryHandler(
        IBatchRepository batchRepository,
        IMedicineRepository medicineRepository,
        IDateTimeProvider dateTimeProvider)
    {
        _batchRepository = batchRepository;
        _medicineRepository = medicineRepository;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<IReadOnlyList<BatchDto>>> HandleAsync(ListInventoryBatchesQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filter = new BatchFilter
        {
            MedicineId = query.MedicineId,
            InStockOnly = query.InStockOnly,
            IsActive = query.IsActive
        };

        var batches = await _batchRepository.ListAsync(filter, cancellationToken);
        var medicines = (await _medicineRepository.ListAsync(cancellationToken: cancellationToken))
            .ToDictionary(m => m.Id, m => m.Name);

        var today = DateOnly.FromDateTime(_dateTimeProvider.UtcNow.Date);

        var dtos = batches.Select(b =>
        {
            medicines.TryGetValue(b.MedicineId, out var medName);
            return BatchDto.FromDomain(b, medName ?? "Unknown Medicine", today);
        }).ToList();

        return Result.Success<IReadOnlyList<BatchDto>>(dtos);
    }
}
