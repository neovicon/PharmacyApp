using PharmacyApp.Desktop.ViewModels;

namespace PharmacyApp.Desktop.Navigation;

/// <summary>
/// Controls which top-level page is shown in the shell content area.
/// Uses a sidebar + single active page pattern (no back-stack).
/// </summary>
public interface INavigationService
{
    /// <summary>The currently visible page ViewModel.</summary>
    ViewModelBase CurrentPage { get; }

    /// <summary>
    /// Navigate to the page associated with <typeparamref name="TViewModel"/>.
    /// The ViewModel is resolved from DI, so it may be a fresh instance or a
    /// singleton depending on registration lifetime.
    /// </summary>
    void NavigateTo<TViewModel>() where TViewModel : ViewModelBase;

    /// <summary>Raised after CurrentPage changes.</summary>
    event EventHandler<ViewModelBase>? NavigationChanged;
}
