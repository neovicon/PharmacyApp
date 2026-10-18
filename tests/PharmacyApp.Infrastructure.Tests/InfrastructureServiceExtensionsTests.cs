using Microsoft.Extensions.DependencyInjection;
using PharmacyApp.Application.Abstractions;
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
    }
}
