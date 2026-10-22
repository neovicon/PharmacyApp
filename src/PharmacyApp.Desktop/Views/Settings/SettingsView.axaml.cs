using Avalonia.Controls;
using PharmacyApp.Desktop.ViewModels.Settings;

namespace PharmacyApp.Desktop.Views.Settings;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            _ = vm.LoadAllCommand.ExecuteAsync(null);
        }
    }
}
