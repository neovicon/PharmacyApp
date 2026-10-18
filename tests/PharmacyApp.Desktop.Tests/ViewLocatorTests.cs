using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using PharmacyApp.Desktop;
using PharmacyApp.Desktop.ViewModels;
using PharmacyApp.Desktop.ViewModels.Dashboard;
using PharmacyApp.Desktop.Views.Dashboard;
using Xunit;

namespace PharmacyApp.Desktop.Tests;

public class ViewLocatorTests
{
    private readonly ViewLocator _locator = new();

    [Fact]
    public void Match_ReturnsTrueForViewModelBase()
    {
        var vm = new DashboardViewModel();
        Assert.True(_locator.Match(vm));
    }

    [Fact]
    public void Match_ReturnsFalseForNonViewModel()
    {
        Assert.False(_locator.Match("plain string"));
        Assert.False(_locator.Match(null));
        Assert.False(_locator.Match(123));
    }

    [Fact]
    public void Build_ReturnsNullWhenParamIsNull()
    {
        Assert.Null(_locator.Build(null));
    }

    [Fact]
    public void Build_ReturnsTextBlock_WhenViewNotFound()
    {
        var unknownVm = new UnknownTestViewModel();
        Control? control = _locator.Build(unknownVm);

        Assert.NotNull(control);
        var textBlock = Assert.IsType<TextBlock>(control);
        Assert.Contains("View not found", textBlock.Text);
    }

    [Fact]
    public void Build_WhenAppServiceProviderPresentAndRegistered_ResolvesFromDi()
    {
        var services = new ServiceCollection();
        services.AddTransient<DashboardView>();
        var provider = services.BuildServiceProvider();

        var previousProvider = App.ServiceProvider;
        try
        {
            App.ServiceProvider = provider;

            var vm = new DashboardViewModel();
            Control? control = _locator.Build(vm);

            Assert.NotNull(control);
            Assert.IsType<DashboardView>(control);
        }
        finally
        {
            App.ServiceProvider = previousProvider;
        }
    }

    [Fact]
    public void Build_WhenAppServiceProviderPresentAndNotRegistered_FailsClearlyWithoutSilentFallback()
    {
        // An empty service provider where DashboardView is intentionally NOT registered
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();

        var previousProvider = App.ServiceProvider;
        try
        {
            App.ServiceProvider = provider;

            var vm = new DashboardViewModel();
            Control? control = _locator.Build(vm);

            Assert.NotNull(control);
            var textBlock = Assert.IsType<TextBlock>(control);
            Assert.Contains("View not registered in DI container", textBlock.Text);
        }
        finally
        {
            App.ServiceProvider = previousProvider;
        }
    }

    private sealed class UnknownTestViewModel : ViewModelBase { }
}
