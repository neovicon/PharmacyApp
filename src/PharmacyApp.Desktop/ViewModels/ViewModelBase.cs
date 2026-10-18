using CommunityToolkit.Mvvm.ComponentModel;

namespace PharmacyApp.Desktop.ViewModels;

/// <summary>
/// Abstract base for all ViewModels. Inherits ObservableObject from
/// CommunityToolkit.Mvvm, enabling source-generated property change
/// notification via [ObservableProperty].
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
}
