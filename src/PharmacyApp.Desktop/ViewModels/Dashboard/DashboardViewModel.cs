namespace PharmacyApp.Desktop.ViewModels.Dashboard;

/// <summary>
/// Dashboard page — the default landing page shown after the shell loads.
/// Business widgets and summary data will be added in later milestones.
/// </summary>
public sealed partial class DashboardViewModel : ViewModelBase
{
    public string PageTitle => "Dashboard";
    public string StatusMessage => "Milestone 0 — Foundation complete. Business features coming in later milestones.";
}
