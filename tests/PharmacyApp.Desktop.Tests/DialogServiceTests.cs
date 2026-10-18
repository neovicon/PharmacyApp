using PharmacyApp.Application.Abstractions;
using PharmacyApp.Desktop.Services;
using Xunit;

namespace PharmacyApp.Desktop.Tests;

public class DialogServiceTests
{
    private readonly IDialogService _dialogService = new DesktopDialogService();

    [Fact]
    public async Task ShowMessageAsync_CompletesSuccessfully()
    {
        var ex = await Record.ExceptionAsync(() => _dialogService.ShowMessageAsync("Notice", "Test message"));
        Assert.Null(ex);
    }

    [Fact]
    public async Task ShowErrorAsync_CompletesSuccessfully()
    {
        var ex = await Record.ExceptionAsync(() => _dialogService.ShowErrorAsync("Error", "Test error"));
        Assert.Null(ex);
    }

    [Fact]
    public async Task ShowConfirmationAsync_ReturnsTrueInMilestoneZero()
    {
        bool confirmed = await _dialogService.ShowConfirmationAsync("Confirm", "Proceed with action?");
        Assert.True(confirmed);
    }
}
