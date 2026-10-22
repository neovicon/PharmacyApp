using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Common;
using PharmacyApp.Application.Medicines;
using PharmacyApp.Application.Medicines.Commands;
using PharmacyApp.Application.Medicines.Queries;
using PharmacyApp.Domain.Enums;
using PharmacyApp.Desktop.Navigation;

namespace PharmacyApp.Desktop.ViewModels.Medicines;

public sealed partial class MedicinesViewModel : ViewModelBase, INavigationAware
{
    private readonly IQueryHandler<ListMedicinesQuery, IReadOnlyList<MedicineDto>> _listHandler;
    private readonly IQueryHandler<SearchMedicinesQuery, IReadOnlyList<MedicineDto>> _searchHandler;
    private readonly ICommandHandler<CreateMedicineCommand, Guid> _createHandler;
    private readonly ICommandHandler<UpdateMedicineCommand> _updateHandler;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _filterIndex = 0; // 0: All, 1: Active, 2: Inactive

    public IReadOnlyList<string> FilterOptions { get; } = new[] { "All Medicines", "Active Only", "Inactive Only" };

    [ObservableProperty]
    private MedicineDto? _selectedMedicine;

    [ObservableProperty]
    private bool _isFormOpen;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _formTitle = "Add New Medicine";

    [ObservableProperty]
    private string _formName = string.Empty;

    [ObservableProperty]
    private string _formGenericName = string.Empty;

    [ObservableProperty]
    private string _formDosageForm = "Tablet";

    [ObservableProperty]
    private string _formDispensingUnit = "Strip";

    [ObservableProperty]
    private int _formReorderLevel = 10;

    [ObservableProperty]
    private string? _formBrand;

    [ObservableProperty]
    private string? _formStrength;

    [ObservableProperty]
    private string? _formBarcode;

    [ObservableProperty]
    private string? _formCategory = "General";

    [ObservableProperty]
    private bool _formRequiresPrescription;

    [ObservableProperty]
    private string? _formErrorMessage;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<MedicineDto> Medicines { get; } = new();

    public bool CanManageMedicines =>
        !_currentUserService.IsAuthenticated ||
        _currentUserService.Role is UserRole.Admin or UserRole.Pharmacist;

    public MedicinesViewModel(
        IQueryHandler<ListMedicinesQuery, IReadOnlyList<MedicineDto>> listHandler,
        IQueryHandler<SearchMedicinesQuery, IReadOnlyList<MedicineDto>> searchHandler,
        ICommandHandler<CreateMedicineCommand, Guid> createHandler,
        ICommandHandler<UpdateMedicineCommand> updateHandler,
        ICurrentUserService currentUserService,
        IDialogService dialogService)
    {
        _listHandler = listHandler;
        _searchHandler = searchHandler;
        _createHandler = createHandler;
        _updateHandler = updateHandler;
        _currentUserService = currentUserService;
        _dialogService = dialogService;

        _ = LoadMedicinesAsync();
    }

    public Task OnNavigatedToAsync() => LoadMedicinesAsync();

    [RelayCommand]
    public async Task LoadMedicinesAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading medicine catalogue...";

        try
        {
            bool? isActiveFilter = FilterIndex switch
            {
                1 => true,
                2 => false,
                _ => null
            };

            IReadOnlyList<MedicineDto> results;
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                var queryResult = await _listHandler.HandleAsync(new ListMedicinesQuery(isActiveFilter));
                results = queryResult.IsSuccess ? queryResult.Value : Array.Empty<MedicineDto>();
            }
            else
            {
                var searchResult = await _searchHandler.HandleAsync(new SearchMedicinesQuery(SearchText.Trim(), isActiveFilter));
                results = searchResult.IsSuccess ? searchResult.Value : Array.Empty<MedicineDto>();
            }

            Medicines.Clear();
            foreach (var item in results)
            {
                Medicines.Add(item);
            }

            StatusMessage = $"{Medicines.Count} medicines loaded.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load medicines: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public void OpenCreateForm()
    {
        if (!CanManageMedicines) return;

        IsEditing = false;
        FormTitle = "Add Master Medicine";
        FormName = string.Empty;
        FormGenericName = string.Empty;
        FormDosageForm = "Tablet";
        FormDispensingUnit = "Strip";
        FormReorderLevel = 10;
        FormBrand = null;
        FormStrength = null;
        FormBarcode = null;
        FormCategory = "General";
        FormRequiresPrescription = false;
        FormErrorMessage = null;
        IsFormOpen = true;
    }

