using PharmacyApp.Application.Abstractions;
using Serilog;

namespace PharmacyApp.Desktop.Services;

/// <summary>
/// Desktop implementation of <see cref="IDialogService"/>.
/// Provides dialog logging and a testable extension point for modal UI dialogs
/// in subsequent milestones.
/// </summary>
public sealed class DesktopDialogService : IDialogService
{
    public Task ShowMessageAsync(string title, string message)
    {
        Log.Information("Dialog shown: [{Title}] {Message}", title, message);
        return Task.CompletedTask;
    }

    public Task<bool> ShowConfirmationAsync(string title, string message)
    {
        Log.Information("Confirmation prompt: [{Title}] {Message}", title, message);
        return Task.FromResult(true);
    }

    public Task ShowErrorAsync(string title, string message)
    {
        Log.Warning("Error dialog shown: [{Title}] {Message}", title, message);
        return Task.CompletedTask;
    }
}
