using Microsoft.Extensions.DependencyInjection;
using PharmacyApp.Desktop.Navigation;
using PharmacyApp.Desktop.ViewModels;
using PharmacyApp.Desktop.ViewModels.Dashboard;
using PharmacyApp.Desktop.ViewModels.Medicines;
using PharmacyApp.Desktop.ViewModels.Inventory;
using PharmacyApp.Desktop.ViewModels.Purchases;
using PharmacyApp.Desktop.ViewModels.Sales;
using PharmacyApp.Desktop.ViewModels.Reports;
using PharmacyApp.Desktop.ViewModels.Settings;
using Xunit;

using PharmacyApp.Application.Abstractions;
using PharmacyApp.Infrastructure;

namespace PharmacyApp.Desktop.Tests;

public class NavigationServiceTests : IDisposable
{
    private readonly IsolatedDesktopTestPathProvider _pathProvider = new();
    private readonly ServiceProvider _serviceProvider;
    private readonly NavigationService _navigationService;

    public NavigationServiceTests()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure();
        services.AddSingleton<IAppPathProvider>(_pathProvider);
        services.AddDesktopServices();
        _serviceProvider = services.BuildServiceProvider();
        _navigationService = (NavigationService)_serviceProvider.GetRequiredService<INavigationService>();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        _pathProvider.Dispose();
    }

    [Fact]
    public void InitialPage_IsDashboardViewModel()
    {
        Assert.NotNull(_navigationService.CurrentPage);
        Assert.IsType<DashboardViewModel>(_navigationService.CurrentPage);
    }

    [Fact]
    public void NavigateTo_ChangesCurrentPageAndRaisesEvent()
    {
        ViewModelBase? raisedPage = null;
        _navigationService.NavigationChanged += (_, page) => raisedPage = page;

        _navigationService.NavigateTo<MedicinesViewModel>();

        Assert.IsType<MedicinesViewModel>(_navigationService.CurrentPage);
        Assert.Same(_navigationService.CurrentPage, raisedPage);
    }

    [Fact]
    public void NavigateTo_SamePageDoesNotRaiseDuplicateEvent()
    {
        int eventCount = 0;
        _navigationService.NavigationChanged += (_, _) => eventCount++;

        // Initial page is already Dashboard, navigate to Dashboard again
        _navigationService.NavigateTo<DashboardViewModel>();

        Assert.Equal(0, eventCount);
    }

    [Theory]
    [InlineData(typeof(DashboardViewModel))]
    [InlineData(typeof(MedicinesViewModel))]
    [InlineData(typeof(InventoryViewModel))]
    [InlineData(typeof(PurchasesViewModel))]
    [InlineData(typeof(SalesViewModel))]
    [InlineData(typeof(ReportsViewModel))]
    [InlineData(typeof(SettingsViewModel))]
    public void CanNavigateTo_AllPlaceholderPages(Type viewModelType)
    {
        var method = typeof(NavigationService).GetMethod(nameof(NavigationService.NavigateTo))!
            .MakeGenericMethod(viewModelType);

        method.Invoke(_navigationService, null);

        Assert.Equal(viewModelType, _navigationService.CurrentPage.GetType());
    }
}
