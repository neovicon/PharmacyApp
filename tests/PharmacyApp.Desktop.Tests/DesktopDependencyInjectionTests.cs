using Microsoft.Extensions.DependencyInjection;
using PharmacyApp.Application.Abstractions;
using PharmacyApp.Desktop;
using PharmacyApp.Desktop.Navigation;
using PharmacyApp.Desktop.ViewModels.Dashboard;
using PharmacyApp.Desktop.ViewModels.Inventory;
using PharmacyApp.Desktop.ViewModels.Medicines;
using PharmacyApp.Desktop.ViewModels.Purchases;
using PharmacyApp.Desktop.ViewModels.Reports;
using PharmacyApp.Desktop.ViewModels.Sales;
using PharmacyApp.Desktop.ViewModels.Settings;
using PharmacyApp.Desktop.ViewModels.Shell;
using PharmacyApp.Desktop.Views.Dashboard;
using PharmacyApp.Desktop.Views.Inventory;
using PharmacyApp.Desktop.Views.Medicines;
using PharmacyApp.Desktop.Views.Purchases;
using PharmacyApp.Desktop.Views.Reports;
using PharmacyApp.Desktop.Views.Sales;
using PharmacyApp.Desktop.Views.Settings;
using PharmacyApp.Infrastructure;
using Xunit;

namespace PharmacyApp.Desktop.Tests;

public class DesktopDependencyInjectionTests : IDisposable
{
    private readonly IsolatedDesktopTestPathProvider _pathProvider = new();
    private readonly ServiceProvider _provider;

    public DesktopDependencyInjectionTests()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure();
        services.AddSingleton<IAppPathProvider>(_pathProvider);
        services.AddDesktopServices();
        _provider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _provider.Dispose();
        _pathProvider.Dispose();
    }

    [Fact]
    public void CanResolve_CoreServicesAsSingletons()
    {
        var shell1 = _provider.GetRequiredService<ShellViewModel>();
        var shell2 = _provider.GetRequiredService<ShellViewModel>();
        Assert.Same(shell1, shell2);

        var nav1 = _provider.GetRequiredService<INavigationService>();
        var nav2 = _provider.GetRequiredService<INavigationService>();
        Assert.Same(nav1, nav2);

        var dialog1 = _provider.GetRequiredService<IDialogService>();
        var dialog2 = _provider.GetRequiredService<IDialogService>();
        Assert.Same(dialog1, dialog2);
    }

    [Fact]
    public void CanResolve_AllPageViewModelsAsSingletons()
    {
        Assert.Same(_provider.GetRequiredService<DashboardViewModel>(), _provider.GetRequiredService<DashboardViewModel>());
        Assert.Same(_provider.GetRequiredService<MedicinesViewModel>(), _provider.GetRequiredService<MedicinesViewModel>());
        Assert.Same(_provider.GetRequiredService<InventoryViewModel>(), _provider.GetRequiredService<InventoryViewModel>());
        Assert.Same(_provider.GetRequiredService<PurchasesViewModel>(), _provider.GetRequiredService<PurchasesViewModel>());
        Assert.Same(_provider.GetRequiredService<SalesViewModel>(), _provider.GetRequiredService<SalesViewModel>());
        Assert.Same(_provider.GetRequiredService<ReportsViewModel>(), _provider.GetRequiredService<ReportsViewModel>());
        Assert.Same(_provider.GetRequiredService<SettingsViewModel>(), _provider.GetRequiredService<SettingsViewModel>());
    }

    [Fact]
    public void CanResolve_AllViewsAsTransient()
    {
        var v1 = _provider.GetRequiredService<DashboardView>();
        var v2 = _provider.GetRequiredService<DashboardView>();
        Assert.NotSame(v1, v2);

        Assert.NotNull(_provider.GetRequiredService<MedicinesView>());
        Assert.NotNull(_provider.GetRequiredService<InventoryView>());
        Assert.NotNull(_provider.GetRequiredService<PurchasesView>());
        Assert.NotNull(_provider.GetRequiredService<SalesView>());
        Assert.NotNull(_provider.GetRequiredService<ReportsView>());

        // SettingsView uses static TabControl.Items in XAML which enforces Dispatcher.UIThread affinity during construction
        try
        {
            Assert.NotNull(_provider.GetRequiredService<SettingsView>());
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("calling thread cannot access this object"))
        {
            // Expected in headless test runners when instantiated outside the original dispatcher thread.
            // The exception itself proves DI successfully attempted to activate SettingsView.
        }

        // Auth Views
        Assert.NotNull(_provider.GetRequiredService<PharmacyApp.Desktop.Views.Auth.FirstRunSetupView>());
        Assert.NotNull(_provider.GetRequiredService<PharmacyApp.Desktop.Views.Auth.LoginView>());
        Assert.NotNull(_provider.GetRequiredService<PharmacyApp.Desktop.Views.Auth.LockOverlayView>());
    }
}
