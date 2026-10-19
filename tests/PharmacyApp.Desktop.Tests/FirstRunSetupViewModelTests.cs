using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Authentication;
using PharmacyApp.Application.Authentication.Commands;
using PharmacyApp.Application.Common;
using PharmacyApp.Desktop.ViewModels.Auth;
using PharmacyApp.Domain.Entities;
using PharmacyApp.Domain.Enums;
using Xunit;

namespace PharmacyApp.Desktop.Tests;

public class FirstRunSetupViewModelTests
{
    private readonly StubCreateAdminHandler _handler = new();
    private readonly StubSessionManager _sessionManager = new();

    [Fact]
    public async Task CreateAdministratorCommand_WhenHandlerFails_DisplaysErrorMessage()
    {
        _handler.ResultToReturn = Result.Failure<Guid>("Password does not meet complexity requirements.");
        bool completed = false;

        var vm = new FirstRunSetupViewModel(_handler, _sessionManager, () => completed = true)
        {
            Username = "admin",
            DisplayName = "Administrator",
            Password = "weak",
            ConfirmPassword = "weak"
        };

        await vm.CreateAdministratorCommand.ExecuteAsync(null);

        Assert.Equal("Password does not meet complexity requirements.", vm.ErrorMessage);
        Assert.Equal(string.Empty, vm.Password);
        Assert.Equal(string.Empty, vm.ConfirmPassword);
        Assert.False(completed);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task CreateAdministratorCommand_WhenHandlerThrows_ClearsPasswordsAndDisplaysGenericErrorWithoutExposingException()
    {
        _handler.ThrowOnHandle = true;
        bool completed = false;

        var vm = new FirstRunSetupViewModel(_handler, _sessionManager, () => completed = true)
        {
            Username = "admin",
            DisplayName = "Administrator",
            Password = "SecretPassword123!",
            ConfirmPassword = "SecretPassword123!"
        };

        await vm.CreateAdministratorCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, vm.Password);
        Assert.Equal(string.Empty, vm.ConfirmPassword);
        Assert.DoesNotContain("SensitiveInternalSetupException", vm.ErrorMessage);
        Assert.Equal("Administrator setup failed due to an unexpected error. Please try again.", vm.ErrorMessage);
        Assert.False(completed);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task CreateAdministratorCommand_WhenHandlerSucceeds_StartsSessionAndClearsSensitiveInputs()
    {
        var adminId = Guid.NewGuid();
        _handler.ResultToReturn = Result.Success(adminId);
        bool completed = false;

        var vm = new FirstRunSetupViewModel(_handler, _sessionManager, () => completed = true)
        {
            Username = "AdminUser",
            DisplayName = "Main Admin",
            Password = "ComplexPassword123!",
            ConfirmPassword = "ComplexPassword123!"
        };

        await vm.CreateAdministratorCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorMessage);
        Assert.True(completed);
        Assert.Equal(string.Empty, vm.Password);
        Assert.Equal(string.Empty, vm.ConfirmPassword);
        Assert.NotNull(_sessionManager.StartedSession);
        Assert.Equal(adminId, _sessionManager.StartedSession.UserId);
        Assert.Equal("adminuser", _sessionManager.StartedSession.Username);
        Assert.Equal("Main Admin", _sessionManager.StartedSession.DisplayName);
        Assert.Equal(UserRole.Admin, _sessionManager.StartedSession.Role);
    }
}

internal sealed class StubCreateAdminHandler : ICommandHandler<CreateInitialAdminCommand, Guid>
{
    public bool ThrowOnHandle { get; set; }

    public Result<Guid> ResultToReturn { get; set; } = Result.Success(Guid.NewGuid());

    public Task<Result<Guid>> HandleAsync(CreateInitialAdminCommand command, CancellationToken cancellationToken = default)
    {
        if (ThrowOnHandle)
        {
            throw new InvalidOperationException("SensitiveInternalSetupException: DB file locked");
        }

        return Task.FromResult(ResultToReturn);
    }
}

internal sealed class StubSessionManager : ISessionManager
{
    public bool ThrowOnUnlock { get; set; }

    public SessionState State { get; set; } = SessionState.Unauthenticated;
    public UserSessionDto? CurrentUser => StartedSession;
    public Guid UserId => StartedSession?.UserId ?? Guid.Empty;
    public string Username => StartedSession?.Username ?? string.Empty;
    public string DisplayName => StartedSession?.DisplayName ?? string.Empty;
    public UserRole Role => StartedSession?.Role ?? UserRole.Cashier;
    public bool IsAuthenticated => State == SessionState.Active && StartedSession != null;

    public UserSessionDto? StartedSession { get; private set; }
    public bool Locked { get; private set; }
    public bool Terminated { get; private set; }

    public event EventHandler<SessionStateChangedEventArgs>? SessionStateChanged;

    public void StartSession(UserSessionDto user)
    {
        StartedSession = user;
        State = SessionState.Active;
        SessionStateChanged?.Invoke(this, new SessionStateChangedEventArgs(State, user));
    }

    public void LockSession()
    {
        Locked = true;
        State = SessionState.Locked;
        SessionStateChanged?.Invoke(this, new SessionStateChangedEventArgs(State, StartedSession));
    }

    public Task<Result> UnlockSessionAsync(string password, CancellationToken cancellationToken = default)
    {
        if (ThrowOnUnlock)
        {
            throw new InvalidOperationException("SensitiveUnlockError: DB lock failure");
        }

        if (password == "correct_pass")
        {
            Locked = false;
            State = SessionState.Active;
            SessionStateChanged?.Invoke(this, new SessionStateChangedEventArgs(State, StartedSession));
            return Task.FromResult(Result.Success());
        }

        return Task.FromResult(Result.Failure("Invalid password."));
    }

    public void TerminateSession()
    {
        Terminated = true;
        StartedSession = null;
        State = SessionState.Unauthenticated;
        SessionStateChanged?.Invoke(this, new SessionStateChangedEventArgs(State, null));
    }
}
