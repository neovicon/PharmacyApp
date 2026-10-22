namespace PharmacyApp.Application.Exceptions;

/// <summary>
/// Thrown when an operation violates a foreign key constraint (e.g. invalid medicine ID, batch ID, or user ID).
/// </summary>
public sealed class ForeignKeyConstraintException : InvalidOperationException
{
    public ForeignKeyConstraintException(string message)
        : base(message)
    {
    }

    public ForeignKeyConstraintException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
