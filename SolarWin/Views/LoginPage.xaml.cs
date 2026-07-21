using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using SolarWin.ViewModels;
using Windows.System;

namespace SolarWin.Views;

public sealed partial class LoginPage : Page
{
    public LoginViewModel ViewModel { get; }

    public LoginPage()
    {
        ViewModel = App.Services.GetRequiredService<LoginViewModel>();
        InitializeComponent();
        ViewModel.LoginSucceeded += OnLoginSucceeded;
        ViewModel.NavigateToRegister += OnNavigateRegister;
        ViewModel.NavigateToRecover += OnNavigateRecover;
        Unloaded += OnUnloaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.XamlRoot = XamlRoot;
        if (e.Parameter is string account && !string.IsNullOrWhiteSpace(account))
        {
            ViewModel.Account = account;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.LoginSucceeded -= OnLoginSucceeded;
        ViewModel.NavigateToRegister -= OnNavigateRegister;
        ViewModel.NavigateToRecover -= OnNavigateRecover;
        ViewModel.CancelLoginCommand.Execute(null);
        Unloaded -= OnUnloaded;
    }

    private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        ViewModel.Password = PasswordBox.Password;
    }

    private void PasswordBox_OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ViewModel.LoginCommand.CanExecute(null))
        {
            ViewModel.LoginCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void FactorList_OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is FactorOptionItem item)
        {
            ViewModel.SelectFactorCommand.Execute(item);
        }
    }

    private void OnLoginSucceeded(object? sender, EventArgs e)
    {
        Frame?.Navigate(typeof(ShellPage));
    }

    private void OnNavigateRegister(object? sender, EventArgs e)
        => Frame?.Navigate(typeof(RegisterPage));

    private void OnNavigateRecover(object? sender, EventArgs e)
        => Frame?.Navigate(typeof(RecoverPage), ViewModel.Account);
}
