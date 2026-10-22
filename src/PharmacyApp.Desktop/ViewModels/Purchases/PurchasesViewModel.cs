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
using PharmacyApp.Application.Suppliers;
using PharmacyApp.Application.Suppliers.Commands;
using PharmacyApp.Application.Suppliers.Queries;
using PharmacyApp.Desktop.Navigation;

namespace PharmacyApp.Desktop.ViewModels.Purchases;

public sealed partial class PurchasesViewModel : ViewModelBase, INavigationAware
{
    private readonly IQueryHandler<ListMedicinesQuery, IReadOnlyList<MedicineDto>> _listMedicinesHandler;
    private readonly IQueryHandler<ListInventoryBatchesQuery, IReadOnlyList<BatchDto>> _listBatchesHandler;
    private readonly IQueryHandler<ListSuppliersQuery, IReadOnlyList<SupplierDto>> _listSuppliersHandler;
    private readonly ICommandHandler<ReceiveBatchCommand, Guid> _receiveBatchHandler;
    private readonly ICommandHandler<CreateSupplierCommand, Guid> _createSupplierHandler;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private MedicineDto? _selectedMedicine;

    [ObservableProperty]
    private SupplierDto? _selectedSupplier;

    [ObservableProperty]
    private string _batchCode = string.Empty;

    [ObservableProperty]
    private int _quantity = 100;

    [ObservableProperty]
    private decimal _purchaseCostNpr = 10.00m;

    [ObservableProperty]
    private decimal _sellingPriceNpr = 15.00m;

    [ObservableProperty]
    private string? _manufacturerBatchNumber;

    [ObservableProperty]
    private DateTimeOffset? _expiryDate = DateTimeOffset.Now.AddYears(1);

    [ObservableProperty]
    private string? _storageLocation = "Shelf A-1";

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isBusy;

    // --- Add Supplier Modal State ---
    [ObservableProperty]
    private bool _isAddSupplierModalOpen;

    [ObservableProperty]
    private string _newSupplierName = string.Empty;

    [ObservableProperty]
    private string? _newSupplierContactPerson;

    [ObservableProperty]
    private string? _newSupplierPhone;

    [ObservableProperty]
    private string? _newSupplierEmail;

    [ObservableProperty]
    private string? _newSupplierPan;

    [ObservableProperty]
    private string? _newSupplierAddress;

    [ObservableProperty]
    private string? _addSupplierErrorMessage;

    public ObservableCollection<MedicineDto> AvailableMedicines { get; } = new();
    public ObservableCollection<SupplierDto> AvailableSuppliers { get; } = new();
    public ObservableCollection<BatchDto> RecentBatches { get; } = new();

    public PurchasesViewModel(
        IQueryHandler<ListMedicinesQuery, IReadOnlyList<MedicineDto>> listMedicinesHandler,
        IQueryHandler<ListInventoryBatchesQuery, IReadOnlyList<BatchDto>> listBatchesHandler,
        IQueryHandler<ListSuppliersQuery, IReadOnlyList<SupplierDto>> listSuppliersHandler,
        ICommandHandler<ReceiveBatchCommand, Guid> receiveBatchHandler,
        ICommandHandler<CreateSupplierCommand, Guid> createSupplierHandler,
        IDialogService dialogService)
    {
        _listMedicinesHandler = listMedicinesHandler;
        _listBatchesHandler = listBatchesHandler;
        _listSuppliersHandler = listSuppliersHandler;
        _receiveBatchHandler = receiveBatchHandler;
        _createSupplierHandler = createSupplierHandler;
        _dialogService = dialogService;

        _ = InitializeAsync();
    }

    public Task OnNavigatedToAsync() => InitializeAsync();

    [RelayCommand]
    public async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            var medResult = await _listMedicinesHandler.HandleAsync(new ListMedicinesQuery(IsActive: true));
            if (medResult.IsSuccess)
            {
                Guid? prevMedId = SelectedMedicine?.Id;
                AvailableMedicines.Clear();
                foreach (var m in medResult.Value)
                {
                    AvailableMedicines.Add(m);
                }

                if (prevMedId.HasValue)
                {
                    SelectedMedicine = AvailableMedicines.FirstOrDefault(m => m.Id == prevMedId.Value)
                                       ?? AvailableMedicines.FirstOrDefault();
                }
                else
                {
                    SelectedMedicine = AvailableMedicines.FirstOrDefault();
                }
            }

            var supResult = await _listSuppliersHandler.HandleAsync(new ListSuppliersQuery(IsActive: true));
            if (supResult.IsSuccess)
            {
                Guid? prevSupId = SelectedSupplier?.Id;
                AvailableSuppliers.Clear();
                foreach (var s in supResult.Value)
                {
                    AvailableSuppliers.Add(s);
                }

                if (prevSupId.HasValue)
                {
                    SelectedSupplier = AvailableSuppliers.FirstOrDefault(s => s.Id == prevSupId.Value)
                                       ?? AvailableSuppliers.FirstOrDefault();
                }
                else
                {
                    SelectedSupplier = AvailableSuppliers.FirstOrDefault();
                }
            }

