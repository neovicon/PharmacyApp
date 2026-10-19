using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Common;
using PharmacyApp.Domain.Entities;
using PharmacyApp.Domain.Enums;

namespace PharmacyApp.Application.Authentication.Commands;

public sealed record CreateInitialAdminCommand(
    string Username,
    string DisplayName,
    string Password,
    string ConfirmPassword);

public sealed class CreateInitialAdminCommandHandler : ICommandHandler<CreateInitialAdminCommand, Guid>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CreateInitialAdminCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IDateTimeProvider dateTimeProvider)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<Guid>> HandleAsync(CreateInitialAdminCommand command, CancellationToken cancellationToken = default)
    {
        // Guard against duplicate initial administrator creation
        bool hasExistingUsers = await _userRepository.HasAnyUsersAsync(cancellationToken);
        if (hasExistingUsers)
        {
            return Result.Failure<Guid>("Initial setup has already been completed. Ordinary login is required.");
        }

        if (string.IsNullOrWhiteSpace(command.Username))
            return Result.Failure<Guid>("Username is required.");

        string trimmedUsername = command.Username.Trim();
        if (trimmedUsername.Length < 3)
            return Result.Failure<Guid>("Username must be at least 3 characters long.");

        if (string.IsNullOrWhiteSpace(command.DisplayName))
            return Result.Failure<Guid>("Display name is required.");

        var passwordValidation = PasswordPolicy.Validate(command.Password, command.ConfirmPassword);
        if (passwordValidation.IsFailure)
            return Result.Failure<Guid>(passwordValidation.Error!);

        string passwordHash = _passwordHasher.HashPassword(command.Password);
        DateTimeOffset now = _dateTimeProvider.UtcNow;

        var adminUser = User.Create(
            username: trimmedUsername,
            displayName: command.DisplayName.Trim(),
            passwordHash: passwordHash,
            role: UserRole.Admin,
            now: now);

        bool created = await _userRepository.TryCreateInitialAdminAsync(adminUser, cancellationToken);
        if (!created)
        {
            return Result.Failure<Guid>("Initial setup has already been completed. Ordinary login is required.");
        }

        return Result.Success(adminUser.Id);
    }
}
