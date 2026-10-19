using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Common;

namespace PharmacyApp.Application.Authentication.Commands;

public sealed record LoginCommand(string Username, string Password);

public sealed class LoginCommandHandler : ICommandHandler<LoginCommand, UserSessionDto>
{
    private const string GenericErrorMessage = "Invalid username or password.";

    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILoginThrottleService _throttleService;

    public LoginCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ILoginThrottleService throttleService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _throttleService = throttleService;
    }

    public async Task<Result<UserSessionDto>> HandleAsync(LoginCommand command, CancellationToken cancellationToken = default)
    {
        if (_throttleService.IsThrottled(out TimeSpan retryAfter))
        {
            int remainingSeconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
            return Result.Failure<UserSessionDto>(
                $"Too many failed login attempts. Please try again in {remainingSeconds} second{(remainingSeconds == 1 ? "" : "s")}.");
        }

        if (string.IsNullOrWhiteSpace(command.Username) || string.IsNullOrEmpty(command.Password))
        {
            _throttleService.RecordFailedAttempt();
            return Result.Failure<UserSessionDto>(GenericErrorMessage);
        }

        if (command.Password.Length > PasswordPolicy.MaxLength)
        {
            _throttleService.RecordFailedAttempt();
            return Result.Failure<UserSessionDto>(GenericErrorMessage);
        }

        var user = await _userRepository.GetByUsernameAsync(command.Username.Trim(), cancellationToken);
        if (user is null || !user.IsActive)
        {
            // Uniform failure message to prevent username enumeration
            _throttleService.RecordFailedAttempt();
            return Result.Failure<UserSessionDto>(GenericErrorMessage);
        }

        bool isPasswordValid = _passwordHasher.VerifyPassword(command.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            _throttleService.RecordFailedAttempt();
            return Result.Failure<UserSessionDto>(GenericErrorMessage);
        }

        _throttleService.Reset();

        var session = new UserSessionDto(
            UserId: user.Id,
            Username: user.Username,
            DisplayName: user.DisplayName,
            Role: user.Role);

        return Result.Success(session);
    }
}
