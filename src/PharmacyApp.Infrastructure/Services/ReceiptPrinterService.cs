using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Sales;
using PharmacyApp.Application.Settings;

namespace PharmacyApp.Infrastructure.Services;

public sealed class ReceiptPrinterService : IReceiptPrinterService
{
    private readonly IAppPathProvider _pathProvider;
    private readonly IPharmacyProfileService _profileService;

    public ReceiptPrinterService(
        IAppPathProvider pathProvider,
        IPharmacyProfileService profileService)
    {
        _pathProvider = pathProvider;
        _profileService = profileService;
    }

    public string FormatThermalReceipt(SaleReceiptDto receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        var profile = _profileService.GetProfileAsync().GetAwaiter().GetResult();
        return BuildReceiptText(receipt, profile);
    }

    public async Task<string> ExportReceiptToFileAsync(SaleReceiptDto receipt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        var profile = await _profileService.GetProfileAsync(cancellationToken);
        string receiptText = BuildReceiptText(receipt, profile);

        string receiptsDir = Path.Combine(_pathProvider.DataDirectory, "Receipts");
        Directory.CreateDirectory(receiptsDir);

        string safeInvoiceName = string.Join("_", receipt.InvoiceNumber.Split(Path.GetInvalidFileNameChars()));
        string filePath = Path.Combine(receiptsDir, $"{safeInvoiceName}.txt");

        await File.WriteAllTextAsync(filePath, receiptText, Encoding.UTF8, cancellationToken);
        return filePath;
    }

    public async Task<string> PrintOrExportReceiptAsync(SaleReceiptDto receipt, CancellationToken cancellationToken = default)
    {
        string filePath = await ExportReceiptToFileAsync(receipt, cancellationToken);

        // Attempt non-blocking OS spool if printer command exists
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Check if 'lp' is installed
                using var check = Process.Start(new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = "lp",
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                });
                check?.WaitForExit(500);
                if (check?.ExitCode == 0)
                {
                    using var printProc = Process.Start("lp", filePath);
                    printProc?.WaitForExit(1000);
                }
            }
        }
        catch
        {
            // Silently fall back to saved file on disk without throwing
        }

        return filePath;
    }

    private static string BuildReceiptText(SaleReceiptDto r, PharmacyProfileDto p)
    {
        const int width = 42;
        var sb = new StringBuilder();

        void PrintCenter(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            text = text.Trim();
            if (text.Length >= width)
            {
                sb.AppendLine(text);
                return;
            }
            int pad = (width - text.Length) / 2;
            sb.AppendLine(new string(' ', pad) + text);
        }

        void PrintRow(string left, string right)
        {
            left = left ?? "";
            right = right ?? "";
            int spaces = width - left.Length - right.Length;
            if (spaces < 1) spaces = 1;
            sb.AppendLine(left + new string(' ', spaces) + right);
        }

        sb.AppendLine(new string('=', width));
        PrintCenter(p.StoreName);
        PrintCenter(p.Address);
        if (!string.IsNullOrWhiteSpace(p.Phone)) PrintCenter($"Tel: {p.Phone}");
        if (!string.IsNullOrWhiteSpace(p.TaxPanNumber)) PrintCenter($"Tax/PAN: {p.TaxPanNumber}");
        sb.AppendLine(new string('=', width));

        PrintRow("Invoice No:", r.InvoiceNumber);
        PrintRow("Date & Time:", r.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"));
        if (!string.IsNullOrWhiteSpace(r.CashierName))
        {
            PrintRow("Cashier:", r.CashierName);
        }
        if (!string.IsNullOrWhiteSpace(r.CustomerName))
        {
            PrintRow("Customer:", r.CustomerName);
        }
        if (!string.IsNullOrWhiteSpace(r.PrescriptionNumber))
        {
            PrintRow("Prescription #:", r.PrescriptionNumber);
        }
        PrintRow("Payment:", r.PaymentMethod.ToString());

        sb.AppendLine(new string('-', width));
        sb.AppendLine(string.Format("{0,-20} {1,3} {2,8} {3,8}", "Item", "Qty", "Price", "Total"));
        sb.AppendLine(new string('-', width));

        foreach (var line in r.Lines)
        {
            string name = line.MedicineName.Length > 20 ? line.MedicineName[..19] + "…" : line.MedicineName;
            sb.AppendLine(string.Format("{0,-20} {1,3} {2,8:F2} {3,8:F2}",
                name,
                line.Quantity,
                line.UnitPriceNpr,
                line.LineTotalNpr));

            sb.AppendLine($"  [Lot: {line.BatchCode}]");
        }

        sb.AppendLine(new string('-', width));
        PrintRow("Sub-Total:", $"NPR {r.SubTotalNpr:N2}");
        if (r.DiscountNpr > 0)
        {
            PrintRow("Discount:", $"-NPR {r.DiscountNpr:N2}");
        }
        if (r.TaxNpr > 0)
        {
            PrintRow("Tax / VAT:", $"NPR {r.TaxNpr:N2}");
        }
        sb.AppendLine(new string('-', width));
        PrintRow("GRAND TOTAL:", $"NPR {r.TotalAmountNpr:N2}");
        sb.AppendLine(new string('=', width));

        if (!string.IsNullOrWhiteSpace(p.ReceiptFooterMessage))
        {
            PrintCenter(p.ReceiptFooterMessage);
            sb.AppendLine(new string('=', width));
        }

        return sb.ToString();
    }
}
