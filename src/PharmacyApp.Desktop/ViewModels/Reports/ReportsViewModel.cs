using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PharmacyApp.Application.Common;
using PharmacyApp.Application.Inventory;
using PharmacyApp.Application.Inventory.Queries;
using PharmacyApp.Application.Reports;
using PharmacyApp.Desktop.Navigation;

namespace PharmacyApp.Desktop.ViewModels.Reports;

public sealed partial class ReportsViewModel : ViewModelBase, INavigationAware
{
    private readonly IQueryHandler<GetInventoryValuationQuery, InventoryValuationReportDto> _valuationHandler;
    private readonly IQueryHandler<GetRecentStockAdjustmentsQuery, IReadOnlyList<StockAdjustmentDto>> _adjustmentsHandler;
    private readonly PharmacyApp.Application.Abstractions.IAppPathProvider _pathProvider;

    [ObservableProperty]
    private InventoryValuationReportDto? _valuationReport;

    [ObservableProperty]
    private int _selectedReportTab = 0; // 0: Inventory Valuation, 1: Audit Log

    public bool IsValuationTabActive => SelectedReportTab == 0;
    public bool IsAuditTabActive => SelectedReportTab == 1;

    public string ValuationTabBackground => SelectedReportTab == 0 ? "#6366F1" : "#1F1F38";
    public string ValuationTabForeground => SelectedReportTab == 0 ? "#FFFFFF" : "#A0A0C0";
    public string AuditTabBackground => SelectedReportTab == 1 ? "#6366F1" : "#1F1F38";
    public string AuditTabForeground => SelectedReportTab == 1 ? "#FFFFFF" : "#A0A0C0";

    partial void OnSelectedReportTabChanged(int value)
    {
        OnPropertyChanged(nameof(IsValuationTabActive));
        OnPropertyChanged(nameof(IsAuditTabActive));
        OnPropertyChanged(nameof(ValuationTabBackground));
        OnPropertyChanged(nameof(ValuationTabForeground));
        OnPropertyChanged(nameof(AuditTabBackground));
        OnPropertyChanged(nameof(AuditTabForeground));
    }

    [RelayCommand]
    private void SelectValuationTab() => SelectedReportTab = 0;

    [RelayCommand]
    private void SelectAuditTab() => SelectedReportTab = 1;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    public ObservableCollection<StockAdjustmentDto> AuditTrail { get; } = new();

    public ReportsViewModel(
        IQueryHandler<GetInventoryValuationQuery, InventoryValuationReportDto> valuationHandler,
        IQueryHandler<GetRecentStockAdjustmentsQuery, IReadOnlyList<StockAdjustmentDto>> adjustmentsHandler,
        PharmacyApp.Application.Abstractions.IAppPathProvider pathProvider)
    {
        _valuationHandler = valuationHandler;
        _adjustmentsHandler = adjustmentsHandler;
        _pathProvider = pathProvider;

        _ = LoadReportsAsync();
    }

    public Task OnNavigatedToAsync() => LoadReportsAsync();

    [RelayCommand]
    public async Task LoadReportsAsync()
    {
        IsBusy = true;
        StatusMessage = "Compiling financial and operational reports...";

        try
        {
            var valResult = await _valuationHandler.HandleAsync(new GetInventoryValuationQuery());
            if (valResult.IsSuccess)
            {
                ValuationReport = valResult.Value;
            }

            var adjResult = await _adjustmentsHandler.HandleAsync(new GetRecentStockAdjustmentsQuery(Limit: 100));
            if (adjResult.IsSuccess)
            {
                AuditTrail.Clear();
                foreach (var adj in adjResult.Value)
                {
                    AuditTrail.Add(adj);
                }
            }

            StatusMessage = "Reports successfully generated from live ledger.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error generating report: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ExportCurrentTabCsvAsync()
    {
        if (SelectedReportTab == 0)
        {
            await ExportValuationCsvAsync();
        }
        else
        {
            await ExportAuditTrailCsvAsync();
        }
    }

    [RelayCommand]
    public async Task ExportValuationCsvAsync()
    {
        if (ValuationReport is null || ValuationReport.Items.Count == 0)
        {
            StatusMessage = "No valuation data available to export.";
            return;
        }

        try
        {
            string exportsDir = Path.Combine(_pathProvider.DataDirectory, "exports");
            Directory.CreateDirectory(exportsDir);
            string filePath = Path.Combine(exportsDir, $"inventory_valuation_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Medicine,GenericName,Category,UnitsOnHand,ReorderLevel,CostNpr,RetailNpr,MarginPercent");

            foreach (var item in ValuationReport.Items)
            {
                sb.AppendLine(string.Join(",",
                    EscapeCsv(item.MedicineName),
                    EscapeCsv(item.GenericName),
                    EscapeCsv(item.Category),
                    item.TotalUnitsOnHand,
                    item.ReorderLevel,
                    item.TotalCostValueNpr.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    item.TotalRetailValueNpr.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    item.MarginPercentage.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)));
            }

            await File.WriteAllTextAsync(filePath, sb.ToString());
            StatusMessage = $"Exported valuation report to: {filePath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task ExportAuditTrailCsvAsync()
    {
        if (AuditTrail.Count == 0)
        {
            StatusMessage = "No audit log data available to export.";
            return;
        }

        try
        {
            string exportsDir = Path.Combine(_pathProvider.DataDirectory, "exports");
            Directory.CreateDirectory(exportsDir);
            string filePath = Path.Combine(exportsDir, $"audit_trail_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("TimestampUtc,AdjustmentType,Medicine,BatchCode,QuantityDelta,NewQuantity,Reason,UserId");

            foreach (var adj in AuditTrail)
            {
                sb.AppendLine(string.Join(",",
                    adj.TimestampUtc.ToString("O"),
                    EscapeCsv(adj.AdjustmentTypeName),
                    EscapeCsv(adj.MedicineName),
                    EscapeCsv(adj.BatchCode),
                    adj.QuantityDelta,
                    adj.NewQuantity,
                    EscapeCsv(adj.Reason ?? string.Empty),
                    adj.UserId));
            }

            await File.WriteAllTextAsync(filePath, sb.ToString());
            StatusMessage = $"Exported audit trail to: {filePath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
        }
    }

    private static string EscapeCsv(string? field)
    {
        if (string.IsNullOrEmpty(field)) return "\"\"";
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }
}
