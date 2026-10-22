using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Common;
using PharmacyApp.Domain.Enums;

namespace PharmacyApp.Application.Users.Commands;

/// <summary>
/// Command to activate or deactivate a user account.
/// </summary>
public sealed record ToggleUserActiveCommand(
    Guid TargetUserId,
    Guid CurrentUserId);

/// <summary>
/// Handles activation/deactivation with safety guards:
/// - Only active Admins can toggle status.
/// - Admins cannot deactivate their own account.
/// - The last active Admin account cannot be deactivated.
/// </summary>
public sealed class ToggleUserActiveCommandHandler : ICommandHandler<ToggleUserActiveCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ToggleUserActiveCommandHandler(
        IUserRepository userRepository,
        IDateTimeProvider dateTimeProvider)
    {
        _userRepository = userRepository;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result> HandleAsync(ToggleUserActiveCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var currentUser = await _userRepository.GetByIdAsync(command.CurrentUserId, cancellationToken);
        if (currentUser is null || !currentUser.IsActive || currentUser.Role != UserRole.Admin)
        {
            return Result.Failure("Only active Administrators can modify user status.");
        }

        if (command.TargetUserId == command.CurrentUserId)
        {
            return Result.Failure("You cannot deactivate your own account.");
        }

        var targetUser = await _userRepository.GetByIdAsync(command.TargetUserId, cancellationToken);
        if (targetUser is null)
        {
            return Result.Failure("Target user was not found.");
        }

        if (targetUser.IsActive)
        {
            // If deactivating an Admin, ensure at least one other active Admin remains
            if (targetUser.Role == UserRole.Admin)
            {
                var allUsers = await _userRepository.ListAsync(cancellationToken);
                int activeAdminCount = allUsers.Count(u => u.Role == UserRole.Admin && u.IsActive && u.Id != targetUser.Id);
                if (activeAdminCount == 0)
                {
                    return Result.Failure("Cannot deactivate the only active Administrator account in the system.");
                }
            }

            targetUser.Deactivate(_dateTimeProvider.UtcNow);
        }
        else
        {
            targetUser.Activate(_dateTimeProvider.UtcNow);
        }

        await _userRepository.UpdateAsync(targetUser, cancellationToken);
        return Result.Success();
    }
}
