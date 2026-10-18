using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Authentication;
using PharmacyApp.Application.Authentication.Commands;
using PharmacyApp.Application.Common;

namespace PharmacyApp.Desktop.ViewModels.Auth;

/// <summary>
/// ViewModel for user authentication.
/// Provides credentials validation and generic error feedback to prevent enumeration.
/// </summary>
public sealed partial class LoginViewModel : ViewModelBase
{
    private readonly ICommandHandler<LoginCommand, UserSessionDto> _loginHandler;
    private readonly ISessionManager _sessionManager;
    private readonly Action _onLoginSuccessful;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    public LoginViewModel(
        ICommandHandler<LoginCommand, UserSessionDto> loginHandler,
        ISessionManager sessionManager,
        Action onLoginSuccessful)
    {
        _loginHandler = loginHandler;
        _sessionManager = sessionManager;
        _onLoginSuccessful = onLoginSuccessful;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy) return;

        ErrorMessage = null;
        IsBusy = true;

        try
        {
            var command = new LoginCommand(Username, Password);
            var result = await _loginHandler.HandleAsync(command);

            // Always clear the plaintext password from the viewmodel property
            Password = string.Empty;

            if (result.IsFailure)
            {
                ErrorMessage = result.Error;
                return;
            }

            _sessionManager.StartSession(result.Value);
            _onLoginSuccessful.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Authentication failed unexpectedly: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
