using Microsoft.Extensions.DependencyInjection;
using PharmacyApp.Application.Abstractions;
using PharmacyApp.Desktop.Navigation;
using PharmacyApp.Desktop.ViewModels.Dashboard;
using PharmacyApp.Desktop.ViewModels.Medicines;
using PharmacyApp.Desktop.ViewModels.Shell;
using PharmacyApp.Infrastructure;
using Xunit;

namespace PharmacyApp.Desktop.Tests;

public class ShellViewModelTests : IDisposable
{
    private readonly IsolatedDesktopTestPathProvider _pathProvider = new();
    private readonly ServiceProvider _serviceProvider;
    private readonly ShellViewModel _shellViewModel;
    private readonly INavigationService _navigationService;
    private readonly ISessionManager _sessionManager;
    private readonly IUserRepository _userRepository;

    public ShellViewModelTests()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure();
        services.AddSingleton<IAppPathProvider>(_pathProvider);
        services.AddDesktopServices();
        _serviceProvider = services.BuildServiceProvider();
        _navigationService = _serviceProvider.GetRequiredService<INavigationService>();
        _sessionManager = _serviceProvider.GetRequiredService<ISessionManager>();
        _userRepository = _serviceProvider.GetRequiredService<IUserRepository>();
        _shellViewModel = _serviceProvider.GetRequiredService<ShellViewModel>();
    }

    public void Dispose()
    {
        _shellViewModel.Dispose();
        _serviceProvider.Dispose();
        _pathProvider.Dispose();
    }

    [Fact]
    public void InitialState_CurrentPageMatchesNavigationService()
    {
        Assert.NotNull(_shellViewModel.CurrentPage);
        Assert.IsType<DashboardViewModel>(_shellViewModel.CurrentPage);
        Assert.Same(_navigationService.CurrentPage, _shellViewModel.CurrentPage);
    }

    [Fact]
    public void InitialState_DashboardNavItemIsSelected()
    {
        var dashboardItem = _shellViewModel.NavItems[0];
        Assert.Equal("Dashboard", dashboardItem.Label);
        Assert.True(dashboardItem.IsSelected);

        for (int i = 1; i < _shellViewModel.NavItems.Count; i++)
        {
            Assert.False(_shellViewModel.NavItems[i].IsSelected);
        }
    }

    [Fact]
    public void Shell_ContainsSevenSidebarNavItems()
    {
        Assert.Equal(7, _shellViewModel.NavItems.Count);

        string[] expectedLabels =
        [
            "Dashboard",
            "Medicines",
            "Inventory",
            "Purchases",
            "Sales",
            "Reports",
            "Settings"
        ];

        for (int i = 0; i < expectedLabels.Length; i++)
        {
            Assert.Equal(expectedLabels[i], _shellViewModel.NavItems[i].Label);
            Assert.NotNull(_shellViewModel.NavItems[i].NavigateCommand);
            Assert.False(string.IsNullOrWhiteSpace(_shellViewModel.NavItems[i].IconGlyph));
            Assert.NotNull(_shellViewModel.NavItems[i].TargetViewModelType);
        }
    }

    [Fact]
    public void ExecutingNavItemCommand_UpdatesCurrentPageAndSelectionState()
    {
        var dashboardItem = _shellViewModel.NavItems[0];
        var medicinesItem = _shellViewModel.NavItems[1];

        Assert.True(dashboardItem.IsSelected);
        Assert.False(medicinesItem.IsSelected);

        medicinesItem.NavigateCommand.Execute(null);

        Assert.IsType<MedicinesViewModel>(_shellViewModel.CurrentPage);
        Assert.Same(_navigationService.CurrentPage, _shellViewModel.CurrentPage);

        // Verification of active sidebar highlight synchronization
        Assert.False(dashboardItem.IsSelected);
        Assert.True(medicinesItem.IsSelected);
    }

    [Fact]
    public async Task InitializeAsync_WhenNoUsersExist_TransitionsToFirstRunSetup()
    {
        var dbInit = _serviceProvider.GetRequiredService<PharmacyApp.Infrastructure.Persistence.SqliteDatabaseInitializer>();
        await dbInit.InitializeAsync();

        await _shellViewModel.InitializeAsync();

        Assert.Equal(ShellState.FirstRunSetup, _shellViewModel.ShellState);
        Assert.NotNull(_shellViewModel.AuthViewModel);
        Assert.IsType<PharmacyApp.Desktop.ViewModels.Auth.FirstRunSetupViewModel>(_shellViewModel.AuthViewModel);
    }

    [Fact]
    public async Task InitializeAsync_WhenUsersExist_TransitionsToLogin()
    {
        var dbInit = _serviceProvider.GetRequiredService<PharmacyApp.Infrastructure.Persistence.SqliteDatabaseInitializer>();
        await dbInit.InitializeAsync();

        var existingUser = PharmacyApp.Domain.Entities.User.Create("admin", "Admin", "hash", PharmacyApp.Domain.Enums.UserRole.Admin, DateTimeOffset.UtcNow);
        await _userRepository.AddAsync(existingUser);

        await _shellViewModel.InitializeAsync();

        Assert.Equal(ShellState.Login, _shellViewModel.ShellState);
        Assert.NotNull(_shellViewModel.AuthViewModel);
        Assert.IsType<PharmacyApp.Desktop.ViewModels.Auth.LoginViewModel>(_shellViewModel.AuthViewModel);
    }

    [Fact]
    public void LockCommand_WhenActive_TransitionsToLockedStateAndSetsLockOverlay()
    {
        var session = new PharmacyApp.Application.Authentication.UserSessionDto(
            Guid.NewGuid(), "admin", "Administrator", PharmacyApp.Domain.Enums.UserRole.Admin);
        _sessionManager.StartSession(session);

        Assert.Equal(ShellState.Authenticated, _shellViewModel.ShellState);
        Assert.Null(_shellViewModel.AuthViewModel);
        Assert.Equal("Administrator", _shellViewModel.UserDisplayName);

        _shellViewModel.LockCommand.Execute(null);

        Assert.Equal(ShellState.Locked, _shellViewModel.ShellState);
        Assert.NotNull(_shellViewModel.LockOverlayViewModel);
    }

    [Fact]
    public void LogoutCommand_TransitionsToLoginStateAndClearsIdentity()
    {
        var session = new PharmacyApp.Application.Authentication.UserSessionDto(
            Guid.NewGuid(), "pharmacist", "Jane Doe", PharmacyApp.Domain.Enums.UserRole.Pharmacist);
        _sessionManager.StartSession(session);

        _shellViewModel.LogoutCommand.Execute(null);

        Assert.Equal(ShellState.Login, _shellViewModel.ShellState);
        Assert.NotNull(_shellViewModel.AuthViewModel);
        Assert.IsType<PharmacyApp.Desktop.ViewModels.Auth.LoginViewModel>(_shellViewModel.AuthViewModel);
        Assert.Equal(string.Empty, _shellViewModel.UserDisplayName);
    }
}
