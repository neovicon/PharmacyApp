using PharmacyApp.Application.Abstractions;
using PharmacyApp.Domain.Enums;

namespace PharmacyApp.Application.Tests.TestDoubles;

public class TestCurrentUserService : ICurrentUserService
{
    public Guid UserId { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = "testuser";
    public string DisplayName { get; set; } = "Test User";
    public UserRole Role { get; set; } = UserRole.Admin;
    public bool IsAuthenticated { get; set; } = true;
}
