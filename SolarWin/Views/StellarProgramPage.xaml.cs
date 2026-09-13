using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SolarWin.ViewModels;
using Windows.System;

namespace SolarWin.Views;

public sealed partial class StellarProgramPage : Page
{
    private static readonly Uri PricingUri = new("https://solian.app/pricing");

    public StellarProgramViewModel ViewModel { get; }

    public StellarProgramPage()
    {
        ViewModel = App.Services.GetRequiredService<StellarProgramViewModel>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (ViewModel.LoadCommand.CanExecute(null))
        {
            ViewModel.LoadCommand.Execute(null);
        }
    }

    private async void OpenPricing_OnClick(object sender, RoutedEventArgs e)
        => await Launcher.LaunchUriAsync(PricingUri);

    private async void PlanSubscribe_OnClick(object sender, RoutedEventArgs e)
        => await Launcher.LaunchUriAsync(PricingUri);

    private async void CancelMembership_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "取消当前恒星方案？",
            Content = "取消后将停止后续续订。已经生效的权益可能会持续到当前周期结束。",
            PrimaryButtonText = "确认取消",
            CloseButtonText = "返回",
            DefaultButton = ContentDialogButton.Close,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary
            && ViewModel.CancelCurrentCommand.CanExecute(null))
        {
            await ViewModel.CancelCurrentCommand.ExecuteAsync(null);
        }
    }
}
