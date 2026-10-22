namespace PharmacyApp.Application.Exceptions;

/// <summary>
/// Thrown when an operation violates a database check or validation constraint.
/// </summary>
public sealed class DataConstraintException : InvalidOperationException
{
    public DataConstraintException(string message)
        : base(message)
    {
    }

    public DataConstraintException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
