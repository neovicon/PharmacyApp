using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Common;
using PharmacyApp.Domain.Enums;

namespace PharmacyApp.Application.Suppliers.Commands;

/// <summary>
/// Command to toggle active status of a supplier.
/// </summary>
public sealed record ToggleSupplierActiveCommand(Guid SupplierId, Guid CurrentUserId = default);

public sealed class ToggleSupplierActiveCommandHandler : ICommandHandler<ToggleSupplierActiveCommand>
{
    private readonly ISupplierRepository _supplierRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ToggleSupplierActiveCommandHandler(
        ISupplierRepository supplierRepository,
        IUserRepository userRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider)
    {
        _supplierRepository = supplierRepository;
        _userRepository = userRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result> HandleAsync(ToggleSupplierActiveCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Guid userId = command.CurrentUserId != Guid.Empty
            ? command.CurrentUserId
            : (_currentUserService.IsAuthenticated ? _currentUserService.UserId : Guid.Empty);

        if (userId != Guid.Empty)
        {
            var currentUser = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (currentUser is not null && (!currentUser.IsActive || (currentUser.Role != UserRole.Admin && currentUser.Role != UserRole.Pharmacist)))
            {
                return Result.Failure("Only active Administrators or Pharmacists can modify supplier status.");
            }
        }
        else if (_currentUserService.IsAuthenticated &&
                 _currentUserService.Role != UserRole.Admin &&
                 _currentUserService.Role != UserRole.Pharmacist)
        {
            return Result.Failure("Only active Administrators or Pharmacists can modify supplier status.");
        }

        var supplier = await _supplierRepository.GetByIdAsync(command.SupplierId, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure("Supplier was not found.");
        }

        if (supplier.IsActive)
        {
            supplier.Deactivate(_dateTimeProvider.UtcNow);
        }
        else
        {
            supplier.Activate(_dateTimeProvider.UtcNow);
        }

        await _supplierRepository.UpdateAsync(supplier, cancellationToken);
        return Result.Success();
    }
}
