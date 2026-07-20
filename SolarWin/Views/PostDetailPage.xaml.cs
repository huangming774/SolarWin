using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SolarWin.ViewModels;

namespace SolarWin.Views;

public sealed partial class PostDetailPage : Page
{
    public PostDetailViewModel ViewModel { get; }

    public PostDetailPage()
    {
        ViewModel = App.Services.GetRequiredService<PostDetailViewModel>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is PostItemViewModel item)
        {
            ViewModel.Initialize(item);
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
            Frame?.Navigate(typeof(PostsPage));
        }
    }
}
