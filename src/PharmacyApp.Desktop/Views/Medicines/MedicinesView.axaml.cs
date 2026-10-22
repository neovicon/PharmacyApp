using Avalonia.Controls;
using Avalonia.Input;
using PharmacyApp.Desktop.ViewModels.Medicines;

namespace PharmacyApp.Desktop.Views.Medicines;

public partial class MedicinesView : UserControl
{
    public MedicinesView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is MedicinesViewModel vm)
            {
                await vm.LoadMedicinesAsync();
            }
        };
    }

    private void OnCatalogueSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down)
        {
            var list = this.FindControl<ListBox>("MedicinesListBox");
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
            if (DataContext is MedicinesViewModel vm && vm.Medicines.Count > 0)
            {
                vm.SelectedMedicine = vm.Medicines[0];
                e.Handled = true;
            }
        }
    }
}
