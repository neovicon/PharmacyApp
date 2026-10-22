using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Common;

namespace PharmacyApp.Application.Inventory.Queries;

public sealed record GetBatchesByMedicineIdQuery(Guid MedicineId, bool? IsActive = null);

public sealed class GetBatchesByMedicineIdQueryHandler : IQueryHandler<GetBatchesByMedicineIdQuery, IReadOnlyList<BatchDto>>
{
    private readonly IBatchRepository _batchRepository;
    private readonly IMedicineRepository _medicineRepository;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetBatchesByMedicineIdQueryHandler(
        IBatchRepository batchRepository,
        IMedicineRepository medicineRepository,
        IDateTimeProvider dateTimeProvider)
    {
        _batchRepository = batchRepository;
        _medicineRepository = medicineRepository;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<IReadOnlyList<BatchDto>>> HandleAsync(GetBatchesByMedicineIdQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.MedicineId == Guid.Empty)
            return Result.Failure<IReadOnlyList<BatchDto>>("Medicine ID cannot be empty.");

        var medicine = await _medicineRepository.GetByIdAsync(query.MedicineId, cancellationToken);
        string medicineName = medicine?.Name ?? "Unknown Medicine";

        var batches = await _batchRepository.GetByMedicineIdAsync(query.MedicineId, query.IsActive, cancellationToken);
        var today = DateOnly.FromDateTime(_dateTimeProvider.UtcNow.Date);

        var dtos = batches.Select(b => BatchDto.FromDomain(b, medicineName, today)).ToList();
        return Result.Success<IReadOnlyList<BatchDto>>(dtos);
    }
}
