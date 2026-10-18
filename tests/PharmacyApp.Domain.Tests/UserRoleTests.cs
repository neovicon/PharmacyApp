using PharmacyApp.Domain.Enums;
using Xunit;

namespace PharmacyApp.Domain.Tests;

public class UserRoleTests
{
    [Fact]
    public void UserRole_ContainsExpectedCoarseGrainedRoles()
    {
        Assert.True(Enum.IsDefined(typeof(UserRole), UserRole.Admin));
        Assert.True(Enum.IsDefined(typeof(UserRole), UserRole.Pharmacist));
        Assert.True(Enum.IsDefined(typeof(UserRole), UserRole.Cashier));
    }

    [Fact]
    public void Admin_IsZeroIndexDefault()
    {
        Assert.Equal(0, (int)UserRole.Admin);
    }
}
