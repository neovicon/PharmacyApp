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
