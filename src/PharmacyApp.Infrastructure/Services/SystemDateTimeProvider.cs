using PharmacyApp.Application.Abstractions;

namespace PharmacyApp.Infrastructure.Services;

/// <summary>
/// Returns the real UTC wall-clock time.
/// Replace with a controllable implementation in tests.
/// </summary>
public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
