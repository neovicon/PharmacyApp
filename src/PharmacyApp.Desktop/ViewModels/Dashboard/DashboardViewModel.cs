using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PharmacyApp.Application.Common;
using PharmacyApp.Application.Dashboard;
using PharmacyApp.Desktop.Navigation;
using PharmacyApp.Desktop.ViewModels.Inventory;
using PharmacyApp.Desktop.ViewModels.Medicines;
using PharmacyApp.Desktop.ViewModels.Purchases;
using PharmacyApp.Desktop.ViewModels.Sales;

namespace PharmacyApp.Desktop.ViewModels.Dashboard;

public sealed partial class DashboardViewModel : ViewModelBase, INavigationAware
{
    private readonly IQueryHandler<GetDashboardSummaryQuery, DashboardSummaryDto> _dashboardHandler;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private DashboardSummaryDto? _summary;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    public DashboardViewModel()
    {
        _dashboardHandler = null!;
        _navigationService = null!;
    }

    public DashboardViewModel(
        IQueryHandler<GetDashboardSummaryQuery, DashboardSummaryDto> dashboardHandler,
        INavigationService navigationService)
    {
        _dashboardHandler = dashboardHandler;
        _navigationService = navigationService;

        _ = LoadDashboardAsync();
    }

    public Task OnNavigatedToAsync() => LoadDashboardAsync();

    [RelayCommand]
    public async Task LoadDashboardAsync()
    {
        if (_dashboardHandler == null) return;

        IsBusy = true;
        try
        {
            var res = await _dashboardHandler.HandleAsync(new GetDashboardSummaryQuery());
            if (res.IsSuccess)
            {
                Summary = res.Value;
                StatusMessage = "Dashboard up to date.";
            }
            else
            {
                StatusMessage = $"Failed to load dashboard: {res.Error}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public void GoToSales() => _navigationService?.NavigateTo<SalesViewModel>();

    [RelayCommand]
    public void GoToPurchases() => _navigationService?.NavigateTo<PurchasesViewModel>();

    [RelayCommand]
    public void GoToMedicines() => _navigationService?.NavigateTo<MedicinesViewModel>();

    [RelayCommand]
    public void GoToInventory() => _navigationService?.NavigateTo<InventoryViewModel>();
}
