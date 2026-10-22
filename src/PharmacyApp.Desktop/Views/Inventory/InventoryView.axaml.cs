using Avalonia.Controls;
using Avalonia.Input;
using PharmacyApp.Desktop.ViewModels.Inventory;

namespace PharmacyApp.Desktop.Views.Inventory;

public partial class InventoryView : UserControl
{
    public InventoryView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is InventoryViewModel vm)
            {
                await vm.LoadBatchesAsync();
            }
        };
    }

    private void OnInventorySearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down)
        {
            var list = this.FindControl<ListBox>("BatchesListBox");
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
            if (DataContext is InventoryViewModel vm && vm.Batches.Count > 0)
            {
                vm.SelectedBatch = vm.Batches[0];
                e.Handled = true;
            }
        }
    }
}
