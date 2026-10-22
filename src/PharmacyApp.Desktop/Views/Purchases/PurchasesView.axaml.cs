using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using PharmacyApp.Desktop.ViewModels.Purchases;

namespace PharmacyApp.Desktop.Views.Purchases;

public partial class PurchasesView : UserControl
{
    public PurchasesView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is PurchasesViewModel vm)
            {
                await vm.InitializeAsync();
            }
        };
    }

    private void OnMedicineSearchGotFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PurchasesViewModel vm &&
            !string.IsNullOrWhiteSpace(vm.MedicineSearchText) &&
            vm.FilteredMedicines.Count > 0 &&
            vm.SelectedMedicine == null)
        {
            vm.IsSuggestionsOpen = true;
        }
    }

    private ListBox? GetMatchedMedicinesList()
    {
        var list = this.FindControl<ListBox>("MatchedMedicinesListBox");
        if (list != null) return list;

        var popup = this.FindControl<Popup>("MedicineSuggestionsPopup");
        return popup?.Child?.FindControl<ListBox>("MatchedMedicinesListBox");
    }

    private void OnMedicineSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down)
        {
            if (DataContext is PurchasesViewModel vm && vm.FilteredMedicines.Count > 0)
            {
                vm.IsSuggestionsOpen = true;
                var list = GetMatchedMedicinesList();
                if (list != null)
                {
                    list.Focus();
                    if (list.ItemCount > 0 && list.SelectedIndex < 0)
                    {
                        list.SelectedIndex = 0;
                    }
                    e.Handled = true;
                }
            }
        }
        else if (e.Key == Key.Enter)
        {
            if (DataContext is PurchasesViewModel vm && vm.FilteredMedicines.Count > 0)
            {
                vm.SelectedMedicine = vm.FilteredMedicines[0];
                vm.IsSuggestionsOpen = false;
                var batchBox = this.FindControl<TextBox>("BatchCodeInput");
                batchBox?.Focus();
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Escape)
        {
            if (DataContext is PurchasesViewModel vm)
            {
                vm.IsSuggestionsOpen = false;
                e.Handled = true;
            }
        }
    }

    private void OnMatchedMedicinesKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (DataContext is PurchasesViewModel vm)
            {
                vm.IsSuggestionsOpen = false;
            }
            var batchBox = this.FindControl<TextBox>("BatchCodeInput");
            batchBox?.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (DataContext is PurchasesViewModel vm)
            {
                vm.IsSuggestionsOpen = false;
            }
            var searchBox = this.FindControl<TextBox>("PurchasesMedicineSearchBox");
            searchBox?.Focus();
            e.Handled = true;
        }
    }
}
