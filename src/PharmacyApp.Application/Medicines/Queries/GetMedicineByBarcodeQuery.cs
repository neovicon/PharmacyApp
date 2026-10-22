using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Common;

namespace PharmacyApp.Application.Medicines.Queries;

/// <summary>
/// Query to retrieve a medicine by its barcode.
/// </summary>
public sealed record GetMedicineByBarcodeQuery(string Barcode);

/// <summary>
/// Handles retrieval of a medicine by barcode and maps it to a DTO.
/// </summary>
public sealed class GetMedicineByBarcodeQueryHandler : IQueryHandler<GetMedicineByBarcodeQuery, MedicineDto>
{
    private readonly IMedicineRepository _medicineRepository;

    public GetMedicineByBarcodeQueryHandler(IMedicineRepository medicineRepository)
    {
        _medicineRepository = medicineRepository;
    }

    public async Task<Result<MedicineDto>> HandleAsync(GetMedicineByBarcodeQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrWhiteSpace(query.Barcode))
        {
            return Result.Failure<MedicineDto>("Barcode cannot be empty.");
        }

        var medicine = await _medicineRepository.GetByBarcodeAsync(query.Barcode.Trim(), cancellationToken);
        if (medicine == null)
        {
            return Result.Failure<MedicineDto>($"Medicine with barcode '{query.Barcode.Trim()}' was not found.");
        }

        return Result.Success(MedicineDto.FromDomain(medicine));
    }
}
