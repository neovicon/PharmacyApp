using Avalonia.Controls;
using Avalonia.Input;
using PharmacyApp.Desktop.ViewModels.Sales;

namespace PharmacyApp.Desktop.Views.Sales;

public partial class SalesView : UserControl
{
    public SalesView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is SalesViewModel vm && string.IsNullOrWhiteSpace(vm.SearchText))
            {
                await vm.LoadInitialMedicinesAsync();
            }
        };
    }

    private void OnSalesSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down)
        {
            var list = this.FindControl<ListBox>("SalesSearchResultsList");
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
        else if (e.Key == Key.Enter)
        {
            if (DataContext is SalesViewModel vm && vm.SearchResults.Count > 0)
            {
                vm.SelectedMedicine = vm.SearchResults[0];
                e.Handled = true;
            }
        }
    }

    private void OnSalesSearchResultsKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var batchCombo = this.FindControl<ComboBox>("BatchSelectorComboBox");
            batchCombo?.Focus();
            e.Handled = true;
        }
    }
}
