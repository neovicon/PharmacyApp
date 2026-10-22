using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Common;

namespace PharmacyApp.Application.Medicines.Queries;

/// <summary>
/// Query to list medicines ordered by name, optionally filtered by active status.
/// </summary>
public sealed record ListMedicinesQuery(bool? IsActive = null);

/// <summary>
/// Handles listing medicines using the repository list functionality.
/// </summary>
public sealed class ListMedicinesQueryHandler : IQueryHandler<ListMedicinesQuery, IReadOnlyList<MedicineDto>>
{
    private readonly IMedicineRepository _medicineRepository;

    public ListMedicinesQueryHandler(IMedicineRepository medicineRepository)
    {
        _medicineRepository = medicineRepository;
    }

    public async Task<Result<IReadOnlyList<MedicineDto>>> HandleAsync(ListMedicinesQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var medicines = await _medicineRepository.ListAsync(query.IsActive, cancellationToken);
        var dtos = medicines.Select(MedicineDto.FromDomain).ToList();
        return Result.Success<IReadOnlyList<MedicineDto>>(dtos);
    }
}
