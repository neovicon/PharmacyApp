using Microsoft.Extensions.DependencyInjection;
using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Authentication;
using PharmacyApp.Application.Authentication.Commands;
using PharmacyApp.Application.Common;
using PharmacyApp.Application.Medicines;
using PharmacyApp.Application.Medicines.Commands;
using PharmacyApp.Application.Medicines.Queries;
using PharmacyApp.Application.Inventory;
using PharmacyApp.Application.Inventory.Commands;
using PharmacyApp.Application.Inventory.Queries;
using PharmacyApp.Application.Sales;
using PharmacyApp.Application.Sales.Commands;
using PharmacyApp.Application.Sales.Queries;
using PharmacyApp.Application.Dashboard;
using PharmacyApp.Application.Reports;
using PharmacyApp.Application.Settings;
using PharmacyApp.Application.Settings.Commands;
using PharmacyApp.Application.Settings.Queries;
using PharmacyApp.Application.Users;
using PharmacyApp.Application.Users.Commands;
using PharmacyApp.Application.Users.Queries;
using PharmacyApp.Application.Suppliers;
using PharmacyApp.Application.Suppliers.Commands;
using PharmacyApp.Application.Suppliers.Queries;
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
            return $"Data Source={dbPath};Cache=Shared;Foreign Keys=True;Default Timeout=5;";
        });

        // SQLite backup service
        services.AddSingleton<ISqliteBackupService, SqliteBackupService>();

        // SQLite schema initializer
        services.AddSingleton(sp =>
        {
            string connectionString = sp.GetRequiredService<string>();
            var backupService = sp.GetRequiredService<ISqliteBackupService>();
            return new SqliteDatabaseInitializer(connectionString, backupService);
        });

        // User repository
        services.AddSingleton<IUserRepository>(sp =>
        {
            string connectionString = sp.GetRequiredService<string>();
            return new SqliteUserRepository(connectionString);
        });

        // Medicine, Batch, and StockAdjustment repositories
        services.AddSingleton<IMedicineRepository>(sp =>
        {
            string connectionString = sp.GetRequiredService<string>();
            return new SqliteMedicineRepository(connectionString);
        });

        services.AddSingleton<IBatchRepository>(sp =>
        {
            string connectionString = sp.GetRequiredService<string>();
            return new SqliteBatchRepository(connectionString);
        });

        services.AddSingleton<IStockAdjustmentRepository>(sp =>
        {
            string connectionString = sp.GetRequiredService<string>();
            return new SqliteStockAdjustmentRepository(connectionString);
        });

        services.AddSingleton<ISupplierRepository>(sp =>
        {
            string connectionString = sp.GetRequiredService<string>();
            return new SqliteSupplierRepository(connectionString);
        });

        services.AddSingleton<IInvoiceRepository>(sp =>
        {
            string connectionString = sp.GetRequiredService<string>();
            return new SqliteInvoiceRepository(connectionString);
        });

        services.AddSingleton<IReceiptPrinterService, ReceiptPrinterService>();

        // Transaction runner
        services.AddSingleton<ITransactionRunner>(sp =>
        {
            string connectionString = sp.GetRequiredService<string>();
            return new SqliteTransactionRunner(connectionString);
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

        // Medicine validator
        services.AddSingleton<IMedicineValidator, MedicineValidator>();

        // Medicine command & query handlers
        services.AddTransient<ICommandHandler<CreateMedicineCommand, Guid>, CreateMedicineCommandHandler>();
        services.AddTransient<ICommandHandler<UpdateMedicineCommand>, UpdateMedicineCommandHandler>();
        services.AddTransient<IQueryHandler<GetMedicineByIdQuery, MedicineDto>, GetMedicineByIdQueryHandler>();
        services.AddTransient<IQueryHandler<GetMedicineByBarcodeQuery, MedicineDto>, GetMedicineByBarcodeQueryHandler>();
        services.AddTransient<IQueryHandler<SearchMedicinesQuery, IReadOnlyList<MedicineDto>>, SearchMedicinesQueryHandler>();
        services.AddTransient<IQueryHandler<ListMedicinesQuery, IReadOnlyList<MedicineDto>>, ListMedicinesQueryHandler>();

        // Inventory command & query handlers
        services.AddTransient<ICommandHandler<ReceiveBatchCommand, Guid>, ReceiveBatchCommandHandler>();
        services.AddTransient<ICommandHandler<AdjustStockCommand>, AdjustStockCommandHandler>();
        services.AddTransient<IQueryHandler<GetBatchesByMedicineIdQuery, IReadOnlyList<BatchDto>>, GetBatchesByMedicineIdQueryHandler>();
        services.AddTransient<IQueryHandler<ListInventoryBatchesQuery, IReadOnlyList<BatchDto>>, ListInventoryBatchesQueryHandler>();
        services.AddTransient<IQueryHandler<GetRecentStockAdjustmentsQuery, IReadOnlyList<StockAdjustmentDto>>, GetRecentStockAdjustmentsQueryHandler>();

        // Sales command & query handlers
        services.AddTransient<ICommandHandler<ProcessSaleCommand, SaleReceiptDto>, ProcessSaleCommandHandler>();
        services.AddTransient<IQueryHandler<GetRecentInvoicesQuery, IReadOnlyList<InvoiceDto>>, GetRecentInvoicesQueryHandler>();

        // Dashboard & Reports query handlers
        services.AddTransient<IQueryHandler<GetDashboardSummaryQuery, DashboardSummaryDto>, GetDashboardSummaryQueryHandler>();
        services.AddTransient<IQueryHandler<GetInventoryValuationQuery, InventoryValuationReportDto>, GetInventoryValuationQueryHandler>();

        // Settings & Database Management services
        services.AddSingleton<IPharmacyProfileService, JsonPharmacyProfileService>();
        services.AddSingleton<IDatabaseManagementService, SqliteDatabaseManagementService>();
        services.AddTransient<IQueryHandler<GetDatabaseStatusQuery, DatabaseStatusDto>, GetDatabaseStatusQueryHandler>();
        services.AddTransient<ICommandHandler<CreateManualBackupCommand, BackupInfoDto>, CreateManualBackupCommandHandler>();

        // User management query & command handlers
        services.AddTransient<IQueryHandler<ListUsersQuery, IReadOnlyList<UserDto>>, ListUsersQueryHandler>();
        services.AddTransient<ICommandHandler<CreateUserCommand, Guid>, CreateUserCommandHandler>();
        services.AddTransient<ICommandHandler<ToggleUserActiveCommand>, ToggleUserActiveCommandHandler>();
        services.AddTransient<ICommandHandler<ResetUserPasswordCommand>, ResetUserPasswordCommandHandler>();

        // Supplier management query & command handlers
        services.AddTransient<IQueryHandler<ListSuppliersQuery, IReadOnlyList<SupplierDto>>, ListSuppliersQueryHandler>();
        services.AddTransient<ICommandHandler<CreateSupplierCommand, Guid>, CreateSupplierCommandHandler>();
        services.AddTransient<ICommandHandler<ToggleSupplierActiveCommand>, ToggleSupplierActiveCommandHandler>();

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
