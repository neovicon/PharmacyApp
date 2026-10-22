using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Authentication;
using PharmacyApp.Application.Common;
using PharmacyApp.Domain.Entities;
using PharmacyApp.Domain.Enums;

namespace PharmacyApp.Application.Users.Commands;

/// <summary>
/// Command to create a new staff or user account.
/// </summary>
public sealed record CreateUserCommand(
    string Username,
    string DisplayName,
    string Password,
    UserRole Role,
    Guid CurrentUserId);

/// <summary>
/// Handles user creation with administrative authorization, username uniqueness,
/// and password complexity validation.
/// </summary>
public sealed class CreateUserCommandHandler : ICommandHandler<CreateUserCommand, Guid>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CreateUserCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IDateTimeProvider dateTimeProvider)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<Guid>> HandleAsync(CreateUserCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Verify current user is an active Administrator
        var currentUser = await _userRepository.GetByIdAsync(command.CurrentUserId, cancellationToken);
        if (currentUser is null || !currentUser.IsActive || currentUser.Role != UserRole.Admin)
        {
            return Result.Failure<Guid>("Only active Administrators are authorized to create new user accounts.");
        }

        // Validate basic inputs
        if (string.IsNullOrWhiteSpace(command.Username))
        {
            return Result.Failure<Guid>("Username is required.");
        }

        if (string.IsNullOrWhiteSpace(command.DisplayName))
        {
            return Result.Failure<Guid>("Display name is required.");
        }

        string trimmedUsername = command.Username.Trim();
        if (trimmedUsername.Length < 3 || trimmedUsername.Length > 50)
        {
            return Result.Failure<Guid>("Username must be between 3 and 50 characters.");
        }

        // Validate password
        var passwordValidation = PasswordPolicy.Validate(command.Password);
        if (passwordValidation.IsFailure)
        {
            return Result.Failure<Guid>(passwordValidation.Error ?? "Password does not meet complexity requirements.");
        }

        // Check if username is already taken
        var existing = await _userRepository.GetByUsernameAsync(trimmedUsername, cancellationToken);
        if (existing is not null)
        {
            return Result.Failure<Guid>($"Username '{trimmedUsername}' is already taken.");
        }

        // Hash password and create user entity
        string passwordHash = _passwordHasher.HashPassword(command.Password);
        var newUser = User.Create(
            trimmedUsername,
            command.DisplayName.Trim(),
            passwordHash,
            command.Role,
            _dateTimeProvider.UtcNow);

        await _userRepository.AddAsync(newUser, cancellationToken);
        return Result.Success(newUser.Id);
    }
}
