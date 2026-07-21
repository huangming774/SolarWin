using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SolarWin.Models;
using SolarWin.ViewModels;

namespace SolarWin.Views;

public sealed partial class RealmDetailPage : Page
{
    public RealmDetailViewModel ViewModel { get; }

    public RealmDetailPage()
    {
        ViewModel = App.Services.GetRequiredService<RealmDetailViewModel>();
        InitializeComponent();
        ViewModel.OpenUserProfile += OnOpenUserProfile;
        Unloaded += (_, _) => ViewModel.OpenUserProfile -= OnOpenUserProfile;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        switch (e.Parameter)
        {
            case RealmDetailNavArgs args:
                ViewModel.Initialize(args);
                break;
            case string slug when !string.IsNullOrWhiteSpace(slug):
                ViewModel.Initialize(new RealmDetailNavArgs(slug));
                break;
            case SnRealm realm when !string.IsNullOrWhiteSpace(realm.Slug):
                ViewModel.Initialize(new RealmDetailNavArgs(realm.Slug!, realm.Name));
                break;
        }
    }

    private void BackButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (Frame?.CanGoBack == true)
        {
            Frame.GoBack();
        }
        else
        {
            Frame?.Navigate(typeof(HomePage));
        }
    }

    private void OnOpenUserProfile(object? sender, UserProfileNavArgs args)
    {
        Frame?.Navigate(typeof(UserProfilePage), args);
    }
}
