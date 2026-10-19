using Microsoft.Extensions.DependencyInjection;
using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Authentication;
using PharmacyApp.Application.Authentication.Commands;
using PharmacyApp.Application.Common;
using PharmacyApp.Infrastructure.Persistence;
using PharmacyApp.Infrastructure.Security;
using PharmacyApp.Infrastructure.Services;
using Serilog;

namespace PharmacyApp.Infrastructure;

/// <summary>
/// Registers all Infrastructure services into the DI container.
/// Called once from the Desktop composition root.
/// </summary>
public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string? customConnectionString = null)
    {
        // Core providers
        services.AddSingleton<IAppPathProvider, AppPathProvider>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        // Security
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();

        // SQLite connection string
        services.AddSingleton(sp =>
        {
            if (!string.IsNullOrEmpty(customConnectionString))
                return customConnectionString;

            var pathProvider = sp.GetRequiredService<IAppPathProvider>();
            string dbPath = Path.Combine(pathProvider.DataDirectory, "pharmacy.db");
            return $"Data Source={dbPath};Cache=Shared;";
        });

        // SQLite schema initializer
        services.AddSingleton(sp =>
        {
            string connectionString = sp.GetRequiredService<string>();
            return new SqliteDatabaseInitializer(connectionString);
        });

        // User repository
        services.AddSingleton<IUserRepository>(sp =>
        {
            string connectionString = sp.GetRequiredService<string>();
            return new SqliteUserRepository(connectionString);
        });

        // Session manager (singleton serving both ISessionManager and ICurrentUserService)
        services.AddSingleton<SessionManager>();
        services.AddSingleton<ISessionManager>(sp => sp.GetRequiredService<SessionManager>());
        services.AddSingleton<ICurrentUserService>(sp => sp.GetRequiredService<SessionManager>());

        // Idle timer service
        services.AddSingleton<IIdleTimerService, IdleTimerService>();

        // Login failed-attempt rate limiter & progressive cooldown throttle
        services.AddSingleton<ILoginThrottleService, LoginThrottleService>();

        // Direct command handlers (no MediatR)
        services.AddTransient<ICommandHandler<CreateInitialAdminCommand, Guid>, CreateInitialAdminCommandHandler>();
        services.AddTransient<ICommandHandler<LoginCommand, UserSessionDto>, LoginCommandHandler>();

        return services;
    }

    /// <summary>
    /// Configures Serilog using resolved path information.
    /// Must be called after the DI container is built so that
    /// IAppPathProvider can be resolved.
    ///
    /// Log files are written to: {DataDirectory}/Logs/pharmacy-YYYYMMDD.log
    /// Retained for 30 days. Async wrapper avoids blocking the UI thread.
    /// </summary>
    public static void ConfigureSerilog(IAppPathProvider pathProvider, string appVersion)
    {
        string logTemplate =
            "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

        string logPath = Path.Combine(pathProvider.LogDirectory, "pharmacy-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
#if DEBUG
            .MinimumLevel.Debug()
#endif
            .Enrich.FromLogContext()
            .WriteTo.Async(a =>
                a.File(
                    logPath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    outputTemplate: logTemplate,
                    shared: false))
            .CreateLogger();

        Log.Information(
            "PharmacyApp Desktop starting. Version={Version} Environment={Environment}",
            appVersion,
            GetEnvironmentName());
    }

    private static string GetEnvironmentName()
    {
#if DEBUG
        return "Development";
#else
        return "Production";
#endif
    }
}
