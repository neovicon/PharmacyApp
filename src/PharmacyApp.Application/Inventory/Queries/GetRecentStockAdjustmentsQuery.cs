using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Common;

namespace PharmacyApp.Application.Inventory.Queries;

public sealed record GetRecentStockAdjustmentsQuery(int Limit = 50, Guid? MedicineId = null, Guid? BatchId = null);

public sealed class GetRecentStockAdjustmentsQueryHandler : IQueryHandler<GetRecentStockAdjustmentsQuery, IReadOnlyList<StockAdjustmentDto>>
{
    private readonly IStockAdjustmentRepository _stockAdjustmentRepository;
    private readonly IMedicineRepository _medicineRepository;
    private readonly IBatchRepository _batchRepository;

    public GetRecentStockAdjustmentsQueryHandler(
        IStockAdjustmentRepository stockAdjustmentRepository,
        IMedicineRepository medicineRepository,
        IBatchRepository batchRepository)
    {
        _stockAdjustmentRepository = stockAdjustmentRepository;
        _medicineRepository = medicineRepository;
        _batchRepository = batchRepository;
    }

    public async Task<Result<IReadOnlyList<StockAdjustmentDto>>> HandleAsync(GetRecentStockAdjustmentsQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var adjustments = query.BatchId.HasValue
            ? await _stockAdjustmentRepository.GetByBatchIdAsync(query.BatchId.Value, cancellationToken)
            : query.MedicineId.HasValue
                ? await _stockAdjustmentRepository.GetByMedicineIdAsync(query.MedicineId.Value, cancellationToken)
                : await _stockAdjustmentRepository.ListRecentAsync(query.Limit, cancellationToken);

        var medicines = (await _medicineRepository.ListAsync(cancellationToken: cancellationToken))
            .ToDictionary(m => m.Id, m => m.Name);

        var dtos = new List<StockAdjustmentDto>();
        foreach (var adj in adjustments)
        {
            medicines.TryGetValue(adj.MedicineId, out var medName);
            var batch = await _batchRepository.GetByIdAsync(adj.BatchId, cancellationToken);
            string batchCode = batch?.BatchCode ?? "UNKNOWN";

            dtos.Add(StockAdjustmentDto.FromDomain(adj, batchCode, medName ?? "Unknown Medicine"));
        }

        return Result.Success<IReadOnlyList<StockAdjustmentDto>>(dtos);
    }
}
