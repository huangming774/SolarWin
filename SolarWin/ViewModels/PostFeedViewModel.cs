using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using SolarWin.Helpers;
using SolarWin.Models;
using SolarWin.Services;

namespace SolarWin.ViewModels;

/// <summary>Filtered post list: tag / category / collection / publisher.</summary>
public partial class PostFeedViewModel : ObservableObject
{
    private const int PageSize = 20;

    private readonly ISolarApiClient _api;
    private readonly IToastService _toast;
    private readonly DysonFileImageLoader _imageLoader;

    private PostFeedNavArgs? _args;
    private int _offset;
    private bool _hasMore = true;

    public PostFeedViewModel(
        ISolarApiClient api,
        IToastService toast,
        DysonFileImageLoader imageLoader)
    {
        _api = api;
        _toast = toast;
        _imageLoader = imageLoader;
    }

    public ObservableCollection<PostItemViewModel> Items { get; } = [];

    [ObservableProperty]
    public partial string Title { get; set; } = "帖子流";

    [ObservableProperty]
    public partial string Subtitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsLoadingMore { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsSubscribed { get; set; }

    [ObservableProperty]
    public partial string SubscribeButtonText { get; set; } = "订阅";

    [ObservableProperty]
    public partial bool CanSubscribe { get; set; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsEmpty => !IsBusy && !HasError && Items.Count == 0;

    public Visibility EmptyVisibility => IsEmpty ? Visibility.Visible : Visibility.Collapsed;

    public Visibility LoadMoreVisibility => _hasMore && !IsBusy ? Visibility.Visible : Visibility.Collapsed;

    public event EventHandler<PostItemViewModel>? OpenPost;

    public void Initialize(PostFeedNavArgs? args)
    {
        _args = args;
        if (args is null || string.IsNullOrWhiteSpace(args.Key))
        {
            ErrorMessage = "缺少筛选条件";
            return;
        }

        Title = args.Title is { Length: > 0 } t
            ? t
            : args.Kind switch
            {
                PostFeedKind.Tag => $"标签 · {args.Key}",
                PostFeedKind.Category => $"分类 · {args.Key}",
                PostFeedKind.Collection => $"合集 · {args.Key}",
                PostFeedKind.Publisher => $"发布者 · {args.Key}",
                _ => args.Key,
            };

        Subtitle = args.Kind switch
        {
            PostFeedKind.Tag => $"tag:{args.Key}",
            PostFeedKind.Category => $"category:{args.Key}",
            PostFeedKind.Collection => $"{args.PublisherName}/{args.Key}",
            PostFeedKind.Publisher => $"@{args.Key}",
            _ => args.Key,
        };

        CanSubscribe = args.Kind is PostFeedKind.Tag or PostFeedKind.Category or PostFeedKind.Collection
            or PostFeedKind.Publisher;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (_args is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            Items.Clear();
            _offset = 0;
            _hasMore = true;
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(EmptyVisibility));
            OnPropertyChanged(nameof(LoadMoreVisibility));

            var list = await FetchPageAsync().ConfigureAwait(true);
            foreach (var post in list)
            {
                Items.Add(new PostItemViewModel(post, _imageLoader));
            }

            StatusText = $"{Items.Count} 条";
            await RefreshSubscribeStateAsync().ConfigureAwait(true);
            _ = LoadImagesAsync();
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.Message;
            StatusText = "加载失败";
            _toast.Error(ex.Message);
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(EmptyVisibility));
            OnPropertyChanged(nameof(LoadMoreVisibility));
        }
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (_args is null || IsLoadingMore || IsBusy || !_hasMore)
        {
            return;
        }

        try
        {
            IsLoadingMore = true;
            var list = await FetchPageAsync().ConfigureAwait(true);
            foreach (var post in list)
            {
                Items.Add(new PostItemViewModel(post, _imageLoader));
            }

            StatusText = $"{Items.Count} 条";
            _ = LoadImagesAsync();
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
        finally
        {
            IsLoadingMore = false;
            OnPropertyChanged(nameof(LoadMoreVisibility));
        }
    }

    [RelayCommand]
    private void OpenItem(PostItemViewModel? item)
    {
        if (item is not null)
        {
            OpenPost?.Invoke(this, item);
        }
    }

    [RelayCommand]
    private async Task ToggleSubscribeAsync()
    {
        if (_args is null || !CanSubscribe)
        {
            return;
        }

        try
        {
            if (IsSubscribed)
            {
                await UnsubscribeCurrentAsync().ConfigureAwait(true);
                IsSubscribed = false;
                SubscribeButtonText = "订阅";
                _toast.Success("已取消订阅");
            }
            else
            {
                await SubscribeCurrentAsync().ConfigureAwait(true);
                IsSubscribed = true;
                SubscribeButtonText = "已订阅";
                _toast.Success("已订阅");
            }
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    private async Task<List<SnPost>> FetchPageAsync()
    {
        var args = _args!;
        List<SnPost> list;
        switch (args.Kind)
        {
            case PostFeedKind.Collection:
                if (string.IsNullOrWhiteSpace(args.PublisherName))
                {
                    throw new SolarApiException("合集流需要 publisher name。");
                }

                // Collection endpoint typically returns all items; page client-side.
                if (_offset == 0)
                {
                    list = await _api.GetCollectionPostsAsync(args.PublisherName, args.Key).ConfigureAwait(true);
                    _hasMore = false;
                }
                else
                {
                    list = [];
                    _hasMore = false;
                }

                break;

            case PostFeedKind.Tag:
                list = await _api.GetPostsAsync(
                    _offset,
                    PageSize,
                    tag: args.Key).ConfigureAwait(true);
                _hasMore = list.Count >= PageSize;
                _offset += list.Count;
                break;

            case PostFeedKind.Category:
                list = await _api.GetPostsAsync(
                    _offset,
                    PageSize,
                    category: args.Key).ConfigureAwait(true);
                _hasMore = list.Count >= PageSize;
                _offset += list.Count;
                break;

            case PostFeedKind.Publisher:
                list = await _api.GetPostsAsync(
                    _offset,
                    PageSize,
                    pub: args.Key).ConfigureAwait(true);
                _hasMore = list.Count >= PageSize;
                _offset += list.Count;
                break;

            default:
                list = [];
                _hasMore = false;
                break;
        }

        return list;
    }

    private async Task RefreshSubscribeStateAsync()
    {
        if (_args is null || !CanSubscribe)
        {
            IsSubscribed = false;
            SubscribeButtonText = "订阅";
            return;
        }

        // Best-effort: subscription GET is not always available for all kinds.
        // Keep button default; user can toggle.
        IsSubscribed = false;
        SubscribeButtonText = "订阅";
        await Task.CompletedTask.ConfigureAwait(true);
    }

    private Task SubscribeCurrentAsync()
    {
        var args = _args!;
        return args.Kind switch
        {
            PostFeedKind.Tag => _api.SubscribeTagAsync(args.Key),
            PostFeedKind.Category => _api.SubscribeCategoryAsync(args.Key),
            PostFeedKind.Collection => _api.SubscribeCollectionAsync(args.PublisherName!, args.Key),
            PostFeedKind.Publisher => _api.SubscribePublisherAsync(args.Key),
            _ => Task.CompletedTask,
        };
    }

    private Task UnsubscribeCurrentAsync()
    {
        var args = _args!;
        return args.Kind switch
        {
            PostFeedKind.Tag => _api.UnsubscribeTagAsync(args.Key),
            PostFeedKind.Category => _api.UnsubscribeCategoryAsync(args.Key),
            PostFeedKind.Collection => _api.UnsubscribeCollectionAsync(args.PublisherName!, args.Key),
            PostFeedKind.Publisher => _api.UnsubscribePublisherAsync(args.Key),
            _ => Task.CompletedTask,
        };
    }

    private async Task LoadImagesAsync()
    {
        foreach (var item in Items.ToList())
        {
            if (item.HasAvatar && item.AvatarImage is null && !string.IsNullOrWhiteSpace(item.AvatarUrl))
            {
                try
                {
                    var bmp = await _imageLoader.LoadAsync(item.AvatarUrl).ConfigureAwait(true);
                    if (bmp is not null)
                    {
                        item.AvatarImage = bmp;
                    }
                }
                catch
                {
                    // ignore image errors
                }
            }
        }
    }
}
