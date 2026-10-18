using Microsoft.Extensions.DependencyInjection;
using PharmacyApp.Application.Abstractions;
using PharmacyApp.Infrastructure.Services;
using Serilog;

namespace PharmacyApp.Infrastructure;

/// <summary>
/// Registers all Infrastructure services into the DI container.
/// Called once from the Desktop composition root.
/// </summary>
public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Path provider must be a singleton: the directories are created in the constructor
        // and we want one consistent answer throughout the process lifetime.
        services.AddSingleton<IAppPathProvider, AppPathProvider>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

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
