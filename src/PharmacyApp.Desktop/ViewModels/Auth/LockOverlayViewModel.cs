using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PharmacyApp.Application.Abstractions;
using PharmacyApp.Domain.Enums;

namespace PharmacyApp.Desktop.ViewModels.Auth;

/// <summary>
/// ViewModel for the session lock screen.
/// Requires the active user's password to resume operations,
/// preserving all background UI state and in-progress form entries.
/// </summary>
public sealed partial class LockOverlayViewModel : ViewModelBase
{
    private readonly ISessionManager _sessionManager;
    private readonly Action _onUnlocked;

    public string DisplayName => _sessionManager.CurrentUser?.DisplayName ?? string.Empty;
    public string Username => _sessionManager.CurrentUser?.Username ?? string.Empty;
    public UserRole Role => _sessionManager.CurrentUser?.Role ?? UserRole.Cashier;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    public LockOverlayViewModel(ISessionManager sessionManager, Action onUnlocked)
    {
        _sessionManager = sessionManager;
        _onUnlocked = onUnlocked;
    }

    [RelayCommand]
    private async Task UnlockAsync()
    {
        if (IsBusy) return;

        ErrorMessage = null;
        IsBusy = true;

        try
        {
            string enteredPassword = Password;
            Password = string.Empty;

            var result = await _sessionManager.UnlockSessionAsync(enteredPassword);

            if (result.IsFailure)
            {
                ErrorMessage = result.Error;
                return;
            }

            _onUnlocked.Invoke();
        }
        catch (Exception)
        {
            ErrorMessage = "Unlock service is temporarily unavailable. Please try again.";
        }
        finally
        {
            Password = string.Empty;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Logout()
    {
        Password = string.Empty;
        ErrorMessage = null;
        _sessionManager.TerminateSession();
    }
}
