using Microsoft.Extensions.DependencyInjection;
using PharmacyApp.Desktop.Navigation;
using PharmacyApp.Desktop.ViewModels.Dashboard;
using PharmacyApp.Desktop.ViewModels.Medicines;
using PharmacyApp.Desktop.ViewModels.Shell;
using Xunit;

namespace PharmacyApp.Desktop.Tests;

public class ShellViewModelTests
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ShellViewModel _shellViewModel;
    private readonly INavigationService _navigationService;

    public ShellViewModelTests()
    {
        var services = new ServiceCollection();
        services.AddDesktopServices();
        _serviceProvider = services.BuildServiceProvider();
        _navigationService = _serviceProvider.GetRequiredService<INavigationService>();
        _shellViewModel = _serviceProvider.GetRequiredService<ShellViewModel>();
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
}
