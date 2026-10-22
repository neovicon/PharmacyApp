using Microsoft.Extensions.DependencyInjection;
using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Authentication;
using PharmacyApp.Application.Common;
using PharmacyApp.Application.Settings;
using PharmacyApp.Application.Users;
using PharmacyApp.Desktop.ViewModels.Settings;
using PharmacyApp.Domain.Enums;
using PharmacyApp.Infrastructure;
using Xunit;

namespace PharmacyApp.Desktop.Tests;

public class SettingsViewModelTests : IDisposable
{
    private readonly IsolatedDesktopTestPathProvider _pathProvider = new();
    private readonly ServiceProvider _provider;
    private readonly ISessionManager _sessionManager;

    public SettingsViewModelTests()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure();
        services.AddSingleton<IAppPathProvider>(_pathProvider);
        services.AddDesktopServices();

        _provider = services.BuildServiceProvider();
        var initializer = _provider.GetRequiredService<PharmacyApp.Infrastructure.Persistence.SqliteDatabaseInitializer>();
        initializer.InitializeAsync().GetAwaiter().GetResult();

        _sessionManager = _provider.GetRequiredService<ISessionManager>();

        // Set up active admin session
        _sessionManager.StartSession(new UserSessionDto(
            Guid.NewGuid(),
            "admin",
            "System Admin",
            UserRole.Admin));
    }

    [Fact]
    public async Task LoadAllAsync_PopulatesInitialState()
    {
        var vm = _provider.GetRequiredService<SettingsViewModel>();

        await vm.LoadAllCommand.ExecuteAsync(null);

        Assert.True(vm.IsAdmin);
        Assert.NotNull(vm.StoreName);
        Assert.NotEmpty(vm.StoreName);
        Assert.NotNull(vm.DotNetVersion);
        Assert.NotNull(vm.OperatingSystem);
    }

    [Fact]
    public void OpenAndCloseAddUserModal_UpdatesModalState()
    {
        var vm = _provider.GetRequiredService<SettingsViewModel>();

        vm.OpenAddUserModalCommand.Execute(null);
        Assert.True(vm.IsAddUserModalOpen);

        vm.CloseAddUserModalCommand.Execute(null);
        Assert.False(vm.IsAddUserModalOpen);
    }

    [Fact]
    public async Task SaveStoreProfile_PersistsSettings()
    {
        var vm = _provider.GetRequiredService<SettingsViewModel>();

        vm.StoreName = "Everest Health Care";
        vm.StorePhone = "+977-1-5559999";
        vm.AutoLockMinutes = 30;

        await vm.SaveStoreProfileCommand.ExecuteAsync(null);

        Assert.True(vm.IsProfileSaved);
        Assert.NotNull(vm.ProfileStatusMessage);
    }

    public void Dispose()
    {
        _provider.Dispose();
        _pathProvider.Dispose();
    }
}