    [RelayCommand]
    public void OpenEditForm()
    {
        if (!CanManageMedicines || SelectedMedicine is null) return;

        IsEditing = true;
        FormTitle = $"Edit: {SelectedMedicine.Name}";
        FormName = SelectedMedicine.Name;
        FormGenericName = SelectedMedicine.GenericName;
        FormDosageForm = SelectedMedicine.DosageForm;
        FormDispensingUnit = SelectedMedicine.DispensingUnit;
        FormReorderLevel = SelectedMedicine.ReorderLevel;
        FormBrand = SelectedMedicine.Brand;
        FormStrength = SelectedMedicine.Strength;
        FormBarcode = SelectedMedicine.Barcode;
        FormCategory = SelectedMedicine.Category;
        FormRequiresPrescription = SelectedMedicine.RequiresPrescription;
        FormErrorMessage = null;
        IsFormOpen = true;
    }

    [RelayCommand]
    public void CloseForm()
    {
        IsFormOpen = false;
        FormErrorMessage = null;
    }

    [RelayCommand]
    public async Task SaveMedicineAsync()
    {
        if (!CanManageMedicines) return;

        FormErrorMessage = null;
        IsBusy = true;

        try
        {
            if (IsEditing)
            {
                if (SelectedMedicine is null) return;

                var command = new UpdateMedicineCommand(
                    Id: SelectedMedicine.Id,
                    Name: FormName,
                    GenericName: FormGenericName,
                    DosageForm: FormDosageForm,
                    DispensingUnit: FormDispensingUnit,
                    ReorderLevel: FormReorderLevel,
                    ExpectedVersion: SelectedMedicine.Version,
                    Brand: FormBrand,
                    Strength: FormStrength,
                    Barcode: FormBarcode,
                    Category: FormCategory,
                    RequiresPrescription: FormRequiresPrescription,
                    IsActive: SelectedMedicine.IsActive);

                var result = await _updateHandler.HandleAsync(command);
                if (result.IsFailure)
                {
                    FormErrorMessage = result.Error;
                    return;
                }

                await _dialogService.ShowMessageAsync("Catalogue Updated", $"Medicine '{FormName}' updated successfully.");
            }
            else
            {
                var command = new CreateMedicineCommand(
                    Name: FormName,
                    GenericName: FormGenericName,
                    DosageForm: FormDosageForm,
                    DispensingUnit: FormDispensingUnit,
                    ReorderLevel: FormReorderLevel,
                    Brand: FormBrand,
                    Strength: FormStrength,
                    Barcode: FormBarcode,
                    Category: FormCategory,
                    RequiresPrescription: FormRequiresPrescription);

                var result = await _createHandler.HandleAsync(command);
                if (result.IsFailure)
                {
                    FormErrorMessage = result.Error;
                    return;
                }

                await _dialogService.ShowMessageAsync("Medicine Created", $"Medicine '{FormName}' registered with catalogue ID: {result.Value}.");
            }

            IsFormOpen = false;
            await LoadMedicinesAsync();
        }
        catch (Exception ex)
        {
            FormErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ToggleActiveStatusAsync()
    {
        if (!CanManageMedicines || SelectedMedicine is null) return;

        string actionName = SelectedMedicine.IsActive ? "deactivate" : "reactivate";
        bool confirmed = await _dialogService.ShowConfirmationAsync(
            "Confirm Status Change",
            $"Are you sure you want to {actionName} '{SelectedMedicine.Name}'?");

        if (!confirmed) return;

        IsBusy = true;
        try
        {
            var command = new UpdateMedicineCommand(
                Id: SelectedMedicine.Id,
                Name: SelectedMedicine.Name,
                GenericName: SelectedMedicine.GenericName,
                DosageForm: SelectedMedicine.DosageForm,
                DispensingUnit: SelectedMedicine.DispensingUnit,
                ReorderLevel: SelectedMedicine.ReorderLevel,
                ExpectedVersion: SelectedMedicine.Version,
                Brand: SelectedMedicine.Brand,
                Strength: SelectedMedicine.Strength,
                Barcode: SelectedMedicine.Barcode,
                Category: SelectedMedicine.Category,
                RequiresPrescription: SelectedMedicine.RequiresPrescription,
                IsActive: !SelectedMedicine.IsActive);

            var result = await _updateHandler.HandleAsync(command);
            if (result.IsFailure)
            {
                await _dialogService.ShowErrorAsync("Action Failed", result.Error!);
                return;
            }

            await LoadMedicinesAsync();
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync("Error", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
