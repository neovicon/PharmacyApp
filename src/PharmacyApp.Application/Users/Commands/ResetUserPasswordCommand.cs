using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Authentication;
using PharmacyApp.Application.Common;
using PharmacyApp.Domain.Enums;

namespace PharmacyApp.Application.Users.Commands;

/// <summary>
/// Command to reset a user's password.
/// </summary>
public sealed record ResetUserPasswordCommand(
    Guid TargetUserId,
    string NewPassword,
    Guid CurrentUserId);

/// <summary>
/// Handles password reset with administrative authorization and password complexity validation.
/// </summary>
public sealed class ResetUserPasswordCommandHandler : ICommandHandler<ResetUserPasswordCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ResetUserPasswordCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IDateTimeProvider dateTimeProvider)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result> HandleAsync(ResetUserPasswordCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var currentUser = await _userRepository.GetByIdAsync(command.CurrentUserId, cancellationToken);
        if (currentUser is null || !currentUser.IsActive)
        {
            return Result.Failure("User session is invalid or inactive.");
        }

        // Only Admin or the user themselves can change the password
        if (currentUser.Role != UserRole.Admin && command.CurrentUserId != command.TargetUserId)
        {
            return Result.Failure("Only Administrators can reset other users' passwords.");
        }

        var targetUser = await _userRepository.GetByIdAsync(command.TargetUserId, cancellationToken);
        if (targetUser is null)
        {
            return Result.Failure("Target user was not found.");
        }

        var validation = PasswordPolicy.Validate(command.NewPassword);
        if (validation.IsFailure)
        {
            return Result.Failure(validation.Error ?? "Password does not meet complexity requirements.");
        }

        string newHash = _passwordHasher.HashPassword(command.NewPassword);
        targetUser.UpdatePassword(newHash, _dateTimeProvider.UtcNow);

        await _userRepository.UpdateAsync(targetUser, cancellationToken);
        return Result.Success();
    }
}
