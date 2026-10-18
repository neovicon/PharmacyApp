using Avalonia.Controls;
using Avalonia.Controls.Templates;
using PharmacyApp.Desktop.ViewModels;

namespace PharmacyApp.Desktop;

/// <summary>
/// Resolves the View for a given ViewModel by:
/// 1. Replacing "ViewModel" with "View" in the fully qualified type name.
/// 2. Resolving the resulting type from the DI container (preferred).
/// 3. In design-time mode where the DI container is not running, falls back to <see cref="Activator.CreateInstance"/>.
///
/// Views are registered as Transient in DI so they are properly recreated on navigation
/// while receiving any injected dependencies.
/// </summary>
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        string viewModelName = param.GetType().FullName!;
        string viewName = viewModelName.Replace("ViewModel", "View", StringComparison.Ordinal);

        Type? viewType = param.GetType().Assembly.GetType(viewName)
            ?? typeof(ViewLocator).Assembly.GetType(viewName)
            ?? Type.GetType(viewName);

        if (viewType is null)
        {
            return new TextBlock { Text = $"View not found: {viewName}" };
        }

        // When DI container is available, resolve from DI to honor registrations and dependencies.
        // If the view is missing from DI, fail clearly instead of silently falling back.
        if (App.ServiceProvider is not null)
        {
            object? resolvedView = App.ServiceProvider.GetService(viewType);
            if (resolvedView is null)
            {
                return new TextBlock { Text = $"View not registered in DI container: {viewName}" };
            }

            return resolvedView as Control
                ?? new TextBlock { Text = $"Resolved type is not an Avalonia Control: {viewName}" };
        }

        // Design-time / unit test fallback when App.ServiceProvider is not initialized
        object? fallback = Activator.CreateInstance(viewType);
        return fallback as Control
            ?? new TextBlock { Text = $"Instantiated type is not an Avalonia Control: {viewName}" };
    }

    public bool Match(object? data) => data is ViewModelBase;
}
