namespace PharmacyApp.Desktop.Navigation;

/// <summary>
/// Implemented by page ViewModels that need to refresh their data
/// automatically whenever they are navigated to in the shell.
/// </summary>
public interface INavigationAware
{
    Task OnNavigatedToAsync();
}
