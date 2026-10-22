using PharmacyApp.Application.Sales;

namespace PharmacyApp.Application.Abstractions;

public interface IReceiptPrinterService
{
    /// <summary>
    /// Generates a standardized, monospaced thermal-printer formatted text layout for the given receipt.
    /// </summary>
    string FormatThermalReceipt(SaleReceiptDto receipt);

    /// <summary>
    /// Saves the formatted receipt to the application exports/receipts directory and returns the absolute file path.
    /// </summary>
    Task<string> ExportReceiptToFileAsync(SaleReceiptDto receipt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Spools the receipt to the default system printer if available; otherwise writes to disk.
    /// </summary>
    Task<string> PrintOrExportReceiptAsync(SaleReceiptDto receipt, CancellationToken cancellationToken = default);
}
