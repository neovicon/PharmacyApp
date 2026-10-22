using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Common;
using PharmacyApp.Application.Inventory;
using PharmacyApp.Application.Inventory.Commands;
using PharmacyApp.Application.Inventory.Queries;
using PharmacyApp.Application.Medicines;
using PharmacyApp.Application.Medicines.Queries;
using PharmacyApp.Application.Sales;
using PharmacyApp.Application.Sales.Commands;
using PharmacyApp.Application.Sales.Queries;
using PharmacyApp.Desktop.Navigation;
using PharmacyApp.Domain.Enums;

namespace PharmacyApp.Desktop.ViewModels.Sales;

public sealed partial class CartItemViewModel : ObservableObject
{
    public Guid BatchId { get; init; }
    public Guid MedicineId { get; init; }
    public string MedicineName { get; init; } = string.Empty;
    public string BatchCode { get; init; } = string.Empty;
    public DateOnly? ExpiryDate { get; init; }
    public bool RequiresPrescription { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LineTotalNpr))]
    private int _quantity;

    public decimal UnitPriceNpr { get; init; }

    public decimal LineTotalNpr => Quantity * UnitPriceNpr;
}

public sealed partial class SalesViewModel : ViewModelBase, INavigationAware
{
    private readonly IQueryHandler<SearchMedicinesQuery, IReadOnlyList<MedicineDto>> _searchMedicinesHandler;
    private readonly IQueryHandler<ListMedicinesQuery, IReadOnlyList<MedicineDto>> _listMedicinesHandler;
    private readonly IQueryHandler<GetBatchesByMedicineIdQuery, IReadOnlyList<BatchDto>> _getBatchesHandler;
    private readonly IQueryHandler<GetMedicineByBarcodeQuery, MedicineDto> _getByBarcodeHandler;
    private readonly ICommandHandler<ProcessSaleCommand, SaleReceiptDto> _processSaleHandler;
    private readonly ICommandHandler<AdjustStockCommand> _adjustStockHandler;
    private readonly IQueryHandler<ListInventoryBatchesQuery, IReadOnlyList<BatchDto>> _listBatchesHandler;
    private readonly IQueryHandler<GetRecentInvoicesQuery, IReadOnlyList<InvoiceDto>>? _getRecentInvoicesHandler;
    private readonly IReceiptPrinterService? _receiptPrinterService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private string _quickBarcodeInput = string.Empty;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private MedicineDto? _selectedMedicine;

    [ObservableProperty]
    private BatchDto? _selectedBatch;

    [ObservableProperty]
    private int _quantityToDispense = 1;

    [ObservableProperty]
    private string? _customerName;

    [ObservableProperty]
    private string? _prescriptionNumber;

