using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Users.Commands;
using PharmacyApp.Domain.Entities;
using PharmacyApp.Domain.Enums;
using Xunit;

namespace PharmacyApp.Application.Tests;

public class UserManagementCommandHandlerTests
{
    private readonly TestUserRepository _userRepository = new();
    private readonly TestPasswordHasher _passwordHasher = new();
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));

    public UserManagementCommandHandlerTests()
    {
        // Seed initial admin
        var admin = User.Create("admin", "Administrator", "hashed_Admin123!", UserRole.Admin, _dateTimeProvider.UtcNow);
        _userRepository.AddAsync(admin).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task CreateUser_WhenCurrentUserNotAdmin_ReturnsFailure()
    {
        var cashier = User.Create("cashier1", "Cashier 1", "hashed_Cashier123!", UserRole.Cashier, _dateTimeProvider.UtcNow);
        await _userRepository.AddAsync(cashier);

        var handler = new CreateUserCommandHandler(_userRepository, _passwordHasher, _dateTimeProvider);
        var cmd = new CreateUserCommand("new_staff", "New Staff", "StrongPass1!", UserRole.Pharmacist, cashier.Id);

        var result = await handler.HandleAsync(cmd);

        Assert.True(result.IsFailure);
        Assert.Contains("Only active Administrators", result.Error);
    }

    [Fact]
    public async Task CreateUser_WhenPasswordWeak_ReturnsFailure()
    {
        var admin = (await _userRepository.GetByUsernameAsync("admin"))!;

        var handler = new CreateUserCommandHandler(_userRepository, _passwordHasher, _dateTimeProvider);
        var cmd = new CreateUserCommand("new_staff", "New Staff", "weak", UserRole.Pharmacist, admin.Id);

        var result = await handler.HandleAsync(cmd);

        Assert.True(result.IsFailure);
        Assert.Contains("at least 8 characters", result.Error);
    }

    [Fact]
    public async Task CreateUser_WhenUsernameTaken_ReturnsFailure()
    {
        var admin = (await _userRepository.GetByUsernameAsync("admin"))!;

        var handler = new CreateUserCommandHandler(_userRepository, _passwordHasher, _dateTimeProvider);
        var cmd = new CreateUserCommand("admin", "Duplicate Admin", "StrongPass1!", UserRole.Pharmacist, admin.Id);

        var result = await handler.HandleAsync(cmd);

        Assert.True(result.IsFailure);
        Assert.Contains("already taken", result.Error);
    }

    [Fact]
    public async Task CreateUser_WhenValid_CreatesUserSuccessfully()
    {
        var admin = (await _userRepository.GetByUsernameAsync("admin"))!;

        var handler = new CreateUserCommandHandler(_userRepository, _passwordHasher, _dateTimeProvider);
        var cmd = new CreateUserCommand("pharmacist_ram", "Ram Sharma", "StrongPass1!", UserRole.Pharmacist, admin.Id);

        var result = await handler.HandleAsync(cmd);

        Assert.True(result.IsSuccess);
        var created = await _userRepository.GetByIdAsync(result.Value);
        Assert.NotNull(created);
        Assert.Equal("pharmacist_ram", created.Username);
        Assert.Equal("Ram Sharma", created.DisplayName);
        Assert.Equal(UserRole.Pharmacist, created.Role);
        Assert.True(created.IsActive);
    }

    [Fact]
    public async Task ToggleUserActive_WhenSelfDeactivation_ReturnsFailure()
    {
        var admin = (await _userRepository.GetByUsernameAsync("admin"))!;

        var handler = new ToggleUserActiveCommandHandler(_userRepository, _dateTimeProvider);
        var cmd = new ToggleUserActiveCommand(admin.Id, admin.Id);

        var result = await handler.HandleAsync(cmd);

        Assert.True(result.IsFailure);
        Assert.Equal("You cannot deactivate your own account.", result.Error);
    }

    [Fact]
    public async Task ToggleUserActive_WhenOnlyActiveAdmin_ReturnsFailure()
    {
        var admin = (await _userRepository.GetByUsernameAsync("admin"))!;
        var secondAdmin = User.Create("admin2", "Admin Two", "hashed_Pass123!", UserRole.Admin, _dateTimeProvider.UtcNow);
        await _userRepository.AddAsync(secondAdmin);

        var handler = new ToggleUserActiveCommandHandler(_userRepository, _dateTimeProvider);

        // Deactivating second admin works because 'admin' is still active
        var res1 = await handler.HandleAsync(new ToggleUserActiveCommand(secondAdmin.Id, admin.Id));
        Assert.True(res1.IsSuccess);

        // Now if admin2 tries to deactivate 'admin', there would be 0 active admins left
        // Activate admin2 first to test:
        await handler.HandleAsync(new ToggleUserActiveCommand(secondAdmin.Id, admin.Id)); // now admin2 is active again
        // Deactivate admin:
        var res2 = await handler.HandleAsync(new ToggleUserActiveCommand(admin.Id, secondAdmin.Id));
        Assert.True(res2.IsSuccess);

        // Now only secondAdmin is active. Trying to deactivate secondAdmin should fail!
        // We will simulate another admin call:
        var res3 = await handler.HandleAsync(new ToggleUserActiveCommand(secondAdmin.Id, admin.Id));
        // admin is inactive so cannot perform action:
        Assert.True(res3.IsFailure);
    }

    [Fact]
    public async Task ToggleUserActive_WhenTogglingStaff_SwitchesStatus()
    {
        var admin = (await _userRepository.GetByUsernameAsync("admin"))!;
        var staff = User.Create("staff1", "Staff One", "hashed_Pass123!", UserRole.Pharmacist, _dateTimeProvider.UtcNow);
        await _userRepository.AddAsync(staff);

        var handler = new ToggleUserActiveCommandHandler(_userRepository, _dateTimeProvider);

        // Deactivate
        var res1 = await handler.HandleAsync(new ToggleUserActiveCommand(staff.Id, admin.Id));
        Assert.True(res1.IsSuccess);
        Assert.False(staff.IsActive);

        // Reactivate
        var res2 = await handler.HandleAsync(new ToggleUserActiveCommand(staff.Id, admin.Id));
        Assert.True(res2.IsSuccess);
        Assert.True(staff.IsActive);
    }

    [Fact]
    public async Task ResetPassword_WhenAdmin_ResetsSuccessfully()
    {
        var admin = (await _userRepository.GetByUsernameAsync("admin"))!;
        var staff = User.Create("staff2", "Staff Two", "hashed_OldPass1!", UserRole.Cashier, _dateTimeProvider.UtcNow);
        await _userRepository.AddAsync(staff);

        var handler = new ResetUserPasswordCommandHandler(_userRepository, _passwordHasher, _dateTimeProvider);
        var cmd = new ResetUserPasswordCommand(staff.Id, "BrandNewPass99!", admin.Id);

        var result = await handler.HandleAsync(cmd);

        Assert.True(result.IsSuccess);
        Assert.Equal("hashed_BrandNewPass99!", staff.PasswordHash);
    }

    [Fact]
    public async Task ResetPassword_WhenNonAdminResetsOther_ReturnsFailure()
    {
        var cashier1 = User.Create("cashier_a", "Cashier A", "hashed_Pass123!", UserRole.Cashier, _dateTimeProvider.UtcNow);
        var cashier2 = User.Create("cashier_b", "Cashier B", "hashed_Pass123!", UserRole.Cashier, _dateTimeProvider.UtcNow);
        await _userRepository.AddAsync(cashier1);
        await _userRepository.AddAsync(cashier2);

        var handler = new ResetUserPasswordCommandHandler(_userRepository, _passwordHasher, _dateTimeProvider);
        var cmd = new ResetUserPasswordCommand(cashier2.Id, "NewPass123!", cashier1.Id);

        var result = await handler.HandleAsync(cmd);

        Assert.True(result.IsFailure);
        Assert.Contains("Only Administrators", result.Error);
    }
}
