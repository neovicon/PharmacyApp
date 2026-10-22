namespace PharmacyApp.Application.Exceptions;

/// <summary>
/// Thrown when an expected entity cannot be found in persistence storage during an update or lookup.
/// </summary>
public sealed class EntityNotFoundException : InvalidOperationException
{
    public EntityNotFoundException(string message)
        : base(message)
    {
    }

    public EntityNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
