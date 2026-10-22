using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Common;

namespace PharmacyApp.Application.Medicines.Queries;

/// <summary>
/// Query to retrieve a medicine by its unique identifier.
/// </summary>
public sealed record GetMedicineByIdQuery(Guid Id);

/// <summary>
/// Handles retrieval of a medicine by ID and maps it to a DTO.
/// </summary>
public sealed class GetMedicineByIdQueryHandler : IQueryHandler<GetMedicineByIdQuery, MedicineDto>
{
    private readonly IMedicineRepository _medicineRepository;

    public GetMedicineByIdQueryHandler(IMedicineRepository medicineRepository)
    {
        _medicineRepository = medicineRepository;
    }

    public async Task<Result<MedicineDto>> HandleAsync(GetMedicineByIdQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Id == Guid.Empty)
        {
            return Result.Failure<MedicineDto>("Medicine ID cannot be empty.");
        }

        var medicine = await _medicineRepository.GetByIdAsync(query.Id, cancellationToken);
        if (medicine == null)
        {
            return Result.Failure<MedicineDto>($"Medicine with ID '{query.Id}' was not found.");
        }

        return Result.Success(MedicineDto.FromDomain(medicine));
    }
}
