using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Common;

namespace PharmacyApp.Application.Suppliers.Queries;

/// <summary>
/// Query to list suppliers, optionally filtered by active status or search query.
/// </summary>
public sealed record ListSuppliersQuery(bool? IsActive = null, string? SearchQuery = null);

/// <summary>
/// Handles listing suppliers ordered by name.
/// </summary>
public sealed class ListSuppliersQueryHandler : IQueryHandler<ListSuppliersQuery, IReadOnlyList<SupplierDto>>
{
    private readonly ISupplierRepository _supplierRepository;

    public ListSuppliersQueryHandler(ISupplierRepository supplierRepository)
    {
        _supplierRepository = supplierRepository;
    }

    public async Task<Result<IReadOnlyList<SupplierDto>>> HandleAsync(ListSuppliersQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var suppliers = !string.IsNullOrWhiteSpace(query.SearchQuery)
            ? await _supplierRepository.SearchAsync(query.SearchQuery.Trim(), query.IsActive, cancellationToken)
            : await _supplierRepository.ListAsync(query.IsActive, cancellationToken);

        var dtos = suppliers.Select(SupplierDto.FromDomain).ToList();
        return Result.Success<IReadOnlyList<SupplierDto>>(dtos);
    }
}
