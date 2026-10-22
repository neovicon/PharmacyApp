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
    private ViewModelBase? _currentPage;

    public event EventHandler<ViewModelBase>? NavigationChanged;

    public ViewModelBase CurrentPage
    {
        get
        {
            if (_currentPage == null)
            {
                var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
                _currentPage = vm;
                if (vm is INavigationAware aware)
                {
                    _ = aware.OnNavigatedToAsync();
                }
            }
            return _currentPage;
        }
    }

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase
    {
        ViewModelBase viewModel = _serviceProvider.GetRequiredService<TViewModel>();
        if (ReferenceEquals(CurrentPage, viewModel))
        {
            if (viewModel is INavigationAware sameAware)
            {
                _ = sameAware.OnNavigatedToAsync();
            }
            return;
        }

        _currentPage = viewModel;
        NavigationChanged?.Invoke(this, _currentPage);

        if (viewModel is INavigationAware aware)
        {
            _ = aware.OnNavigatedToAsync();
        }
    }
}
