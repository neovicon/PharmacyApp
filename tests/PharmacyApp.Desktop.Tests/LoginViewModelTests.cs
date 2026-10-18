using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Authentication;
using PharmacyApp.Application.Authentication.Commands;
using PharmacyApp.Application.Common;
using PharmacyApp.Desktop.ViewModels.Auth;
using PharmacyApp.Domain.Enums;
using Xunit;

namespace PharmacyApp.Desktop.Tests;

public class LoginViewModelTests
{
    private readonly StubLoginHandler _handler = new();
    private readonly StubSessionManager _sessionManager = new();

    [Fact]
    public async Task LoginCommand_WhenHandlerFails_DisplaysGenericErrorAndClearsPassword()
    {
        _handler.ResultToReturn = Result.Failure<UserSessionDto>("Invalid username or password.");
        bool completed = false;

        var vm = new LoginViewModel(_handler, _sessionManager, () => completed = true)
        {
            Username = "admin",
            Password = "WrongPassword123!"
        };

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.Equal("Invalid username or password.", vm.ErrorMessage);
        Assert.Equal(string.Empty, vm.Password); // Password cleared
        Assert.False(completed);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task LoginCommand_WhenSuccessful_StartsSessionAndInvokesCallback()
    {
        var session = new UserSessionDto(Guid.NewGuid(), "pharmacist", "Jane Pharmacist", UserRole.Pharmacist);
        _handler.ResultToReturn = Result.Success(session);
        bool completed = false;

        var vm = new LoginViewModel(_handler, _sessionManager, () => completed = true)
        {
            Username = "pharmacist",
            Password = "CorrectPassword123!"
        };

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorMessage);
        Assert.Equal(string.Empty, vm.Password);
        Assert.True(completed);
        Assert.Same(session, _sessionManager.StartedSession);
    }
}

internal sealed class StubLoginHandler : ICommandHandler<LoginCommand, UserSessionDto>
{
    public Result<UserSessionDto> ResultToReturn { get; set; } =
        Result.Success(new UserSessionDto(Guid.NewGuid(), "user", "User", UserRole.Cashier));

    public Task<Result<UserSessionDto>> HandleAsync(LoginCommand command, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ResultToReturn);
    }
}
