using Avalonia.Controls;
using PharmacyApp.Desktop.ViewModels.Dashboard;

namespace PharmacyApp.Desktop.Views.Dashboard;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is DashboardViewModel vm)
            {
                await vm.LoadDashboardAsync();
            }
        };
    }
}
