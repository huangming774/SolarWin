using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SolarWin.ViewModels;

namespace SolarWin.Views;

public sealed partial class SecurityPage : Page
{
    public SecurityViewModel ViewModel { get; }

    public SecurityPage()
    {
        // x:Bind DataContext for nested Command bindings that use ElementName=RootPage
        ViewModel = App.Services.GetRequiredService<SecurityViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.XamlRoot = XamlRoot;
        if (ViewModel.LoadCommand.CanExecute(null))
        {
            ViewModel.LoadCommand.Execute(null);
        }
    }

    private void RequestContactVerify_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ContactRowViewModel row })
        {
            ViewModel.RequestContactVerifyCommand.Execute(row);
        }
    }

    private void VerifyContact_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ContactRowViewModel row })
        {
            ViewModel.VerifyContactCodeCommand.Execute(row);
        }
    }

    private void Back_OnClick(object sender, RoutedEventArgs e)
    {
        if (Frame?.CanGoBack == true)
        {
            Frame.GoBack();
        }
        else
        {
            Frame?.Navigate(typeof(SettingsPage));
        }
    }

    private void ApproveChallenge_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: PendingChallengeRowViewModel row })
        {
            ViewModel.ApproveChallengeCommand.Execute(row);
        }
    }

    private void DeclineChallenge_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: PendingChallengeRowViewModel row })
        {
            ViewModel.DeclineChallengeCommand.Execute(row);
        }
    }

    private void SaveDeviceLabel_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: DeviceRowViewModel row })
        {
            ViewModel.SaveDeviceLabelCommand.Execute(row);
        }
    }

    private void DeleteDevice_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: DeviceRowViewModel row })
        {
            ViewModel.DeleteDeviceCommand.Execute(row);
        }
    }

    private void RevokeSession_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: SessionRowViewModel row })
        {
            ViewModel.RevokeSessionCommand.Execute(row);
        }
    }

    private void EnableFactor_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: FactorRowViewModel row })
        {
            ViewModel.EnableFactorCommand.Execute(row);
        }
    }

    private void DisableFactor_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: FactorRowViewModel row })
        {
            ViewModel.DisableFactorCommand.Execute(row);
        }
    }

    private void DeleteFactor_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: FactorRowViewModel row })
        {
            ViewModel.DeleteFactorCommand.Execute(row);
        }
    }

    private void SetPrimaryContact_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ContactRowViewModel row })
        {
            ViewModel.SetPrimaryContactCommand.Execute(row);
        }
    }

    private void DeleteContact_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ContactRowViewModel row })
        {
            ViewModel.DeleteContactCommand.Execute(row);
        }
    }

    private void RevokeApp_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: AppRowViewModel row })
        {
            ViewModel.RevokeAppCommand.Execute(row);
        }
    }

    private void RotateApiKey_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ApiKeyRowViewModel row })
        {
            ViewModel.RotateApiKeyCommand.Execute(row);
        }
    }

    private void DeleteApiKey_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ApiKeyRowViewModel row })
        {
            ViewModel.DeleteApiKeyCommand.Execute(row);
        }
    }

    private void ToggleConnection_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ConnectionRowViewModel row })
        {
            ViewModel.ToggleConnectionVisibilityCommand.Execute(row);
        }
    }

    private void DeleteConnection_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ConnectionRowViewModel row })
        {
            ViewModel.DeleteConnectionCommand.Execute(row);
        }
    }
}
