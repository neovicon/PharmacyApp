using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Settings;
using PharmacyApp.Application.Settings.Commands;
using PharmacyApp.Application.Settings.Queries;
using PharmacyApp.Domain.Entities;
using PharmacyApp.Domain.Enums;
using Xunit;

namespace PharmacyApp.Application.Tests;

public class SettingsAndBackupCommandHandlerTests
{
    private readonly TestUserRepository _userRepository = new();
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));
    private readonly FakeDatabaseManagementService _dbManagementService = new();

    public SettingsAndBackupCommandHandlerTests()
    {
        var admin = User.Create("admin", "Admin User", "hashed_Admin123!", UserRole.Admin, _dateTimeProvider.UtcNow);
        _userRepository.AddAsync(admin).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task GetDatabaseStatus_WhenInvoked_ReturnsStatus()
    {
        var handler = new GetDatabaseStatusQueryHandler(_dbManagementService);
        var result = await handler.HandleAsync(new GetDatabaseStatusQuery());

        Assert.True(result.IsSuccess);
        Assert.Equal("WAL", result.Value.JournalMode);
        Assert.True(result.Value.ForeignKeysEnabled);
    }

    [Fact]
    public async Task CreateManualBackup_WhenUserIsAdmin_CreatesBackupSuccessfully()
    {
        var admin = (await _userRepository.GetByUsernameAsync("admin"))!;
        var handler = new CreateManualBackupCommandHandler(_dbManagementService, _userRepository);

        var result = await handler.HandleAsync(new CreateManualBackupCommand(admin.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal("test_backup.db", result.Value.FileName);
    }

    [Fact]
    public async Task CreateManualBackup_WhenUserIsNotAdmin_ReturnsFailure()
    {
        var cashier = User.Create("cashier", "Cashier Staff", "hashed_Pass123!", UserRole.Cashier, _dateTimeProvider.UtcNow);
        await _userRepository.AddAsync(cashier);

        var handler = new CreateManualBackupCommandHandler(_dbManagementService, _userRepository);

        var result = await handler.HandleAsync(new CreateManualBackupCommand(cashier.Id));

        Assert.True(result.IsFailure);
        Assert.Contains("Only active Administrators", result.Error);
    }

    private sealed class FakeDatabaseManagementService : IDatabaseManagementService
    {
        public Task<DatabaseStatusDto> GetDatabaseStatusAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DatabaseStatusDto(
                DatabasePath: "/test/pharmacy.db",
                DatabaseSizeBytes: 102400,
                JournalMode: "WAL",
                ForeignKeysEnabled: true,
                BackupCount: 1,
                TotalBackupSizeBytes: 51200,
                LatestBackupTime: DateTimeOffset.UtcNow,
                Backups: new List<BackupInfoDto>
                {
                    new("test_backup.db", "/test/backups/test_backup.db", 51200, DateTimeOffset.UtcNow)
                }));
        }

        public Task<BackupInfoDto> CreateBackupAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new BackupInfoDto("test_backup.db", "/test/backups/test_backup.db", 51200, DateTimeOffset.UtcNow));
        }
    }
}
