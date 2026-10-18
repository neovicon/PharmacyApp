using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;

namespace PharmacyApp.Desktop.ViewModels.Shell;

/// <summary>
/// Represents a single item in the sidebar navigation list.
/// Observes selection state to visibly highlight the active navigation destination.
/// </summary>
public sealed partial class NavItemViewModel : ViewModelBase
{
    public string Label { get; }

    /// <summary>
    /// Unicode glyph used as a simple icon substitute until proper icon assets
    /// are added. Replace with an image source or icon font in a later milestone.
    /// </summary>
    public string IconGlyph { get; }

    public Type TargetViewModelType { get; }

    public ICommand NavigateCommand { get; }

    [ObservableProperty]
    private bool _isSelected;

    public NavItemViewModel(string label, string iconGlyph, Type targetViewModelType, ICommand navigateCommand)
    {
        Label = label;
        IconGlyph = iconGlyph;
        TargetViewModelType = targetViewModelType;
        NavigateCommand = navigateCommand;
    }
}