            var batchResult = await _listBatchesHandler.HandleAsync(new ListInventoryBatchesQuery(IsActive: true));
            if (batchResult.IsSuccess)
            {
                RecentBatches.Clear();
                foreach (var b in batchResult.Value.Take(15))
                {
                    RecentBatches.Add(b);
                }
            }

            StatusMessage = "Ready to receive supplier stock intake.";
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
    public void OpenAddSupplierModal()
    {
        NewSupplierName = string.Empty;
        NewSupplierContactPerson = null;
        NewSupplierPhone = null;
        NewSupplierEmail = null;
        NewSupplierPan = null;
        NewSupplierAddress = null;
        AddSupplierErrorMessage = null;
        IsAddSupplierModalOpen = true;
    }

    [RelayCommand]
    public void CloseAddSupplierModal()
    {
        IsAddSupplierModalOpen = false;
        AddSupplierErrorMessage = null;
    }

    [RelayCommand]
    public async Task SaveNewSupplierAsync()
    {
        if (string.IsNullOrWhiteSpace(NewSupplierName))
        {
            AddSupplierErrorMessage = "Supplier company name is required.";
            return;
        }

        try
        {
            var command = new CreateSupplierCommand(
                Name: NewSupplierName.Trim(),
                ContactPerson: NewSupplierContactPerson?.Trim(),
                Phone: NewSupplierPhone?.Trim(),
                Email: NewSupplierEmail?.Trim(),
                PanNumber: NewSupplierPan?.Trim(),
                Address: NewSupplierAddress?.Trim());

            var result = await _createSupplierHandler.HandleAsync(command);
            if (result.IsFailure)
            {
                AddSupplierErrorMessage = result.Error;
                return;
            }

            // Refresh supplier list and auto-select new one
            var supResult = await _listSuppliersHandler.HandleAsync(new ListSuppliersQuery(IsActive: true));
            if (supResult.IsSuccess)
            {
                AvailableSuppliers.Clear();
                foreach (var s in supResult.Value)
                {
                    AvailableSuppliers.Add(s);
                }
                SelectedSupplier = AvailableSuppliers.FirstOrDefault(s => s.Id == result.Value) ?? AvailableSuppliers.FirstOrDefault();
            }

            IsAddSupplierModalOpen = false;
            await _dialogService.ShowMessageAsync("Supplier Added", $"Supplier '{NewSupplierName.Trim()}' registered successfully.");
        }
        catch (Exception ex)
        {
            AddSupplierErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    public async Task SubmitPurchaseAsync()
    {
        if (SelectedMedicine is null)
        {
            ErrorMessage = "Please select a medicine product.";
            return;
        }

        if (string.IsNullOrWhiteSpace(BatchCode))
        {
            ErrorMessage = "Internal batch code is required.";
            return;
        }

        if (Quantity <= 0)
        {
            ErrorMessage = "Quantity received must be greater than zero.";
            return;
        }

        if (PurchaseCostNpr < 0 || SellingPriceNpr < 0)
        {
            ErrorMessage = "Prices cannot be negative.";
            return;
        }

        DateOnly? expiryDateOnly = ExpiryDate.HasValue
            ? DateOnly.FromDateTime(ExpiryDate.Value.Date)
            : null;

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            string? combinedNotes = SelectedSupplier is not null
                ? $"[Supplier: {SelectedSupplier.Name}] {(string.IsNullOrWhiteSpace(Notes) ? "" : Notes.Trim())}".Trim()
                : Notes?.Trim();

            var command = new ReceiveBatchCommand(
                MedicineId: SelectedMedicine.Id,
                BatchCode: BatchCode.Trim(),
                Quantity: Quantity,
                PurchaseCostNpr: PurchaseCostNpr,
                SellingPriceNpr: SellingPriceNpr,
                ManufacturerBatchNumber: ManufacturerBatchNumber?.Trim(),
                ExpiryDate: expiryDateOnly,
                Location: StorageLocation?.Trim(),
                Reason: combinedNotes,
                IsPurchaseOrder: true);

            var result = await _receiveBatchHandler.HandleAsync(command);
            if (result.IsFailure)
            {
                ErrorMessage = result.Error;
                return;
            }

            await _dialogService.ShowMessageAsync("Stock Received", $"Successfully received {Quantity} units of '{SelectedMedicine.Name}' into batch '{BatchCode.ToUpperInvariant()}'.");

            // Reset fields for next entry
            BatchCode = string.Empty;
            ManufacturerBatchNumber = null;
            Notes = null;

            await InitializeAsync();
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
}
