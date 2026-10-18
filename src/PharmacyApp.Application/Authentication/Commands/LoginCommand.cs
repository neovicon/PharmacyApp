using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Common;

namespace PharmacyApp.Application.Authentication.Commands;

public sealed record LoginCommand(string Username, string Password);

public sealed class LoginCommandHandler : ICommandHandler<LoginCommand, UserSessionDto>
{
    private const string GenericErrorMessage = "Invalid username or password.";

    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public LoginCommandHandler(IUserRepository userRepository, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<UserSessionDto>> HandleAsync(LoginCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Username) || string.IsNullOrEmpty(command.Password))
        {
            return Result.Failure<UserSessionDto>(GenericErrorMessage);
        }

        var user = await _userRepository.GetByUsernameAsync(command.Username.Trim(), cancellationToken);
        if (user is null || !user.IsActive)
        {
            // Uniform failure message to prevent username enumeration
            return Result.Failure<UserSessionDto>(GenericErrorMessage);
        }

        bool isPasswordValid = _passwordHasher.VerifyPassword(command.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            return Result.Failure<UserSessionDto>(GenericErrorMessage);
        }

        var session = new UserSessionDto(
            UserId: user.Id,
            Username: user.Username,
            DisplayName: user.DisplayName,
            Role: user.Role);

        return Result.Success(session);
    }
}
