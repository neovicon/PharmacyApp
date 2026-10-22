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
    public static async Task Main(string[] args)
    {
        // Check for CLI maintenance flags (e.g. password reset for admin)
        if (args.Length >= 2 && (args[0] == "--reset-admin" || args[0] == "--reset-password"))
        {
            await HandlePasswordResetAsync(args);
            return;
        }

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

    private static async Task HandlePasswordResetAsync(string[] args)
    {
        try
        {
            using IHost host = CreateHost(args);
            var initializer = host.Services.GetRequiredService<PharmacyApp.Infrastructure.Persistence.SqliteDatabaseInitializer>();
            await initializer.InitializeAsync();

            var userRepo = host.Services.GetRequiredService<IUserRepository>();
            var hasher = host.Services.GetRequiredService<IPasswordHasher>();
            var dateProvider = host.Services.GetRequiredService<IDateTimeProvider>();

            string targetUsername = args.Length >= 3 && args[0] == "--reset-password" ? args[1] : "admin";
            string newPassword = args.Length >= 3 && args[0] == "--reset-password" ? args[2] : args[1];

            var user = await userRepo.GetByUsernameAsync(targetUsername);
            if (user is null)
            {
                var allUsers = await userRepo.ListAsync();
                user = allUsers.FirstOrDefault(u => u.Role == Domain.Enums.UserRole.Admin) ?? allUsers.FirstOrDefault();
            }

            if (user is null)
            {
                Console.WriteLine("[ERROR] No user found in database to reset.");
                return;
            }

            string newHash = hasher.HashPassword(newPassword);
            user.UpdatePassword(newHash, dateProvider.UtcNow);
            await userRepo.UpdateAsync(user);
            Console.WriteLine($"[SUCCESS] Password for user '{user.Username}' has been reset to: {newPassword}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to reset password: {ex.Message}");
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
