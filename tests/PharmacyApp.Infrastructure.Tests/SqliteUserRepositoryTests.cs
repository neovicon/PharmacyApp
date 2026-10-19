using PharmacyApp.Application.Abstractions;
using PharmacyApp.Domain.Entities;
using PharmacyApp.Domain.Enums;
using PharmacyApp.Infrastructure.Persistence;
using Xunit;

namespace PharmacyApp.Infrastructure.Tests;

public class SqliteUserRepositoryTests : IDisposable
{
    private readonly IsolatedTestPathProvider _pathProvider;
    private readonly string _connectionString;
    private readonly SqliteDatabaseInitializer _initializer;
    private readonly SqliteUserRepository _repository;

    public SqliteUserRepositoryTests()
    {
        _pathProvider = new IsolatedTestPathProvider();
        _connectionString = $"Data Source={Path.Combine(_pathProvider.DataDirectory, "pharmacy.db")};Cache=Shared";
        _initializer = new SqliteDatabaseInitializer(_connectionString);
        _initializer.InitializeAsync().GetAwaiter().GetResult();
        _repository = new SqliteUserRepository(_connectionString);
    }

    public void Dispose()
    {
        // Clear all pooled SQLite connections so files are unlocked before deletion
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        _pathProvider.Dispose();
    }

    [Fact]
    public async Task HasAnyUsersAsync_WhenEmpty_ReturnsFalse()
    {
        bool hasUsers = await _repository.HasAnyUsersAsync();
        Assert.False(hasUsers);
    }

    [Fact]
    public async Task HasAnyUsersAsync_AfterAddingUser_ReturnsTrue()
    {
        var user = User.Create("admin", "Admin Name", "some_hash", UserRole.Admin, DateTimeOffset.UtcNow);
        await _repository.AddAsync(user);

        bool hasUsers = await _repository.HasAnyUsersAsync();
        Assert.True(hasUsers);
    }

