namespace PharmacyApp.Application.Abstractions;

/// <summary>
/// Abstraction for user-facing dialogs, alerts, and confirmations.
/// Keeps ViewModels decoupled from Avalonia UI framework dialog controls
/// and enables automated unit testing without UI dependencies.
/// </summary>
public interface IDialogService
{
    /// <summary>Displays an informational alert dialog to the user.</summary>
    Task ShowMessageAsync(string title, string message);

    /// <summary>Displays a confirmation dialog returning true if confirmed.</summary>
    Task<bool> ShowConfirmationAsync(string title, string message);

    /// <summary>Displays an error message dialog to the user.</summary>
    Task ShowErrorAsync(string title, string message);
}
