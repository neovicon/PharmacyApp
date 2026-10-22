using Avalonia.Controls;
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
}
