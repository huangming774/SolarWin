using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SolarWin.Models;
using SolarWin.ViewModels;

namespace SolarWin.Views;

public sealed partial class PublisherDetailPage : Page
{
    public PublisherDetailViewModel ViewModel { get; }

    public PublisherDetailPage()
    {
        ViewModel = App.Services.GetRequiredService<PublisherDetailViewModel>();
        InitializeComponent();
        ViewModel.NavigateToFeed += OnNavigateToFeed;
        Unloaded += OnUnloaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        switch (e.Parameter)
        {
            case PublisherNavArgs args:
                ViewModel.Initialize(args);
                break;
            case string name when !string.IsNullOrWhiteSpace(name):
                ViewModel.Initialize(new PublisherNavArgs(name));
                break;
            case SnPublisher pub when !string.IsNullOrWhiteSpace(pub.Name):
                ViewModel.Initialize(new PublisherNavArgs(pub.Name!, pub.Nick));
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
            Frame?.Navigate(typeof(SphereExplorePage));
        }
    }

    private void OnNavigateToFeed(object? sender, PostFeedNavArgs args)
    {
        Frame?.Navigate(typeof(PostFeedPage), args);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.NavigateToFeed -= OnNavigateToFeed;
        Unloaded -= OnUnloaded;
    }
}
