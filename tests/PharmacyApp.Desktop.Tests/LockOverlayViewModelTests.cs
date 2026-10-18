using PharmacyApp.Application.Authentication;
using PharmacyApp.Desktop.ViewModels.Auth;
using PharmacyApp.Domain.Enums;
using Xunit;

namespace PharmacyApp.Desktop.Tests;

public class LockOverlayViewModelTests
{
    private readonly StubSessionManager _sessionManager = new();

    [Fact]
    public void DisplayProperties_ReflectLockedCurrentUser()
    {
        var session = new UserSessionDto(Guid.NewGuid(), "admin", "System Administrator", UserRole.Admin);
        _sessionManager.StartSession(session);
        _sessionManager.LockSession();

        var vm = new LockOverlayViewModel(_sessionManager, () => { });

        Assert.Equal("System Administrator", vm.DisplayName);
        Assert.Equal("admin", vm.Username);
        Assert.Equal(UserRole.Admin, vm.Role);
    }

    [Fact]
    public async Task UnlockCommand_WhenPasswordWrong_ShowsErrorAndClearsPassword()
    {
        var session = new UserSessionDto(Guid.NewGuid(), "admin", "System Administrator", UserRole.Admin);
        _sessionManager.StartSession(session);
        _sessionManager.LockSession();

        bool unlockedCallbackFired = false;
        var vm = new LockOverlayViewModel(_sessionManager, () => unlockedCallbackFired = true)
        {
            Password = "wrong_password"
        };

        await vm.UnlockCommand.ExecuteAsync(null);

        Assert.Equal("Invalid password.", vm.ErrorMessage);
        Assert.Equal(string.Empty, vm.Password);
        Assert.False(unlockedCallbackFired);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task UnlockCommand_WhenPasswordCorrect_InvokesUnlockedCallback()
    {
        var session = new UserSessionDto(Guid.NewGuid(), "admin", "System Administrator", UserRole.Admin);
        _sessionManager.StartSession(session);
        _sessionManager.LockSession();

        bool unlockedCallbackFired = false;
        var vm = new LockOverlayViewModel(_sessionManager, () => unlockedCallbackFired = true)
        {
            Password = "correct_pass"
        };

        await vm.UnlockCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorMessage);
        Assert.Equal(string.Empty, vm.Password);
        Assert.True(unlockedCallbackFired);
    }

    [Fact]
    public void LogoutCommand_TerminatesSession()
    {
        var session = new UserSessionDto(Guid.NewGuid(), "admin", "System Administrator", UserRole.Admin);
        _sessionManager.StartSession(session);
        _sessionManager.LockSession();

        var vm = new LockOverlayViewModel(_sessionManager, () => { })
        {
            Password = "residual_input"
        };

        vm.LogoutCommand.Execute(null);

        Assert.True(_sessionManager.Terminated);
        Assert.Equal(string.Empty, vm.Password);
        Assert.Null(vm.ErrorMessage);
    }
}
