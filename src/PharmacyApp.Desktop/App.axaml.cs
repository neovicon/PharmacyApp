using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using PharmacyApp.Desktop.ViewModels.Shell;
using PharmacyApp.Desktop.Views.Shell;

namespace PharmacyApp.Desktop;

public partial class App : Avalonia.Application
{
    /// <summary>
    /// Set by Program.cs before Avalonia initializes so that the App and
    /// ViewLocator can resolve services without a static service locator pattern.
    /// This is an accepted pattern for desktop apps that bootstrap DI outside
    /// the UI framework lifecycle.
    /// </summary>
    public static IServiceProvider ServiceProvider { get; set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            ShellViewModel shellViewModel = ServiceProvider.GetRequiredService<ShellViewModel>();
            desktop.MainWindow = new ShellWindow
            {
                DataContext = shellViewModel,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}