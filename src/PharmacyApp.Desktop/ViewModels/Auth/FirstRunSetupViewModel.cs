using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Authentication;
using PharmacyApp.Application.Authentication.Commands;
using PharmacyApp.Application.Common;
using PharmacyApp.Domain.Enums;

namespace PharmacyApp.Desktop.ViewModels.Auth;

/// <summary>
/// ViewModel for initial administrator account provisioning.
/// Displayed on first run when no user accounts exist in storage.
/// </summary>
public sealed partial class FirstRunSetupViewModel : ViewModelBase
{
    private readonly ICommandHandler<CreateInitialAdminCommand, Guid> _createAdminHandler;
    private readonly ISessionManager _sessionManager;
    private readonly Action _onSetupCompleted;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    public FirstRunSetupViewModel(
        ICommandHandler<CreateInitialAdminCommand, Guid> createAdminHandler,
        ISessionManager sessionManager,
        Action onSetupCompleted)
    {
        _createAdminHandler = createAdminHandler;
        _sessionManager = sessionManager;
        _onSetupCompleted = onSetupCompleted;
    }

    [RelayCommand]
    private async Task CreateAdministratorAsync()
    {
        if (IsBusy) return;

        ErrorMessage = null;
        IsBusy = true;

        try
        {
            var command = new CreateInitialAdminCommand(Username, DisplayName, Password, ConfirmPassword);
            Password = string.Empty;
            ConfirmPassword = string.Empty;

            var result = await _createAdminHandler.HandleAsync(command);

            if (result.IsFailure)
            {
                ErrorMessage = result.Error;
                return;
            }

            // Provisioning complete: establish initial admin session
            var session = new UserSessionDto(
                UserId: result.Value,
                Username: Username.Trim().ToLowerInvariant(),
                DisplayName: DisplayName.Trim(),
                Role: UserRole.Admin);

            _sessionManager.StartSession(session);
            _onSetupCompleted.Invoke();
        }
        catch (Exception)
        {
            ErrorMessage = "Administrator setup failed due to an unexpected error. Please try again.";
        }
        finally
        {
            Password = string.Empty;
            ConfirmPassword = string.Empty;
            IsBusy = false;
        }
    }
}
