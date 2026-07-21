using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SolarWin.Models;
using SolarWin.Services;
using SolarWin.ViewModels;

namespace SolarWin.Views;

public sealed partial class PostFeedPage : Page
{
    private readonly IToastService _toast;

    public PostFeedViewModel ViewModel { get; }

    public PostFeedPage()
    {
        ViewModel = App.Services.GetRequiredService<PostFeedViewModel>();
        _toast = App.Services.GetRequiredService<IToastService>();
        InitializeComponent();
        ViewModel.OpenPost += OnOpenPost;
        Unloaded += OnUnloaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is PostFeedNavArgs args)
        {
            ViewModel.Initialize(args);
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

    private void PostList_OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is PostItemViewModel item)
        {
            ViewModel.OpenItemCommand.Execute(item);
        }
    }

    private void AuthorName_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: PostItemViewModel item })
        {
            var args = item.TryCreateAuthorProfileArgs();
            if (args is null)
            {
                _toast.Warning("无法打开主页：缺少用户名");
                return;
            }

            Frame?.Navigate(typeof(UserProfilePage), args);
        }
    }

    private void OnOpenPost(object? sender, PostItemViewModel item)
    {
        Frame?.Navigate(typeof(PostDetailPage), item);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenPost -= OnOpenPost;
        Unloaded -= OnUnloaded;
    }
}
