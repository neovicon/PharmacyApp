using Avalonia.Controls;
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
}
