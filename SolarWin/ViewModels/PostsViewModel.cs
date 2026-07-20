using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using SolarWin.Helpers;
using SolarWin.Models;
using SolarWin.Services;

namespace SolarWin.ViewModels;

public partial class PostsViewModel : ObservableObject
{
    private const int PageSize = 20;

    private readonly ISolarApiClient _api;
    private readonly IToastService _toast;
    private readonly DysonFileImageLoader _imageLoader;
    private readonly IAuthService _auth;

    private int _offset;
    private string? _publisherName;
    private bool _publisherResolved;

    public PostsViewModel(
        ISolarApiClient api,
        IToastService toast,
        DysonFileImageLoader imageLoader,
        IAuthService auth)
    {
        _api = api;
        _toast = toast;
        _imageLoader = imageLoader;
        _auth = auth;
    }

    public ObservableCollection<PostItemViewModel> Items { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(ShowContent))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(ShowContent))]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool IsLoadingMore { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoadMoreVisibility))]
    public partial bool HasMore { get; set; } = true;

    public Visibility LoadMoreVisibility => HasMore ? Visibility.Visible : Visibility.Collapsed;

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPost))]
    public partial string NewPostContent { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPost))]
    public partial bool IsPosting { get; set; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsEmpty => !IsBusy && !HasError && Items.Count == 0;

    public bool ShowContent => !IsBusy && !HasError && Items.Count > 0;

    public bool CanPost => !IsPosting && !string.IsNullOrWhiteSpace(NewPostContent);

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            Items.Clear();
            _offset = 0;
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(ShowContent));

            var list = await _api.GetPostsAsync(0, PageSize).ConfigureAwait(true);
            foreach (var post in list)
            {
                Items.Add(new PostItemViewModel(post, _imageLoader));
            }

            _offset = Items.Count;
            HasMore = list.Count >= PageSize;
            StatusText = $"共 {Items.Count} 条";
            _ = LoadImagesAsync();
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.Message;
            StatusText = "加载失败";
            _toast.Error("帖子加载失败");
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(ShowContent));
            OnPropertyChanged(nameof(HasError));
        }
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (IsLoadingMore || IsBusy || !HasMore)
        {
            return;
        }

        try
        {
            IsLoadingMore = true;
            var list = await _api.GetPostsAsync(_offset, PageSize).ConfigureAwait(true);
            foreach (var post in list)
            {
                Items.Add(new PostItemViewModel(post, _imageLoader));
            }

            _offset += list.Count;
            HasMore = list.Count >= PageSize;
            StatusText = $"共 {Items.Count} 条";
            _ = LoadImagesAsync();
        }
        catch (SolarApiException ex)
        {
            _toast.Error($"加载更多失败:{ex.Message}");
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    [RelayCommand]
    private async Task CreatePostAsync()
    {
        var content = NewPostContent.Trim();
        if (content.Length == 0 || IsPosting)
        {
            return;
        }

        try
        {
            IsPosting = true;
            ErrorMessage = null;

            var pub = await ResolvePublisherNameAsync().ConfigureAwait(true);
            var request = new CreatePostRequest
            {
                Content = content,
                Visibility = 0,
                Type = 0,
            };

            var created = await _api.CreatePostAsync(request, pub).ConfigureAwait(true);
            Items.Insert(0, new PostItemViewModel(created, _imageLoader));
            NewPostContent = string.Empty;
            StatusText = $"共 {Items.Count} 条";
            _ = LoadImagesAsync();
            _toast.Success("已发布");
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.Message;
            _toast.Error("发布失败");
        }
        finally
        {
            IsPosting = false;
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(ShowContent));
            OnPropertyChanged(nameof(HasError));
        }
    }

    /// <summary>
    /// Resolve the account's own publisher name for the pub= query param.
    /// Falls back to null (server uses the default publisher) when lookup fails.
    /// </summary>
    private async Task<string?> ResolvePublisherNameAsync()
    {
        if (_publisherResolved)
        {
            return _publisherName;
        }

        _publisherResolved = true;
        var accountId = _auth.CurrentAccount?.Id ?? Guid.Empty;
        if (accountId == Guid.Empty)
        {
            return null;
        }

        try
        {
            var publishers = await _api.GetAccountPublishersAsync(accountId).ConfigureAwait(true);
            _publisherName = publishers.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.Name))?.Name;
        }
        catch (SolarApiException)
        {
            // Best-effort; post without pub.
        }

        return _publisherName;
    }

    private bool _imagesLoading;

    private async Task LoadImagesAsync()
    {
        // Single-flight: load/load-more/create can all trigger this concurrently.
        if (_imagesLoading)
        {
            return;
        }

        _imagesLoading = true;
        try
        {
            var avatarTasks = new List<(PostItemViewModel Item, Task<BitmapImage?> Task)>();
            var imageTasks = new List<(PostItemViewModel Item, Task<BitmapImage?> Task)>();

            foreach (var item in Items.ToList())
            {
                if (item.HasAvatar && item.AvatarImage is null && !string.IsNullOrWhiteSpace(item.AvatarUrl))
                {
                    avatarTasks.Add((item, _imageLoader.LoadSafeAsync(item.AvatarUrl)));
                }

                if (item.HasImages && item.FirstImage is null)
                {
                    imageTasks.Add((item, _imageLoader.LoadSafeAsync(item.ImageUrls[0])));
                }
            }

            if (avatarTasks.Count == 0 && imageTasks.Count == 0)
            {
                return;
            }

            // Parallel download; apply in one UI turn so the feed doesn't ripple.
            await Task.WhenAll(
                avatarTasks.Select(t => t.Task)
                    .Concat(imageTasks.Select(t => t.Task))).ConfigureAwait(true);

            foreach (var (item, task) in avatarTasks)
            {
                if (task.Result is { } bmp)
                {
                    item.AvatarImage = bmp;
                }
            }

            foreach (var (item, task) in imageTasks)
            {
                if (task.Result is { } bmp)
                {
                    item.FirstImage = bmp;
                }
            }
        }
        finally
        {
            _imagesLoading = false;
        }
    }
}
