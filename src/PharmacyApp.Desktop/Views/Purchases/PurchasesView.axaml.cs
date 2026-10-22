using Avalonia.Controls;
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
}
