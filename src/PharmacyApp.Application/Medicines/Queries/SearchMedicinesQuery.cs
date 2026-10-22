using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Common;

namespace PharmacyApp.Application.Medicines.Queries;

/// <summary>
/// Query to search medicines across Name, GenericName, Brand, Category, and Barcode.
/// </summary>
public sealed record SearchMedicinesQuery(string SearchTerm, bool? IsActive = null);

/// <summary>
/// Handles searching medicines using the repository search functionality.
/// </summary>
public sealed class SearchMedicinesQueryHandler : IQueryHandler<SearchMedicinesQuery, IReadOnlyList<MedicineDto>>
{
    private readonly IMedicineRepository _medicineRepository;

    public SearchMedicinesQueryHandler(IMedicineRepository medicineRepository)
    {
        _medicineRepository = medicineRepository;
    }

    public async Task<Result<IReadOnlyList<MedicineDto>>> HandleAsync(SearchMedicinesQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var medicines = await _medicineRepository.SearchAsync(query.SearchTerm, query.IsActive, cancellationToken);
        var dtos = medicines.Select(MedicineDto.FromDomain).ToList();
        return Result.Success<IReadOnlyList<MedicineDto>>(dtos);
    }
}
