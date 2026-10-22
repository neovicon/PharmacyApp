namespace PharmacyApp.Application.Exceptions;

/// <summary>
/// Thrown when an operation violates the uniqueness constraint on medicine barcodes.
/// </summary>
public sealed class DuplicateBarcodeException : InvalidOperationException
{
    public string? Barcode { get; }

    public DuplicateBarcodeException(string barcode)
        : base($"A medicine with barcode '{barcode}' already exists.")
    {
        Barcode = barcode;
    }

    public DuplicateBarcodeException(string message, Exception innerException, string? barcode = null)
        : base(message, innerException)
    {
        Barcode = barcode;
    }
}
