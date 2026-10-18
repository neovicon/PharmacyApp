using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PharmacyApp.Application.Abstractions;
using PharmacyApp.Infrastructure;
using Serilog;

namespace PharmacyApp.Desktop;

internal static class Program
{
    // Avalonia requires [STAThread] and must be started from the original thread.
    // We build the IHost first for DI, then hand it to Avalonia.
    [STAThread]
    public static void Main(string[] args)
    {
        // Bootstrap a minimal logger before DI is ready, so startup failures are recorded.
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        try
        {
            using IHost host = CreateHost(args);

            // Configure full Serilog now that IAppPathProvider is available.
            IAppPathProvider paths = host.Services.GetRequiredService<IAppPathProvider>();
            string version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0";
            InfrastructureServiceExtensions.ConfigureSerilog(paths, version);

            // Hand the service provider to App before Avalonia initializes.
            App.ServiceProvider = host.Services;

            // Application-level exception handling hooks
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    Log.Fatal(ex, "Unhandled AppDomain exception. IsTerminating={IsTerminating}", e.IsTerminating);
                }
                else
                {
                    Log.Fatal("Unhandled non-Exception in AppDomain: {Object}", e.ExceptionObject);
                }
            };

            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                Log.Error(e.Exception, "Unobserved TaskScheduler exception.");
                e.SetObserved();
            };

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "PharmacyApp terminated unexpectedly during startup.");
            throw;
        }
        finally
        {
            Log.Information("PharmacyApp shutting down.");
            Log.CloseAndFlush();
        }
    }

    public static IHost CreateHost(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureServices((_, services) =>
            {
                services.AddInfrastructure();
                services.AddDesktopServices();
            })
            .Build();

    // Avalonia configuration — do not remove; also used by the visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
