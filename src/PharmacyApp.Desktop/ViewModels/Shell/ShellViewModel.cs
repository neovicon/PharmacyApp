using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Authentication;
using PharmacyApp.Application.Authentication.Commands;
using PharmacyApp.Application.Common;
using PharmacyApp.Desktop.Navigation;
using PharmacyApp.Desktop.ViewModels.Auth;
using PharmacyApp.Desktop.ViewModels.Dashboard;
using PharmacyApp.Desktop.ViewModels.Inventory;
using PharmacyApp.Desktop.ViewModels.Medicines;
using PharmacyApp.Desktop.ViewModels.Purchases;
using PharmacyApp.Desktop.ViewModels.Reports;
using PharmacyApp.Desktop.ViewModels.Sales;
using PharmacyApp.Desktop.ViewModels.Settings;
using System.Collections.ObjectModel;

namespace PharmacyApp.Desktop.ViewModels.Shell;

public enum ShellState
{
    FirstRunSetup,
    Login,
    Authenticated,
    Locked
}

/// <summary>
/// Root ViewModel orchestrating authentication lifecycle, inactivity locking,
/// sidebar navigation, and page presentation.
/// </summary>
public sealed partial class ShellViewModel : ViewModelBase, IDisposable
{
    private readonly INavigationService _navigation;
    private readonly ISessionManager _sessionManager;
    private readonly IIdleTimerService _idleTimer;
    private readonly IUserRepository _userRepository;
    private readonly ICommandHandler<CreateInitialAdminCommand, Guid> _createAdminHandler;
    private readonly ICommandHandler<LoginCommand, UserSessionDto> _loginHandler;

    [ObservableProperty]
    private ShellState _shellState = ShellState.Login;

    [ObservableProperty]
    private ViewModelBase? _authViewModel;

    [ObservableProperty]
    private LockOverlayViewModel? _lockOverlayViewModel;

    [ObservableProperty]
    private ViewModelBase _currentPage = null!;

    public ObservableCollection<NavItemViewModel> NavItems { get; }

    public string UserDisplayName => _sessionManager.DisplayName;
    public string UserRoleName => _sessionManager.Role.ToString();

    public ShellViewModel(
        INavigationService navigation,
        ISessionManager sessionManager,
        IIdleTimerService idleTimer,
        IUserRepository userRepository,
        ICommandHandler<CreateInitialAdminCommand, Guid> createAdminHandler,
        ICommandHandler<LoginCommand, UserSessionDto> loginHandler)
    {
        _navigation = navigation;
        _sessionManager = sessionManager;
        _idleTimer = idleTimer;
        _userRepository = userRepository;
        _createAdminHandler = createAdminHandler;
        _loginHandler = loginHandler;

        _currentPage = _navigation.CurrentPage;
        NavItems = BuildNavItems();
        UpdateActiveNavItem(_currentPage);

        _navigation.NavigationChanged += OnNavigationChanged;
        _sessionManager.SessionStateChanged += OnSessionStateChanged;
        _idleTimer.InactivityDetected += OnInactivityDetected;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        bool hasUsers = await _userRepository.HasAnyUsersAsync(cancellationToken);
        if (!hasUsers)
        {
            ShellState = ShellState.FirstRunSetup;
            AuthViewModel = new FirstRunSetupViewModel(_createAdminHandler, _sessionManager, OnAuthenticated);
        }
        else if (!_sessionManager.IsAuthenticated)
        {
            ShellState = ShellState.Login;
            AuthViewModel = new LoginViewModel(_loginHandler, _sessionManager, OnAuthenticated);
        }
        else
        {
            ShellState = ShellState.Authenticated;
            _idleTimer.Start();
        }
    }

    public void RecordActivity()
    {
        if (ShellState == ShellState.Authenticated)
        {
            _idleTimer.RecordActivity();
        }
    }

    [RelayCommand]
    private void Lock()
    {
        _sessionManager.LockSession();
    }

    [RelayCommand]
    private void Logout()
    {
        _sessionManager.TerminateSession();
    }

    private void OnAuthenticated()
    {
        ShellState = ShellState.Authenticated;
        AuthViewModel = null;
        _idleTimer.Start();
        NotifyUserProperties();
    }

    private void OnUnlocked()
    {
        ShellState = ShellState.Authenticated;
        LockOverlayViewModel = null;
        _idleTimer.Start();
        NotifyUserProperties();
    }

    private void OnNavigationChanged(object? sender, ViewModelBase page)
    {
        CurrentPage = page;
        UpdateActiveNavItem(page);
    }

    private void OnSessionStateChanged(object? sender, SessionStateChangedEventArgs e)
    {
        switch (e.State)
        {
            case SessionState.Active:
                ShellState = ShellState.Authenticated;
                AuthViewModel = null;
                LockOverlayViewModel = null;
                _idleTimer.Start();
                NotifyUserProperties();
                break;

            case SessionState.Locked:
                ShellState = ShellState.Locked;
                _idleTimer.Stop();
                LockOverlayViewModel = new LockOverlayViewModel(_sessionManager, OnUnlocked);
                break;

            case SessionState.Unauthenticated:
                _idleTimer.Stop();
                ShellState = ShellState.Login;
                LockOverlayViewModel = null;
                AuthViewModel = new LoginViewModel(_loginHandler, _sessionManager, OnAuthenticated);
                NotifyUserProperties();
                break;
        }
    }

    private void OnInactivityDetected(object? sender, EventArgs e)
    {
        _sessionManager.LockSession();
    }

    private void NotifyUserProperties()
    {
        OnPropertyChanged(nameof(UserDisplayName));
        OnPropertyChanged(nameof(UserRoleName));
    }

    private void UpdateActiveNavItem(ViewModelBase activePage)
    {
        Type activeType = activePage.GetType();
        foreach (var item in NavItems)
        {
            item.IsSelected = (item.TargetViewModelType == activeType);
        }
    }

    private ObservableCollection<NavItemViewModel> BuildNavItems()
    {
        return
        [
            new NavItemViewModel("Dashboard", "⊞", typeof(DashboardViewModel), new RelayCommand(() => _navigation.NavigateTo<DashboardViewModel>())),
            new NavItemViewModel("Medicines", "💊", typeof(MedicinesViewModel), new RelayCommand(() => _navigation.NavigateTo<MedicinesViewModel>())),
            new NavItemViewModel("Inventory", "📦", typeof(InventoryViewModel), new RelayCommand(() => _navigation.NavigateTo<InventoryViewModel>())),
            new NavItemViewModel("Purchases", "🛒", typeof(PurchasesViewModel), new RelayCommand(() => _navigation.NavigateTo<PurchasesViewModel>())),
            new NavItemViewModel("Sales",     "🧾", typeof(SalesViewModel),     new RelayCommand(() => _navigation.NavigateTo<SalesViewModel>())),
            new NavItemViewModel("Reports",   "📊", typeof(ReportsViewModel),   new RelayCommand(() => _navigation.NavigateTo<ReportsViewModel>())),
            new NavItemViewModel("Settings",  "⚙",  typeof(SettingsViewModel),  new RelayCommand(() => _navigation.NavigateTo<SettingsViewModel>())),
        ];
    }

    public void Dispose()
    {
        _navigation.NavigationChanged -= OnNavigationChanged;
        _sessionManager.SessionStateChanged -= OnSessionStateChanged;
        _idleTimer.InactivityDetected -= OnInactivityDetected;
    }
}
