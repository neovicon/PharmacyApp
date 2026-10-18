using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PharmacyApp.Desktop.Navigation;
using PharmacyApp.Desktop.ViewModels.Dashboard;
using PharmacyApp.Desktop.ViewModels.Inventory;
using PharmacyApp.Desktop.ViewModels.Medicines;
using PharmacyApp.Desktop.ViewModels.Purchases;
using PharmacyApp.Desktop.ViewModels.Reports;
using PharmacyApp.Desktop.ViewModels.Sales;
using PharmacyApp.Desktop.ViewModels.Settings;
using System.Collections.ObjectModel;

namespace PharmacyApp.Desktop.ViewModels.Shell;

/// <summary>
/// Root ViewModel for the application shell.
/// Owns the sidebar navigation items, active page ContentControl binding,
/// and synchronizes the visual selection highlight across navigation items.
/// </summary>
public sealed partial class ShellViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private ViewModelBase _currentPage = null!;

    /// <summary>
    /// Navigation items displayed in the sidebar.
    /// Each item holds a label, icon glyph, target VM type, and navigation command.
    /// </summary>
    public ObservableCollection<NavItemViewModel> NavItems { get; }

    public ShellViewModel(INavigationService navigation)
    {
        _navigation = navigation;
        _currentPage = _navigation.CurrentPage;

        NavItems = BuildNavItems();
        UpdateActiveNavItem(_currentPage);

        _navigation.NavigationChanged += (_, page) =>
        {
            CurrentPage = page;
            UpdateActiveNavItem(page);
        };
    }

    private void UpdateActiveNavItem(ViewModelBase activePage)
    {
        Type activeType = activePage.GetType();
        foreach (var item in NavItems)
        {
            item.IsSelected = (item.TargetViewModelType == activeType);
        }
    }

    private ObservableCollection<NavItemViewModel> BuildNavItems()
    {
        return
        [
            new NavItemViewModel("Dashboard", "⊞", typeof(DashboardViewModel), new RelayCommand(() => _navigation.NavigateTo<DashboardViewModel>())),
            new NavItemViewModel("Medicines", "💊", typeof(MedicinesViewModel), new RelayCommand(() => _navigation.NavigateTo<MedicinesViewModel>())),
            new NavItemViewModel("Inventory", "📦", typeof(InventoryViewModel), new RelayCommand(() => _navigation.NavigateTo<InventoryViewModel>())),
            new NavItemViewModel("Purchases", "🛒", typeof(PurchasesViewModel), new RelayCommand(() => _navigation.NavigateTo<PurchasesViewModel>())),
            new NavItemViewModel("Sales",     "🧾", typeof(SalesViewModel),     new RelayCommand(() => _navigation.NavigateTo<SalesViewModel>())),
            new NavItemViewModel("Reports",   "📊", typeof(ReportsViewModel),   new RelayCommand(() => _navigation.NavigateTo<ReportsViewModel>())),
            new NavItemViewModel("Settings",  "⚙",  typeof(SettingsViewModel),  new RelayCommand(() => _navigation.NavigateTo<SettingsViewModel>())),
        ];
    }
}
