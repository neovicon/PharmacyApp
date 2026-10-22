using Avalonia.Controls;
using Avalonia.Input;
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

    private void OnMedicineSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down)
        {
            var list = this.FindControl<ListBox>("MatchedMedicinesListBox");
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
            if (DataContext is PurchasesViewModel vm && vm.FilteredMedicines.Count > 0)
            {
                vm.SelectedMedicine = vm.FilteredMedicines[0];
                e.Handled = true;
            }
        }
    }

    private void OnMatchedMedicinesKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var batchBox = this.FindControl<TextBox>("BatchCodeInput");
            batchBox?.Focus();
            e.Handled = true;
        }
    }
}
