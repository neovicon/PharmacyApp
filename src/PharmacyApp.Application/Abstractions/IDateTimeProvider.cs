namespace PharmacyApp.Application.Abstractions;

/// <summary>
/// Provides the current UTC time. Injected into handlers so that tests can
/// control time without touching system clocks.
/// </summary>
public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}
