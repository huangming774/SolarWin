using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SolarWin.Models;
using SolarWin.Services;
using SolarWin.ViewModels;

namespace SolarWin.Views;

public sealed partial class PostDetailPage : Page
{
    private readonly IToastService _toast;

    public PostDetailViewModel ViewModel { get; }

    public PostDetailPage()
    {
        ViewModel = App.Services.GetRequiredService<PostDetailViewModel>();
        _toast = App.Services.GetRequiredService<IToastService>();
        InitializeComponent();
        ViewModel.NavigateToUserProfile += OnNavigateToUserProfile;
        Unloaded += OnUnloaded;
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

    private void ReplyAuthorAvatar_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: PostItemViewModel item })
        {
            OpenReplyAuthor(item);
        }
    }

    private void ReplyAuthorName_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: PostItemViewModel item })
        {
            OpenReplyAuthor(item);
        }
    }

    private void OpenReplyAuthor(PostItemViewModel item)
    {
        var args = item.TryCreateAuthorProfileArgs();
        if (args is null)
        {
            _toast.Warning("无法打开主页：缺少用户名");
            return;
        }

        Frame?.Navigate(typeof(UserProfilePage), args);
    }

    private void OnNavigateToUserProfile(object? sender, UserProfileNavArgs args)
    {
        Frame?.Navigate(typeof(UserProfilePage), args);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.NavigateToUserProfile -= OnNavigateToUserProfile;
        Unloaded -= OnUnloaded;
    }
}
