using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using SolarWin.Helpers;
using SolarWin.Models;
using SolarWin.Services;

namespace SolarWin.ViewModels;

/// <summary>
/// Filtered post list: tag / category / collection / publisher.
/// Page must call <see cref="Cleanup"/> from <c>OnNavigatedFrom</c> so in-flight fetches
/// are cancelled and list items (URL bindings for any GpuImage) are released.
/// </summary>
public partial class PostFeedViewModel : ObservableObject
{
    private const int PageSize = 20;
    private const int MaxFeedItems = 120;

    private readonly ISolarApiClient _api;
    private readonly IToastService _toast;
    private readonly DysonFileImageLoader _imageLoader;

    /// <summary>One CTS per page session; cancelled in <see cref="Cleanup"/>.</summary>
    private CancellationTokenSource? _sessionCts;

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
        BeginSession();
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

    /// <summary>
    /// Cancel load/load-more, drop list items and legacy image slots so nothing keeps running off-page.
    /// </summary>
    public void Cleanup()
    {
        CancelSession();
        ReleaseMediaBindings();
        IsBusy = false;
        IsLoadingMore = false;
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyVisibility));
        OnPropertyChanged(nameof(LoadMoreVisibility));
    }

    private void BeginSession()
    {
        CancelSession();
        _sessionCts = new CancellationTokenSource();
    }

    private void CancelSession()
    {
        var cts = Interlocked.Exchange(ref _sessionCts, null);
        if (cts is null)
        {
            return;
        }

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        cts.Dispose();
    }

    private CancellationToken SessionToken
        => _sessionCts?.Token ?? CancellationToken.None;

    private void ReleaseMediaBindings()
    {
        foreach (var item in Items)
        {
            item.AvatarImage = null;
            item.FirstImage = null;
        }

        Items.Clear();
        _offset = 0;
        _hasMore = false;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (_args is null)
        {
            return;
        }

        var ct = SessionToken;
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
            ct.ThrowIfCancellationRequested();

            var list = await FetchPageAsync(ct).ConfigureAwait(true);
            ct.ThrowIfCancellationRequested();
            foreach (var post in list)
            {
                Items.Add(new PostItemViewModel(post, _imageLoader));
            }

            StatusText = $"{Items.Count} 条";
            await RefreshSubscribeStateAsync(ct).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (SolarApiException ex)
        {
            if (ct.IsCancellationRequested)
            {
                return;
            }

            ErrorMessage = ex.Message;
            StatusText = "加载失败";
            _toast.Error(ex.Message);
        }
        finally
        {
            if (!ct.IsCancellationRequested)
            {
                IsBusy = false;
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(EmptyVisibility));
                OnPropertyChanged(nameof(LoadMoreVisibility));
            }
        }
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (_args is null || IsLoadingMore || IsBusy || !_hasMore)
        {
            return;
        }

        var ct = SessionToken;
        try
        {
            IsLoadingMore = true;
            var list = await FetchPageAsync(ct).ConfigureAwait(true);
            ct.ThrowIfCancellationRequested();
            foreach (var post in list)
            {
                Items.Add(new PostItemViewModel(post, _imageLoader));
            }

            TrimFeedWindow();
            StatusText = $"{Items.Count} 条";
        }
        catch (OperationCanceledException)
        {
        }
        catch (SolarApiException ex)
        {
            if (!ct.IsCancellationRequested)
            {
                _toast.Error(ex.Message);
            }
        }
        finally
        {
            if (!ct.IsCancellationRequested)
            {
                IsLoadingMore = false;
                OnPropertyChanged(nameof(LoadMoreVisibility));
            }
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

    /// <summary>Keep at most <see cref="MaxFeedItems"/> cards; drop from the top (older).</summary>
    private void TrimFeedWindow()
    {
        while (Items.Count > MaxFeedItems)
        {
            var old = Items[0];
            old.AvatarImage = null;
            old.FirstImage = null;
            Items.RemoveAt(0);
        }
    }

    [RelayCommand]
    private async Task ToggleSubscribeAsync()
    {
        if (_args is null || !CanSubscribe)
        {
            return;
        }

        var ct = SessionToken;
        try
        {
            if (IsSubscribed)
            {
                await UnsubscribeCurrentAsync(ct).ConfigureAwait(true);
                if (ct.IsCancellationRequested)
                {
                    return;
                }

                IsSubscribed = false;
                SubscribeButtonText = "订阅";
                _toast.Success("已取消订阅");
            }
            else
            {
                await SubscribeCurrentAsync(ct).ConfigureAwait(true);
                if (ct.IsCancellationRequested)
                {
                    return;
                }

                IsSubscribed = true;
                SubscribeButtonText = "已订阅";
                _toast.Success("已订阅");
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (SolarApiException ex)
        {
            if (!ct.IsCancellationRequested)
            {
                _toast.Error(ex.Message);
            }
        }
    }

    private async Task<List<SnPost>> FetchPageAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
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
                    list = await _api
                        .GetCollectionPostsAsync(args.PublisherName, args.Key, cancellationToken)
                        .ConfigureAwait(true);
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
                    cancellationToken,
                    tag: args.Key).ConfigureAwait(true);
                _hasMore = list.Count >= PageSize;
                _offset += list.Count;
                break;

            case PostFeedKind.Category:
                list = await _api.GetPostsAsync(
                    _offset,
                    PageSize,
                    cancellationToken,
                    category: args.Key).ConfigureAwait(true);
                _hasMore = list.Count >= PageSize;
                _offset += list.Count;
                break;

            case PostFeedKind.Publisher:
                list = await _api.GetPostsAsync(
                    _offset,
                    PageSize,
                    cancellationToken,
                    pub: args.Key).ConfigureAwait(true);
                _hasMore = list.Count >= PageSize;
                _offset += list.Count;
                break;

            default:
                list = [];
                _hasMore = false;
                break;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return list;
    }

    private async Task RefreshSubscribeStateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_args is null || !CanSubscribe)
        {
            IsSubscribed = false;
            SubscribeButtonText = "订阅";
            return;
        }

        // Best-effort: subscription GET is not always available for all kinds.
        IsSubscribed = false;
        SubscribeButtonText = "订阅";
        await Task.CompletedTask.ConfigureAwait(true);
    }

    private Task SubscribeCurrentAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var args = _args!;
        return args.Kind switch
        {
            PostFeedKind.Tag => _api.SubscribeTagAsync(args.Key, cancellationToken),
            PostFeedKind.Category => _api.SubscribeCategoryAsync(args.Key, cancellationToken),
            PostFeedKind.Collection => _api.SubscribeCollectionAsync(args.PublisherName!, args.Key, cancellationToken),
            PostFeedKind.Publisher => _api.SubscribePublisherAsync(args.Key, cancellationToken),
            _ => Task.CompletedTask,
        };
    }

    private Task UnsubscribeCurrentAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var args = _args!;
        return args.Kind switch
        {
            PostFeedKind.Tag => _api.UnsubscribeTagAsync(args.Key, cancellationToken),
            PostFeedKind.Category => _api.UnsubscribeCategoryAsync(args.Key, cancellationToken),
            PostFeedKind.Collection => _api.UnsubscribeCollectionAsync(args.PublisherName!, args.Key, cancellationToken),
            PostFeedKind.Publisher => _api.UnsubscribePublisherAsync(args.Key, cancellationToken),
            _ => Task.CompletedTask,
        };
    }
}