    [ObservableProperty]
    private PaymentMethod _selectedPaymentMethod = PaymentMethod.Cash;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DiscountNpr))]
    [NotifyPropertyChangedFor(nameof(TaxNpr))]
    [NotifyPropertyChangedFor(nameof(GrandTotalNpr))]
    private decimal _discountAmountNpr = 0m;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TaxNpr))]
    [NotifyPropertyChangedFor(nameof(GrandTotalNpr))]
    private decimal _taxPercentage = 0m;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private SaleReceiptDto? _lastReceipt;

    [ObservableProperty]
    private bool _isReceiptOpen;

    // --- Customer Return & Refund Modal State ---
    [ObservableProperty]
    private bool _isReturnModalOpen;

    [ObservableProperty]
    private BatchDto? _returnSelectedBatch;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReturnRefundTotalNpr))]
    private int _returnQuantity = 1;

    [ObservableProperty]
    private string? _returnCustomerName;

    [ObservableProperty]
    private string? _returnReason;

    [ObservableProperty]
    private string? _returnErrorMessage;

    // --- Invoice History & Reprint Modal State ---
    [ObservableProperty]
    private bool _isHistoryModalOpen;

    [ObservableProperty]
    private string _invoiceSearchText = string.Empty;

    public ObservableCollection<MedicineDto> SearchResults { get; } = new();
    public ObservableCollection<BatchDto> AvailableBatches { get; } = new();
    public ObservableCollection<CartItemViewModel> CartItems { get; } = new();
    public ObservableCollection<BatchDto> ReturnBatches { get; } = new();
    public ObservableCollection<InvoiceDto> RecentInvoices { get; } = new();
    public IReadOnlyList<PaymentMethod> AvailablePaymentMethods { get; } = (PaymentMethod[])Enum.GetValues(typeof(PaymentMethod));

    public decimal SubTotalNpr => CartItems.Sum(c => c.LineTotalNpr);
    public decimal DiscountNpr => Math.Min(DiscountAmountNpr, SubTotalNpr);
    public decimal TaxNpr => TaxPercentage > 0 ? Math.Round((SubTotalNpr - DiscountNpr) * (TaxPercentage / 100m), 2) : 0m;
    public decimal GrandTotalNpr => (SubTotalNpr - DiscountNpr) + TaxNpr;

    public decimal ReturnRefundTotalNpr => ReturnSelectedBatch is not null ? ReturnSelectedBatch.SellingPriceNpr * ReturnQuantity : 0m;

    public SalesViewModel(
        IQueryHandler<SearchMedicinesQuery, IReadOnlyList<MedicineDto>> searchMedicinesHandler,
        IQueryHandler<ListMedicinesQuery, IReadOnlyList<MedicineDto>> listMedicinesHandler,
        IQueryHandler<GetBatchesByMedicineIdQuery, IReadOnlyList<BatchDto>> getBatchesHandler,
        IQueryHandler<GetMedicineByBarcodeQuery, MedicineDto> getByBarcodeHandler,
        ICommandHandler<ProcessSaleCommand, SaleReceiptDto> processSaleHandler,
        ICommandHandler<AdjustStockCommand> adjustStockHandler,
        IQueryHandler<ListInventoryBatchesQuery, IReadOnlyList<BatchDto>> listBatchesHandler,
        IDialogService dialogService,
        IQueryHandler<GetRecentInvoicesQuery, IReadOnlyList<InvoiceDto>>? getRecentInvoicesHandler = null,
        IReceiptPrinterService? receiptPrinterService = null)
    {
        _searchMedicinesHandler = searchMedicinesHandler;
        _listMedicinesHandler = listMedicinesHandler;
        _getBatchesHandler = getBatchesHandler;
        _getByBarcodeHandler = getByBarcodeHandler;
        _processSaleHandler = processSaleHandler;
        _adjustStockHandler = adjustStockHandler;
        _listBatchesHandler = listBatchesHandler;
        _dialogService = dialogService;
        _getRecentInvoicesHandler = getRecentInvoicesHandler;
        _receiptPrinterService = receiptPrinterService;

        _ = LoadInitialMedicinesAsync();
    }

    public async Task OnNavigatedToAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            await LoadInitialMedicinesAsync();
        }
    }

    [RelayCommand]
    public async Task QuickScanBarcodeAsync()
    {
        if (string.IsNullOrWhiteSpace(QuickBarcodeInput)) return;
        string code = QuickBarcodeInput.Trim();
        QuickBarcodeInput = string.Empty;

        try
        {
            var res = await _getByBarcodeHandler.HandleAsync(new GetMedicineByBarcodeQuery(code));
            if (res.IsSuccess)
            {
                SelectedMedicine = res.Value;
                await LoadBatchesForMedicineAsync(res.Value.Id);

                if (SelectedBatch != null)
                {
                    QuantityToDispense = 1;
                    AddToCart();
                    StatusMessage = $"Scanned & added 1 unit of {res.Value.Name} (Batch {SelectedBatch.BatchCode}).";
                }
                else
                {
                    StatusMessage = $"Selected {res.Value.Name}, but no active batch in stock.";
                }
            }
            else
            {
                SearchText = code;
                await SearchAsync();
                StatusMessage = $"No exact barcode match for '{code}' — filtered search list.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Barcode scan error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task LoadInitialMedicinesAsync()
    {
        IsBusy = true;
        try
        {
            var res = await _listMedicinesHandler.HandleAsync(new ListMedicinesQuery(IsActive: true));
            if (res.IsSuccess)
            {
                SearchResults.Clear();
                foreach (var m in res.Value.Take(25))
                {
                    SearchResults.Add(m);
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            SearchResults.Clear();
            return;
        }

        IsBusy = true;
        try
        {
            var res = await _searchMedicinesHandler.HandleAsync(new SearchMedicinesQuery(SearchText.Trim(), IsActive: true));
            if (res.IsSuccess)
            {
                SearchResults.Clear();
                foreach (var m in res.Value)
                {
                    SearchResults.Add(m);
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        _ = SearchAsync();
    }

    partial void OnSelectedMedicineChanged(MedicineDto? value)
    {
        if (value is not null)
        {
            _ = LoadBatchesForMedicineAsync(value.Id);
        }
        else
        {
            AvailableBatches.Clear();
            SelectedBatch = null;
        }
    }

    partial void OnReturnSelectedBatchChanged(BatchDto? value)
    {
        OnPropertyChanged(nameof(ReturnRefundTotalNpr));
    }

    private async Task LoadBatchesForMedicineAsync(Guid medicineId)
    {
        try
        {
            var res = await _getBatchesHandler.HandleAsync(new GetBatchesByMedicineIdQuery(medicineId, IsActive: true));
            AvailableBatches.Clear();
            if (res.IsSuccess)
            {
                // FEFO: Sort by earliest expiry first, filter out 0 stock
                var inStockBatches = res.Value
                    .Where(b => b.QuantityOnHand > 0)
                    .OrderBy(b => b.ExpiryDate == null)
                    .ThenBy(b => b.ExpiryDate)
                    .ToList();

                foreach (var b in inStockBatches)
                {
                    AvailableBatches.Add(b);
                }

                SelectedBatch = AvailableBatches.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    public void AddToCart()
    {
        ErrorMessage = null;

        if (SelectedMedicine is null)
        {
            ErrorMessage = "Select a medicine from the list first.";
            return;
        }

        if (SelectedBatch is null)
        {
            ErrorMessage = "No in-stock batch selected.";
            return;
        }

        if (QuantityToDispense <= 0)
        {
            ErrorMessage = "Quantity must be at least 1.";
            return;
        }

        // Safety verification: Check expired
        if (SelectedBatch.IsExpired)
        {
            ErrorMessage = $"Safety Violation: Batch '{SelectedBatch.BatchCode}' expired on {SelectedBatch.ExpiryDate:yyyy-MM-dd} and cannot be dispensed.";
            return;
        }

        // Safety verification: Check unknown expiry
        if (!SelectedBatch.HasKnownExpiry)
        {
            ErrorMessage = $"Safety Violation: Batch '{SelectedBatch.BatchCode}' has an unknown expiry date and cannot be verified safe to dispense.";
            return;
        }

        // Safety verification: Prevent negative stock
        int alreadyInCart = CartItems.Where(c => c.BatchId == SelectedBatch.Id).Sum(c => c.Quantity);
        if (alreadyInCart + QuantityToDispense > SelectedBatch.QuantityOnHand)
        {
            ErrorMessage = $"Insufficient stock for batch '{SelectedBatch.BatchCode}'. Available: {SelectedBatch.QuantityOnHand}, In Cart: {alreadyInCart}, Requested: {QuantityToDispense}.";
            return;
        }

        var existingItem = CartItems.FirstOrDefault(c => c.BatchId == SelectedBatch.Id);
        if (existingItem != null)
        {
            existingItem.Quantity += QuantityToDispense;
        }
        else
        {
            CartItems.Add(new CartItemViewModel
            {
                BatchId = SelectedBatch.Id,
                MedicineId = SelectedMedicine.Id,
                MedicineName = SelectedMedicine.Name,
                BatchCode = SelectedBatch.BatchCode,
                ExpiryDate = SelectedBatch.ExpiryDate,
                RequiresPrescription = SelectedMedicine.RequiresPrescription,
                Quantity = QuantityToDispense,
                UnitPriceNpr = SelectedBatch.SellingPriceNpr
            });
        }

        NotifyTotals();
        QuantityToDispense = 1;
    }

    [RelayCommand]
    public void RemoveFromCart(CartItemViewModel item)
    {
        CartItems.Remove(item);
        NotifyTotals();
    }

    [RelayCommand]
    public void ClearCart()
    {
        CartItems.Clear();
        NotifyTotals();
    }

    private void NotifyTotals()
    {
        OnPropertyChanged(nameof(SubTotalNpr));
        OnPropertyChanged(nameof(DiscountNpr));
        OnPropertyChanged(nameof(TaxNpr));
        OnPropertyChanged(nameof(GrandTotalNpr));
    }

    [RelayCommand]
    public async Task CompleteSaleAsync()
    {
        ErrorMessage = null;

        if (CartItems.Count == 0)
        {
            ErrorMessage = "Cart is empty. Add medicines to dispense first.";
            return;
        }

        // Prescription check
        bool hasRxMedicine = CartItems.Any(c => c.RequiresPrescription);
        if (hasRxMedicine && string.IsNullOrWhiteSpace(PrescriptionNumber))
        {
            ErrorMessage = "Prescription required: One or more medicines in the cart require a valid Prescription Number.";
            return;
        }

        IsBusy = true;

        try
        {
            var command = new ProcessSaleCommand(
                Items: CartItems.Select(c => new SaleLineItemRequest(c.BatchId, c.Quantity)).ToList(),
                CustomerName: CustomerName,
                PrescriptionNumber: PrescriptionNumber,
                Notes: Notes,
                PaymentMethod: SelectedPaymentMethod,
                DiscountAmountNpr: DiscountAmountNpr,
                TaxPercentage: TaxPercentage);

            var result = await _processSaleHandler.HandleAsync(command);
            if (result.IsFailure)
            {
                ErrorMessage = result.Error;
                return;
            }

            LastReceipt = result.Value;
            IsReceiptOpen = true;

            // Reset checkout fields
            CartItems.Clear();
            NotifyTotals();
            CustomerName = null;
            PrescriptionNumber = null;
            Notes = null;
            DiscountAmountNpr = 0m;

            if (SelectedMedicine is not null)
            {
                await LoadBatchesForMedicineAsync(SelectedMedicine.Id);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public void CloseReceipt()
    {
        IsReceiptOpen = false;
        LastReceipt = null;
    }

    [RelayCommand]
    public async Task PrintOrExportReceiptAsync()
    {
        if (LastReceipt is null || _receiptPrinterService is null) return;

        try
        {
            string path = await _receiptPrinterService.PrintOrExportReceiptAsync(LastReceipt);
            await _dialogService.ShowMessageAsync(
                "Receipt Printed & Exported",
                $"Receipt for invoice '{LastReceipt.InvoiceNumber}' has been generated and saved to:\n{path}");
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync("Print Error", $"Unable to print/export receipt: {ex.Message}");
        }
    }

    // --- Customer Return & Refund Handlers ---
    [RelayCommand]
    public async Task OpenReturnModalAsync()
    {
        ReturnErrorMessage = null;
        ReturnCustomerName = string.Empty;
        ReturnReason = string.Empty;
        ReturnQuantity = 1;

        try
        {
            var res = await _listBatchesHandler.HandleAsync(new ListInventoryBatchesQuery(IsActive: true));
            ReturnBatches.Clear();
            if (res.IsSuccess)
            {
                foreach (var b in res.Value)
                {
                    ReturnBatches.Add(b);
                }
                ReturnSelectedBatch = ReturnBatches.FirstOrDefault();
            }
            IsReturnModalOpen = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Unable to load batches for return: {ex.Message}";
        }
    }

    [RelayCommand]
    public void CloseReturnModal()
    {
        IsReturnModalOpen = false;
        ReturnErrorMessage = null;
    }

    [RelayCommand]
    public async Task ProcessReturnAsync()
    {
        if (ReturnSelectedBatch is null)
        {
            ReturnErrorMessage = "Please select a medicine batch to return.";
            return;
        }

        if (ReturnQuantity <= 0)
        {
            ReturnErrorMessage = "Return quantity must be at least 1.";
            return;
        }

        if (string.IsNullOrWhiteSpace(ReturnReason))
        {
            ReturnErrorMessage = "Please enter a reason or prescription/invoice note for the return.";
            return;
        }

        IsBusy = true;
        ReturnErrorMessage = null;

        try
        {
            int newQty = ReturnSelectedBatch.QuantityOnHand + ReturnQuantity;
            string auditReason = $"Customer Return: {(!string.IsNullOrWhiteSpace(ReturnCustomerName) ? ReturnCustomerName.Trim() : "Walk-in Customer")} | {ReturnReason.Trim()} (Refund NPR {ReturnRefundTotalNpr:N2})";

            var command = new AdjustStockCommand(
                BatchId: ReturnSelectedBatch.Id,
                NewQuantity: newQty,
                AdjustmentType: StockAdjustmentType.ReturnFromCustomer,
                Reason: auditReason,
                ExpectedVersion: ReturnSelectedBatch.Version);

            var result = await _adjustStockHandler.HandleAsync(command);
            if (result.IsFailure)
            {
                ReturnErrorMessage = result.Error;
                return;
            }

            decimal refundedAmount = ReturnRefundTotalNpr;
            string batchCode = ReturnSelectedBatch.BatchCode;
            string medName = ReturnSelectedBatch.MedicineName;

            IsReturnModalOpen = false;

            await _dialogService.ShowMessageAsync(
                "Return & Refund Processed",
                $"Successfully refunded NPR {refundedAmount:N2}.\nRestocked {ReturnQuantity} unit(s) of '{medName}' into batch '{batchCode}'.");

            if (SelectedMedicine is not null)
            {
                await LoadBatchesForMedicineAsync(SelectedMedicine.Id);
            }
        }
        catch (Exception ex)
        {
            ReturnErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // --- Invoice History & Reprint Handlers ---
    [RelayCommand]
    public async Task OpenHistoryModalAsync()
    {
        InvoiceSearchText = string.Empty;
        await LoadRecentInvoicesAsync();
        IsHistoryModalOpen = true;
    }

    [RelayCommand]
    public void CloseHistoryModal()
    {
        IsHistoryModalOpen = false;
    }

    [RelayCommand]
    public async Task LoadRecentInvoicesAsync()
    {
        if (_getRecentInvoicesHandler is null) return;

        IsBusy = true;
        try
        {
            var res = await _getRecentInvoicesHandler.HandleAsync(new GetRecentInvoicesQuery(
                Limit: 50,
                SearchTerm: string.IsNullOrWhiteSpace(InvoiceSearchText) ? null : InvoiceSearchText.Trim()));

            RecentInvoices.Clear();
            if (res.IsSuccess)
            {
                foreach (var inv in res.Value)
                {
                    RecentInvoices.Add(inv);
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Unable to load invoices: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public void ReprintInvoiceReceipt(InvoiceDto invoice)
    {
        if (invoice is null) return;

        var receiptLines = invoice.Items.Select(i => new SaleReceiptLineDto(
            BatchId: i.BatchId,
            MedicineId: i.MedicineId,
            MedicineName: i.MedicineName,
            BatchCode: i.BatchCode,
            Quantity: i.Quantity,
            UnitPriceNpr: i.UnitPriceNpr,
            LineTotalNpr: i.LineTotalNpr)).ToList();

        Enum.TryParse<PaymentMethod>(invoice.PaymentMethodName, out var pm);

        LastReceipt = new SaleReceiptDto(
            TransactionId: invoice.Id,
            InvoiceNumber: invoice.InvoiceNumber,
            TimestampUtc: invoice.CreatedAtUtc,
            CustomerName: invoice.CustomerName,
            PrescriptionNumber: invoice.PrescriptionNumber,
            PaymentMethod: pm,
            Lines: receiptLines,
            SubTotalNpr: invoice.SubTotalNpr,
            DiscountNpr: invoice.DiscountNpr,
            TaxNpr: invoice.TaxNpr,
            TotalAmountNpr: invoice.TotalAmountNpr,
            CashierName: "Verified Register");

        IsHistoryModalOpen = false;
        IsReceiptOpen = true;
    }
}
