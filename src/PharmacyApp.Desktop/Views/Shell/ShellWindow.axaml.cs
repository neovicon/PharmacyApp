using Avalonia.Controls;
using Avalonia.Input;
using PharmacyApp.Desktop.ViewModels.Shell;

namespace PharmacyApp.Desktop.Views.Shell;

public partial class ShellWindow : Window
{
    public ShellWindow()
    {
        InitializeComponent();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        (DataContext as ShellViewModel)?.RecordActivity();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        (DataContext as ShellViewModel)?.RecordActivity();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        (DataContext as ShellViewModel)?.RecordActivity();
    }
}
