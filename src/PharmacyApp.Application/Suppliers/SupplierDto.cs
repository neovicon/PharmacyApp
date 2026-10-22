using PharmacyApp.Domain.Entities;

namespace PharmacyApp.Application.Suppliers;

/// <summary>
/// Data transfer object for wholesale pharmaceutical suppliers.
/// </summary>
public sealed record SupplierDto(
    Guid Id,
    string Name,
    string? ContactPerson,
    string? Phone,
    string? Email,
    string? PanNumber,
    string? Address,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint Version)
{
    public static SupplierDto FromDomain(Supplier supplier)
    {
        ArgumentNullException.ThrowIfNull(supplier);

        return new SupplierDto(
            supplier.Id,
            supplier.Name,
            supplier.ContactPerson,
            supplier.Phone,
            supplier.Email,
            supplier.PanNumber,
            supplier.Address,
            supplier.IsActive,
            supplier.CreatedAt,
            supplier.UpdatedAt,
            supplier.Version);
    }
}
