using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Common;
using PharmacyApp.Application.Settings;
using PharmacyApp.Application.Settings.Commands;
using PharmacyApp.Application.Settings.Queries;
using PharmacyApp.Application.Users;
using PharmacyApp.Application.Users.Commands;
using PharmacyApp.Application.Users.Queries;
using PharmacyApp.Domain.Enums;

namespace PharmacyApp.Desktop.ViewModels.Settings;

public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly ISessionManager _sessionManager;
    private readonly IQueryHandler<ListUsersQuery, IReadOnlyList<UserDto>> _listUsersHandler;
    private readonly ICommandHandler<CreateUserCommand, Guid> _createUserHandler;
    private readonly ICommandHandler<ToggleUserActiveCommand> _toggleUserHandler;
    private readonly ICommandHandler<ResetUserPasswordCommand> _resetPasswordHandler;
    private readonly IQueryHandler<GetDatabaseStatusQuery, DatabaseStatusDto> _getDatabaseStatusHandler;
    private readonly ICommandHandler<CreateManualBackupCommand, BackupInfoDto> _createBackupHandler;
    private readonly IPharmacyProfileService _profileService;

    public string PageTitle => "Settings & Administration";

    // Tab Selection: 0 = Staff, 1 = Database & Backups, 2 = Store Profile, 3 = Diagnostics
    [ObservableProperty]
    private int _selectedTabIndex = 0;

    public bool IsAdmin => _sessionManager.Role == UserRole.Admin;

    // --- Tab 0: Staff Management ---
    [ObservableProperty]
    private ObservableCollection<UserDto> _users = new();

    [ObservableProperty]
    private string? _staffStatusMessage;

    [ObservableProperty]
    private bool _isStaffStatusError;

    // Add User Modal State
    [ObservableProperty]
    private bool _isAddUserModalOpen;

    [ObservableProperty]
    private string _newUsername = string.Empty;

    [ObservableProperty]
    private string _newDisplayName = string.Empty;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private UserRole _newRole = UserRole.Pharmacist;

    [ObservableProperty]
    private string? _addUserErrorMessage;

    // Reset Password Modal State
    [ObservableProperty]
    private bool _isResetPasswordModalOpen;

    [ObservableProperty]
    private UserDto? _resetPasswordTargetUser;

    [ObservableProperty]
    private string _resetNewPassword = string.Empty;

    [ObservableProperty]
    private string? _resetPasswordErrorMessage;

    // --- Tab 1: Database & Backups ---
    [ObservableProperty]
    private string _databasePath = string.Empty;

    [ObservableProperty]
    private string _databaseSizeFormatted = "—";

    [ObservableProperty]
    private string _journalMode = "—";

    [ObservableProperty]
    private string _foreignKeysStatus = "—";

    [ObservableProperty]
    private int _backupCount;

    [ObservableProperty]
    private string _totalBackupSizeFormatted = "0 KB";

    [ObservableProperty]
    private string _lastBackupTimeFormatted = "Never";

    [ObservableProperty]
    private ObservableCollection<BackupInfoDto> _backups = new();

    [ObservableProperty]
    private bool _isBackupInProgress;

    [ObservableProperty]
    private string? _backupStatusMessage;

    [ObservableProperty]
    private bool _isBackupError;

    // --- Tab 2: Store Profile ---
    [ObservableProperty]
    private string _storeName = "Community Care Pharmacy";

    [ObservableProperty]
    private string _storeAddress = string.Empty;

    [ObservableProperty]
    private string _storePhone = string.Empty;

    [ObservableProperty]
    private string _taxPanNumber = string.Empty;

    [ObservableProperty]
    private string _receiptFooterMessage = string.Empty;

    [ObservableProperty]
    private int _autoLockMinutes = 15;

    [ObservableProperty]
    private string? _profileStatusMessage;

    [ObservableProperty]
    private bool _isProfileSaved;

    // --- Tab 3: System Diagnostics ---
    public string AppVersion => "1.0.0";
    public string DotNetVersion => RuntimeInformation.FrameworkDescription;
    public string OperatingSystem => RuntimeInformation.OSDescription;
    public string Architecture => RuntimeInformation.ProcessArchitecture.ToString();
    public string MemoryUsage => $"{(GC.GetTotalMemory(false) / (1024.0 * 1024.0)):F1} MB";
    public string Uptime => GetProcessUptime();

    public SettingsViewModel(
        ISessionManager sessionManager,
        IQueryHandler<ListUsersQuery, IReadOnlyList<UserDto>> listUsersHandler,
        ICommandHandler<CreateUserCommand, Guid> createUserHandler,
        ICommandHandler<ToggleUserActiveCommand> toggleUserHandler,
        ICommandHandler<ResetUserPasswordCommand> resetPasswordHandler,
        IQueryHandler<GetDatabaseStatusQuery, DatabaseStatusDto> getDatabaseStatusHandler,
        ICommandHandler<CreateManualBackupCommand, BackupInfoDto> createBackupHandler,
        IPharmacyProfileService profileService)
    {
        _sessionManager = sessionManager;
        _listUsersHandler = listUsersHandler;
        _createUserHandler = createUserHandler;
        _toggleUserHandler = toggleUserHandler;
        _resetPasswordHandler = resetPasswordHandler;
        _getDatabaseStatusHandler = getDatabaseStatusHandler;
        _createBackupHandler = createBackupHandler;
        _profileService = profileService;
    }

    [RelayCommand]
    public async Task LoadAllAsync()
    {
        await LoadUsersAsync();
        await LoadDatabaseStatusAsync();
        await LoadStoreProfileAsync();
        OnPropertyChanged(nameof(IsAdmin));
        OnPropertyChanged(nameof(MemoryUsage));
        OnPropertyChanged(nameof(Uptime));
    }

    // --- Staff Management Commands ---

    [RelayCommand]
    public async Task LoadUsersAsync()
    {
        StaffStatusMessage = null;
        var result = await _listUsersHandler.HandleAsync(new ListUsersQuery());
        if (result.IsSuccess)
        {
            Users = new ObservableCollection<UserDto>(result.Value);
        }
        else
        {
            StaffStatusMessage = result.Error;
            IsStaffStatusError = true;
        }
    }

    [RelayCommand]
    private void OpenAddUserModal()
    {
        NewUsername = string.Empty;
        NewDisplayName = string.Empty;
        NewPassword = string.Empty;
        NewRole = UserRole.Pharmacist;
        AddUserErrorMessage = null;
        IsAddUserModalOpen = true;
    }

    [RelayCommand]
    private void CloseAddUserModal()
    {
        IsAddUserModalOpen = false;
        AddUserErrorMessage = null;
    }

    [RelayCommand]
    public async Task SaveNewUserAsync()
    {
        AddUserErrorMessage = null;

        var cmd = new CreateUserCommand(
            NewUsername,
            NewDisplayName,
            NewPassword,
            NewRole,
            _sessionManager.UserId);

        var result = await _createUserHandler.HandleAsync(cmd);
        if (result.IsSuccess)
        {
            IsAddUserModalOpen = false;
            StaffStatusMessage = $"User '{NewUsername.Trim()}' created successfully.";
            IsStaffStatusError = false;
            await LoadUsersAsync();
        }
        else
        {
            AddUserErrorMessage = result.Error;
        }
    }

    [RelayCommand]
    public async Task ToggleUserActiveAsync(UserDto? user)
    {
        if (user is null) return;
        StaffStatusMessage = null;

        var cmd = new ToggleUserActiveCommand(user.Id, _sessionManager.UserId);
        var result = await _toggleUserHandler.HandleAsync(cmd);
        if (result.IsSuccess)
        {
            string action = user.IsActive ? "deactivated" : "activated";
            StaffStatusMessage = $"User '{user.Username}' was {action}.";
            IsStaffStatusError = false;
            await LoadUsersAsync();
        }
        else
        {
            StaffStatusMessage = result.Error;
            IsStaffStatusError = true;
        }
    }

    [RelayCommand]
    private void OpenResetPasswordModal(UserDto? user)
    {
        if (user is null) return;
        ResetPasswordTargetUser = user;
        ResetNewPassword = string.Empty;
        ResetPasswordErrorMessage = null;
        IsResetPasswordModalOpen = true;
    }

    [RelayCommand]
    private void CloseResetPasswordModal()
    {
        IsResetPasswordModalOpen = false;
        ResetPasswordTargetUser = null;
        ResetPasswordErrorMessage = null;
    }

    [RelayCommand]
    public async Task SaveResetPasswordAsync()
    {
        if (ResetPasswordTargetUser is null) return;
        ResetPasswordErrorMessage = null;

        var cmd = new ResetUserPasswordCommand(
            ResetPasswordTargetUser.Id,
            ResetNewPassword,
            _sessionManager.UserId);

        var result = await _resetPasswordHandler.HandleAsync(cmd);
        if (result.IsSuccess)
        {
            string username = ResetPasswordTargetUser.Username;
            IsResetPasswordModalOpen = false;
            ResetPasswordTargetUser = null;
            StaffStatusMessage = $"Password for '{username}' was updated successfully.";
            IsStaffStatusError = false;
        }
        else
        {
            ResetPasswordErrorMessage = result.Error;
        }
    }

    // --- Database & Backups Commands ---

    [RelayCommand]
    public async Task LoadDatabaseStatusAsync()
    {
        var result = await _getDatabaseStatusHandler.HandleAsync(new GetDatabaseStatusQuery());
        if (result.IsSuccess)
        {
            var status = result.Value;
            DatabasePath = status.DatabasePath;
            DatabaseSizeFormatted = FormatBytes(status.DatabaseSizeBytes);
            JournalMode = status.JournalMode;
            ForeignKeysStatus = status.ForeignKeysEnabled ? "Enforced (ON)" : "Disabled (OFF)";
            BackupCount = status.BackupCount;
            TotalBackupSizeFormatted = FormatBytes(status.TotalBackupSizeBytes);
            LastBackupTimeFormatted = status.LatestBackupTime.HasValue
                ? status.LatestBackupTime.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                : "None recorded";
            Backups = new ObservableCollection<BackupInfoDto>(status.Backups);
        }
    }

    [RelayCommand]
    public async Task CreateBackupNowAsync()
    {
        IsBackupInProgress = true;
        BackupStatusMessage = "Creating verified WAL-safe backup via VACUUM INTO...";
        IsBackupError = false;

        try
        {
            var cmd = new CreateManualBackupCommand(_sessionManager.UserId);
            var result = await _createBackupHandler.HandleAsync(cmd);
            if (result.IsSuccess)
            {
                var backup = result.Value;
                BackupStatusMessage = $"Backup verified successfully: {backup.FileName} ({FormatBytes(backup.SizeBytes)})";
                IsBackupError = false;
                await LoadDatabaseStatusAsync();
            }
            else
            {
                BackupStatusMessage = result.Error;
                IsBackupError = true;
            }
        }
        catch (Exception ex)
        {
            BackupStatusMessage = $"Backup failed: {ex.Message}";
            IsBackupError = true;
        }
        finally
        {
            IsBackupInProgress = false;
        }
    }

    // --- Store Profile Commands ---

    [RelayCommand]
    public async Task LoadStoreProfileAsync()
    {
        var profile = await _profileService.GetProfileAsync();
        StoreName = profile.StoreName;
        StoreAddress = profile.Address;
        StorePhone = profile.Phone;
        TaxPanNumber = profile.TaxPanNumber;
        ReceiptFooterMessage = profile.ReceiptFooterMessage;
        AutoLockMinutes = profile.AutoLockMinutes;
        ProfileStatusMessage = null;
    }

    [RelayCommand]
    public async Task SaveStoreProfileAsync()
    {
        var profile = new PharmacyProfileDto
        {
            StoreName = StoreName?.Trim() ?? string.Empty,
            Address = StoreAddress?.Trim() ?? string.Empty,
            Phone = StorePhone?.Trim() ?? string.Empty,
            TaxPanNumber = TaxPanNumber?.Trim() ?? string.Empty,
            ReceiptFooterMessage = ReceiptFooterMessage?.Trim() ?? string.Empty,
            AutoLockMinutes = AutoLockMinutes
        };

        await _profileService.SaveProfileAsync(profile);
        ProfileStatusMessage = "Store profile and preferences saved successfully.";
        IsProfileSaved = true;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F2} MB";
    }

    private static string GetProcessUptime()
    {
        try
        {
            var uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
            return $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";
        }
        catch
        {
            return "Unknown";
        }
    }
}
