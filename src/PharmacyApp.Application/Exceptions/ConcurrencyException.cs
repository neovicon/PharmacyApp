namespace PharmacyApp.Application.Exceptions;

/// <summary>
/// Thrown when an optimistic concurrency conflict occurs during an entity update
/// (e.g. the record was modified by another operation since it was retrieved).
/// </summary>
public sealed class ConcurrencyException : InvalidOperationException
{
    public ConcurrencyException(string message)
        : base(message)
    {
    }

    public ConcurrencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
