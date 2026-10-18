using Microsoft.Extensions.DependencyInjection;
using PharmacyApp.Desktop.Navigation;
using PharmacyApp.Desktop.ViewModels.Dashboard;
using PharmacyApp.Desktop.ViewModels.Inventory;
using PharmacyApp.Desktop.ViewModels.Medicines;
using PharmacyApp.Desktop.ViewModels.Purchases;
using PharmacyApp.Desktop.ViewModels.Reports;
using PharmacyApp.Desktop.ViewModels.Sales;
using PharmacyApp.Desktop.ViewModels.Settings;
using PharmacyApp.Desktop.ViewModels.Shell;
using PharmacyApp.Desktop.Views.Dashboard;
using PharmacyApp.Desktop.Views.Inventory;
using PharmacyApp.Desktop.Views.Medicines;
using PharmacyApp.Desktop.Views.Purchases;
using PharmacyApp.Desktop.Views.Reports;
using PharmacyApp.Desktop.Views.Sales;
using PharmacyApp.Desktop.Views.Settings;
using PharmacyApp.Desktop.Views.Shell;

namespace PharmacyApp.Desktop;

/// <summary>
/// Registers all Desktop-layer services (ViewModels, Views, Navigation)
/// into the DI container.
///
/// Lifetime choices:
/// - ShellViewModel: Singleton — there is exactly one shell for the process lifetime.
/// - NavigationService: Singleton — it holds CurrentPage state.
/// - Page ViewModels: Singleton — preserves page state across navigation.
///   Change to Transient if a page should always start fresh.
/// - Views: Transient — re-created each time ViewLocator resolves them.
/// </summary>
public static class DesktopServiceExtensions
{
    public static IServiceCollection AddDesktopServices(this IServiceCollection services)
    {
        // Dialog service
        services.AddSingleton<PharmacyApp.Application.Abstractions.IDialogService, PharmacyApp.Desktop.Services.DesktopDialogService>();

        // Navigation
        services.AddSingleton<INavigationService, NavigationService>();

        // Shell
        services.AddSingleton<ShellViewModel>();

        // Page ViewModels — singletons preserve state across sidebar navigation
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<MedicinesViewModel>();
        services.AddSingleton<InventoryViewModel>();
        services.AddSingleton<PurchasesViewModel>();
        services.AddSingleton<SalesViewModel>();
        services.AddSingleton<ReportsViewModel>();
        services.AddSingleton<SettingsViewModel>();

        // Views — registered so ViewLocator can resolve them from DI
        services.AddTransient<ShellWindow>();
        services.AddTransient<DashboardView>();
        services.AddTransient<MedicinesView>();
        services.AddTransient<InventoryView>();
        services.AddTransient<PurchasesView>();
        services.AddTransient<SalesView>();
        services.AddTransient<ReportsView>();
        services.AddTransient<SettingsView>();

        return services;
    }
}
