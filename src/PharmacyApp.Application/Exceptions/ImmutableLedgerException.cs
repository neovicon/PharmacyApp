namespace PharmacyApp.Application.Exceptions;

/// <summary>
/// Thrown when an illegal modification or deletion of an immutable ledger record is attempted.
/// </summary>
public sealed class ImmutableLedgerException : InvalidOperationException
{
    public ImmutableLedgerException(string message)
        : base(message)
    {
    }

    public ImmutableLedgerException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
