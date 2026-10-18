using Microsoft.Extensions.DependencyInjection;
using PharmacyApp.Desktop.ViewModels;
using PharmacyApp.Desktop.ViewModels.Dashboard;

namespace PharmacyApp.Desktop.Navigation;

/// <summary>
/// Resolves page ViewModels from DI and raises navigation events.
/// ShellViewModel subscribes to NavigationChanged to update its CurrentPage binding.
/// </summary>
public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private ViewModelBase _currentPage;

    public event EventHandler<ViewModelBase>? NavigationChanged;

    public ViewModelBase CurrentPage => _currentPage;

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        // Start on Dashboard.
        _currentPage = _serviceProvider.GetRequiredService<DashboardViewModel>();
    }

    public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase
    {
        ViewModelBase viewModel = _serviceProvider.GetRequiredService<TViewModel>();
        if (ReferenceEquals(_currentPage, viewModel))
            return;

        _currentPage = viewModel;
        NavigationChanged?.Invoke(this, _currentPage);
    }
}
