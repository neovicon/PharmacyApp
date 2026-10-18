using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using PharmacyApp.Desktop.ViewModels.Shell;
using PharmacyApp.Desktop.Views.Shell;
using PharmacyApp.Infrastructure.Persistence;

namespace PharmacyApp.Desktop;

public partial class App : Avalonia.Application
{
    public static IServiceProvider ServiceProvider { get; set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Initialize durable SQLite database schema before UI presents
            var dbInitializer = ServiceProvider.GetRequiredService<SqliteDatabaseInitializer>();
            dbInitializer.InitializeAsync().GetAwaiter().GetResult();

            // Resolve ShellViewModel and determine initial screen (First-Run Setup vs Login)
            ShellViewModel shellViewModel = ServiceProvider.GetRequiredService<ShellViewModel>();
            shellViewModel.InitializeAsync().GetAwaiter().GetResult();

            desktop.MainWindow = new ShellWindow
            {
                DataContext = shellViewModel,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}