using Avalonia.Controls;
using PharmacyApp.Desktop.ViewModels.Reports;

namespace PharmacyApp.Desktop.Views.Reports;

public partial class ReportsView : UserControl
{
    public ReportsView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is ReportsViewModel vm)
            {
                await vm.LoadReportsAsync();
            }
        };
    }
}
