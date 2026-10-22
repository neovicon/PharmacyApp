using Avalonia.Controls;
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
}