    [Fact]
    public async Task AddAndGetByIdAsync_PersistsAllFieldsCorrectly()
    {
        var now = new DateTimeOffset(2026, 10, 18, 14, 30, 0, TimeSpan.Zero);
        var user = User.Create("pharmacist1", "Jane Pharmacist", "$2a$12$somevalidhash", UserRole.Pharmacist, now);

        await _repository.AddAsync(user);

        var retrieved = await _repository.GetByIdAsync(user.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(user.Id, retrieved.Id);
        Assert.Equal("pharmacist1", retrieved.Username);
        Assert.Equal("Jane Pharmacist", retrieved.DisplayName);
        Assert.Equal("$2a$12$somevalidhash", retrieved.PasswordHash);
        Assert.Equal(UserRole.Pharmacist, retrieved.Role);
        Assert.True(retrieved.IsActive);
        Assert.Equal(now, retrieved.CreatedAt);
        Assert.Equal(now, retrieved.UpdatedAt);
        Assert.Equal(1u, retrieved.Version);
    }

    [Theory]
    [InlineData("cashier_bob", "cashier_bob")]
    [InlineData("cashier_bob", "CASHIER_BOB")]
    [InlineData("cashier_bob", "Cashier_Bob")]
    public async Task GetByUsernameAsync_PerformsCaseInsensitiveLookup(string storedUsername, string queryUsername)
    {
        var user = User.Create(storedUsername, "Bob", "hash", UserRole.Cashier, DateTimeOffset.UtcNow);
        await _repository.AddAsync(user);

        var retrieved = await _repository.GetByUsernameAsync(queryUsername);
        Assert.NotNull(retrieved);
        Assert.Equal(user.Id, retrieved.Id);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesUserPropertiesAndVersion()
    {
        var now = DateTimeOffset.UtcNow;
        var user = User.Create("admin", "Original Admin", "old_hash", UserRole.Admin, now);
        await _repository.AddAsync(user);

        var updateTime = now.AddHours(3);
        user.UpdatePassword("new_hash", updateTime);
        user.Deactivate(updateTime);

        await _repository.UpdateAsync(user);

        var retrieved = await _repository.GetByIdAsync(user.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("new_hash", retrieved.PasswordHash);
        Assert.False(retrieved.IsActive);
        Assert.Equal(updateTime, retrieved.UpdatedAt);
        Assert.Equal(3u, retrieved.Version);
    }

    [Fact]
    public async Task PersistenceAcrossServiceRecreation_SurvivesProcessBoundary()
    {
        var user = User.Create("persistent_user", "Persistent", "hash_secret", UserRole.Admin, DateTimeOffset.UtcNow);
        await _repository.AddAsync(user);

        // Instantiate completely new repository instance referencing same storage
        var freshRepositoryInstance = new SqliteUserRepository(_connectionString);

        var retrieved = await freshRepositoryInstance.GetByIdAsync(user.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("persistent_user", retrieved.Username);
        Assert.Equal("Persistent", retrieved.DisplayName);
    }

    [Fact]
    public async Task TryCreateInitialAdminAsync_WhenDatabaseIsEmpty_SucceedsAndInsertsSingleRow()
    {
        var admin = User.Create("initial_admin", "Admin", "valid_hash", UserRole.Admin, DateTimeOffset.UtcNow);

        bool success = await _repository.TryCreateInitialAdminAsync(admin);

        Assert.True(success);
        var retrieved = await _repository.GetByIdAsync(admin.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("initial_admin", retrieved.Username);
    }

    [Fact]
    public async Task TryCreateInitialAdminAsync_WhenDatabaseAlreadyHasUser_ReturnsFalseAndDoesNotInsert()
    {
        var firstUser = User.Create("first_user", "First", "valid_hash", UserRole.Admin, DateTimeOffset.UtcNow);
        await _repository.AddAsync(firstUser);

        var secondAdmin = User.Create("second_admin", "Second", "valid_hash", UserRole.Admin, DateTimeOffset.UtcNow);
        bool success = await _repository.TryCreateInitialAdminAsync(secondAdmin);

        Assert.False(success);
        var retrieved = await _repository.GetByIdAsync(secondAdmin.Id);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task TryCreateInitialAdminAsync_ConcurrentCallsAcrossConnections_ExactlyOneSucceeds()
    {
        // Simulate 10 concurrent requests/processes racing to create the initial admin
        int concurrentCallers = 10;
        var tasks = new Task<bool>[concurrentCallers];

        for (int i = 0; i < concurrentCallers; i++)
        {
            int index = i;
            tasks[i] = Task.Run(async () =>
            {
                var repo = new SqliteUserRepository(_connectionString);
                var candidate = User.Create($"admin_{index}", $"Admin {index}", $"hash_{index}", UserRole.Admin, DateTimeOffset.UtcNow);
                return await repo.TryCreateInitialAdminAsync(candidate);
            });
        }

        bool[] results = await Task.WhenAll(tasks);

        int successCount = results.Count(r => r);
        int failureCount = results.Count(r => !r);

        Assert.Equal(1, successCount);
        Assert.Equal(concurrentCallers - 1, failureCount);

        // Verify database contains exactly 1 user
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM Users;";
        long totalUsers = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
        Assert.Equal(1L, totalUsers);
    }
}

internal sealed class IsolatedTestPathProvider : IAppPathProvider, IDisposable
{
    public string DataDirectory { get; }
    public string LogDirectory { get; }

    public IsolatedTestPathProvider()
    {
        DataDirectory = Path.Combine(Path.GetTempPath(), "PharmacyApp_Tests_" + Guid.NewGuid().ToString("N"));
        LogDirectory = Path.Combine(DataDirectory, "Logs");
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(DataDirectory))
            {
                Directory.Delete(DataDirectory, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup in temp directory
        }
    }
}
