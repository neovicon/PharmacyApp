using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Common;
using PharmacyApp.Domain.Entities;
using PharmacyApp.Domain.Enums;

namespace PharmacyApp.Application.Suppliers.Commands;

/// <summary>
/// Command to register a new wholesale medicine supplier.
/// </summary>
public sealed record CreateSupplierCommand(
    string Name,
    string? ContactPerson = null,
    string? Phone = null,
    string? Email = null,
    string? PanNumber = null,
    string? Address = null,
    Guid CurrentUserId = default);

/// <summary>
/// Handles supplier creation with authorization check and input validation.
/// </summary>
public sealed class CreateSupplierCommandHandler : ICommandHandler<CreateSupplierCommand, Guid>
{
    private readonly ISupplierRepository _supplierRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CreateSupplierCommandHandler(
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

    public async Task<Result<Guid>> HandleAsync(CreateSupplierCommand command, CancellationToken cancellationToken = default)
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
                return Result.Failure<Guid>("Only active Administrators or Pharmacists are authorized to register suppliers.");
            }
        }
        else if (_currentUserService.IsAuthenticated &&
                 _currentUserService.Role != UserRole.Admin &&
                 _currentUserService.Role != UserRole.Pharmacist)
        {
            return Result.Failure<Guid>("Only active Administrators or Pharmacists are authorized to register suppliers.");
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return Result.Failure<Guid>("Supplier name is required.");
        }

        var supplier = Supplier.Create(
            name: command.Name,
            contactPerson: command.ContactPerson,
            phone: command.Phone,
            email: command.Email,
            panNumber: command.PanNumber,
            address: command.Address,
            now: _dateTimeProvider.UtcNow);

        await _supplierRepository.AddAsync(supplier, cancellationToken);
        return Result.Success(supplier.Id);
    }
}
