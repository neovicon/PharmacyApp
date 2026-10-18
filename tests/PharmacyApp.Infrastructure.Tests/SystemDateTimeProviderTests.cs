using PharmacyApp.Infrastructure.Services;
using Xunit;

namespace PharmacyApp.Infrastructure.Tests;

public class SystemDateTimeProviderTests
{
    [Fact]
    public void UtcNow_ReturnsCurrentUtcTimestamp()
    {
        var provider = new SystemDateTimeProvider();
        DateTimeOffset before = DateTimeOffset.UtcNow;
        DateTimeOffset now = provider.UtcNow;
        DateTimeOffset after = DateTimeOffset.UtcNow;

        Assert.True(now >= before);
        Assert.True(now <= after);
    }
}
