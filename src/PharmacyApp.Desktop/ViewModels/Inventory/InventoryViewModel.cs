using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Common;
using PharmacyApp.Application.Inventory;
using PharmacyApp.Application.Inventory.Commands;
using PharmacyApp.Application.Inventory.Queries;
using PharmacyApp.Domain.Enums;
using PharmacyApp.Desktop.Navigation;

namespace PharmacyApp.Desktop.ViewModels.Inventory;

public sealed partial class InventoryViewModel : ViewModelBase, INavigationAware
{
    private readonly IQueryHandler<ListInventoryBatchesQuery, IReadOnlyList<BatchDto>> _listBatchesHandler;
    private readonly ICommandHandler<AdjustStockCommand> _adjustStockHandler;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _filterOption = 1; // 0: All, 1: In-Stock Only, 2: Out of Stock

    public IReadOnlyList<string> FilterOptions { get; } = new[] { "All Batches", "In-Stock Only", "Out of Stock" };

    public IReadOnlyList<string> AdjustmentTypeOptions { get; } = new[]
    {
        "Damaged / Broken Packaging",
        "Expired Stock Write-Off",
        "Physical Count Discrepancy Correction",
        "Return to Supplier / Recall",
        "Theft / Unexplained Shortage",
        "Customer Return / Unopened Restock"
    };

    [ObservableProperty]
    private BatchDto? _selectedBatch;

    [ObservableProperty]
    private bool _isAdjustModalOpen;

    [ObservableProperty]
    private int _adjustNewQuantity;

    [ObservableProperty]
    private int _selectedAdjustmentTypeIndex = 0;

    [ObservableProperty]
    private string _adjustReason = string.Empty;

    [ObservableProperty]
    private string? _adjustErrorMessage;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<BatchDto> Batches { get; } = new();

    public bool CanAdjustStock =>
        !_currentUserService.IsAuthenticated ||
        _currentUserService.Role is UserRole.Admin or UserRole.Pharmacist;

    public InventoryViewModel(
        IQueryHandler<ListInventoryBatchesQuery, IReadOnlyList<BatchDto>> listBatchesHandler,
        ICommandHandler<AdjustStockCommand> adjustStockHandler,
        ICurrentUserService currentUserService,
        IDialogService dialogService)
    {
        _listBatchesHandler = listBatchesHandler;
        _adjustStockHandler = adjustStockHandler;
        _currentUserService = currentUserService;
        _dialogService = dialogService;

        _ = LoadBatchesAsync();
    }

    public Task OnNavigatedToAsync() => LoadBatchesAsync();

    [RelayCommand]
    public async Task LoadBatchesAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading inventory batches...";

        try
        {
            bool? inStockOnly = FilterOption switch
            {
                1 => true,
                2 => false,
                _ => null
            };

            var query = new ListInventoryBatchesQuery(InStockOnly: inStockOnly, IsActive: true);
            var result = await _listBatchesHandler.HandleAsync(query);

            var items = result.IsSuccess ? result.Value : Array.Empty<BatchDto>();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                string term = SearchText.Trim().ToUpperInvariant();
                items = items.Where(b =>
                    b.BatchCode.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    b.MedicineName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (b.Location != null && b.Location.Contains(term, StringComparison.OrdinalIgnoreCase))).ToList();
            }

            Batches.Clear();
            foreach (var b in items)
            {
                Batches.Add(b);
            }

            StatusMessage = $"{Batches.Count} inventory batches loaded.";
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
    public void OpenAdjustModal()
    {
        if (!CanAdjustStock || SelectedBatch is null) return;

        AdjustNewQuantity = SelectedBatch.QuantityOnHand;
        SelectedAdjustmentTypeIndex = 0; // Damaged
        AdjustReason = string.Empty;
        AdjustErrorMessage = null;
        IsAdjustModalOpen = true;
    }

    [RelayCommand]
    public void CloseAdjustModal()
    {
        IsAdjustModalOpen = false;
        AdjustErrorMessage = null;
    }

    [RelayCommand]
    public async Task SubmitAdjustmentAsync()
    {
        if (!CanAdjustStock || SelectedBatch is null) return;

        if (string.IsNullOrWhiteSpace(AdjustReason))
        {
            AdjustErrorMessage = "Please provide an adjustment reason for audit compliance.";
            return;
        }

        if (AdjustNewQuantity < 0)
        {
            AdjustErrorMessage = "Quantity cannot be negative.";
            return;
        }

        if (AdjustNewQuantity == SelectedBatch.QuantityOnHand)
        {
            AdjustErrorMessage = "New quantity must differ from the current quantity.";
            return;
        }

        StockAdjustmentType adjType = SelectedAdjustmentTypeIndex switch
        {
            0 => StockAdjustmentType.Damaged,
            1 => StockAdjustmentType.ExpiredWriteOff,
            2 => StockAdjustmentType.PhysicalCountCorrection,
            3 => StockAdjustmentType.ReturnToSupplier,
            4 => StockAdjustmentType.TheftOrLoss,
            5 => StockAdjustmentType.ReturnFromCustomer,
            _ => StockAdjustmentType.PhysicalCountCorrection
        };

        IsBusy = true;
        AdjustErrorMessage = null;

        try
        {
            var command = new AdjustStockCommand(
                BatchId: SelectedBatch.Id,
                NewQuantity: AdjustNewQuantity,
                AdjustmentType: adjType,
                Reason: AdjustReason.Trim(),
                ExpectedVersion: SelectedBatch.Version);

            var result = await _adjustStockHandler.HandleAsync(command);
            if (result.IsFailure)
            {
                AdjustErrorMessage = result.Error;
                return;
            }

            await _dialogService.ShowMessageAsync("Stock Adjusted", $"Stock for batch '{SelectedBatch.BatchCode}' updated to {AdjustNewQuantity} units.");
            IsAdjustModalOpen = false;
            await LoadBatchesAsync();
        }
        catch (Exception ex)
        {
            AdjustErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
