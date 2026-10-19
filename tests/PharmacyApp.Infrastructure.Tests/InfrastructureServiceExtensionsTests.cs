using Microsoft.Extensions.DependencyInjection;
using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Common;
using PharmacyApp.Infrastructure;
using Xunit;

namespace PharmacyApp.Infrastructure.Tests;

public class InfrastructureServiceExtensionsTests
{
    [Fact]
    public void AddInfrastructure_RegistersExpectedSingletons()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure();

        using var provider = services.BuildServiceProvider();

        var pathProvider1 = provider.GetService<IAppPathProvider>();
        var pathProvider2 = provider.GetService<IAppPathProvider>();
        var dateTimeProvider1 = provider.GetService<IDateTimeProvider>();
        var dateTimeProvider2 = provider.GetService<IDateTimeProvider>();

        Assert.NotNull(pathProvider1);
        Assert.NotNull(dateTimeProvider1);

        // Verify Singleton lifetime
        Assert.Same(pathProvider1, pathProvider2);
        Assert.Same(dateTimeProvider1, dateTimeProvider2);

        // Verify Authentication & Session Services
        var passwordHasher = provider.GetService<IPasswordHasher>();
        var userRepo = provider.GetService<IUserRepository>();
        var sessionManager = provider.GetService<ISessionManager>();
        var currentUserService = provider.GetService<ICurrentUserService>();
        var idleTimer = provider.GetService<IIdleTimerService>();
        var throttleService = provider.GetService<ILoginThrottleService>();
        var dbInitializer = provider.GetService<PharmacyApp.Infrastructure.Persistence.SqliteDatabaseInitializer>();

        Assert.NotNull(passwordHasher);
        Assert.NotNull(userRepo);
        Assert.NotNull(sessionManager);
        Assert.NotNull(currentUserService);
        Assert.Same(sessionManager, currentUserService);
        Assert.NotNull(idleTimer);
        Assert.NotNull(throttleService);
        Assert.NotNull(dbInitializer);

        // Verify Command Handlers
        var createAdminHandler = provider.GetService<ICommandHandler<PharmacyApp.Application.Authentication.Commands.CreateInitialAdminCommand, Guid>>();
        var loginHandler = provider.GetService<ICommandHandler<PharmacyApp.Application.Authentication.Commands.LoginCommand, PharmacyApp.Application.Authentication.UserSessionDto>>();

        Assert.NotNull(createAdminHandler);
        Assert.NotNull(loginHandler);
    }
}
