namespace PharmacyApp.Application.Exceptions;

/// <summary>
/// Thrown when an operation violates the uniqueness constraint on internal batch codes.
/// </summary>
public sealed class DuplicateBatchCodeException : InvalidOperationException
{
    public string? BatchCode { get; }

    public DuplicateBatchCodeException(string batchCode)
        : base($"A batch with batch code '{batchCode}' already exists.")
    {
        BatchCode = batchCode;
    }

    public DuplicateBatchCodeException(string message, Exception innerException, string? batchCode = null)
        : base(message, innerException)
    {
        BatchCode = batchCode;
    }
}
