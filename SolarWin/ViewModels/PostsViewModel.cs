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
    /// <summary>Fetch at least 40 posts per page (user request).</summary>
    private const int PageSize = 48;

    /// <summary>In-memory feed window — drop oldest when LoadMore grows past this (memory review #14).</summary>
    private const int MaxFeedItems = 120;

    private readonly ISolarApiClient _api;
    private readonly IToastService _toast;
    private readonly DysonFileImageLoader _imageLoader;
    private readonly IAuthService _auth;
    private readonly Dictionary<PostItemViewModel, CancellationTokenSource> _imageRequests = [];
    private HashSet<PostItemViewModel> _imageWindow = [];
    private int _imageWindowVersion;

    private int _offset;
    private bool _usingTimeline = true;
    private string? _publisherName;
    private bool _publisherResolved;
    private bool _allowFeedModeReload;

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPost))]
    [NotifyPropertyChangedFor(nameof(IsComposerEnabled))]
    public partial bool IsUploadingImage { get; set; }

    [ObservableProperty]
    public partial double UploadProgress { get; set; }

    /// <summary>Pending cloud file ids for the compose box (Drive upload → post attachments).</summary>
    public ObservableCollection<PendingPostAttachment> PendingAttachments { get; } = [];

    /// <summary>0 = timeline (home), 1 = public posts.</summary>
    [ObservableProperty]
    public partial int FeedModeIndex { get; set; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsEmpty => !IsBusy && !HasError && Items.Count == 0;

    public bool ShowContent => !IsBusy && !HasError && Items.Count > 0;

    public bool CanPost =>
        !IsPosting
        && !IsUploadingImage
        && (!string.IsNullOrWhiteSpace(NewPostContent) || PendingAttachments.Count > 0);

    public bool IsComposerEnabled => !IsPosting && !IsUploadingImage;

    public Microsoft.UI.Xaml.Visibility PendingAttachmentsVisibility =>
        PendingAttachments.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Microsoft.UI.Xaml.Visibility UploadProgressVisibility =>
        IsUploadingImage ? Visibility.Visible : Visibility.Collapsed;

    partial void OnFeedModeIndexChanged(int value)
    {
        if (!_allowFeedModeReload || IsBusy)
        {
            return;
        }

        if (LoadCommand.CanExecute(null))
        {
            LoadCommand.Execute(null);
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            ClearImageWindow(clearImages: true);
            Items.Clear();
            _offset = 0;
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(ShowContent));

            var list = await FetchPageAsync(reset: true).ConfigureAwait(true);
            AppendPostsSafe(list);

            if (Items.Count == 0)
            {
                // Last resort: force public feed then featured
                try
                {
                    var publicList = await _api.GetPostsAsync(0, PageSize).ConfigureAwait(true);
                    AppendPostsSafe(publicList);
                    _usingTimeline = false;
                    _offset = publicList.Count;
                    HasMore = publicList.Count >= PageSize;
                }
                catch (SolarApiException)
                {
                    // try featured
                }

                if (Items.Count == 0)
                {
                    try
                    {
                        var featured = await _api.GetFeaturedPostsAsync().ConfigureAwait(true);
                        AppendPostsSafe(featured);
                        _usingTimeline = false;
                        _offset = featured.Count;
                        HasMore = false;
                    }
                    catch (SolarApiException)
                    {
                        // leave empty
                    }
                }
            }

            StatusText = _usingTimeline
                ? $"时间线 · {Items.Count} 条"
                : $"公共 · {Items.Count} 条";
            if (Items.Count == 0)
            {
                StatusText = "暂无帖子（接口无数据或解析失败）";
            }
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.ApiMessage ?? ex.Message;
            StatusText = "加载失败";
            _toast.Error("帖子加载失败：" + ErrorMessage);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusText = "加载失败";
            _toast.Error("帖子加载失败");
        }
        finally
        {
            IsBusy = false;
            _allowFeedModeReload = true;
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
            var list = await FetchPageAsync(reset: false).ConfigureAwait(true);
            AppendPostsSafe(list);

            StatusText = _usingTimeline
                ? $"时间线 · {Items.Count} 条"
                : $"公共 · {Items.Count} 条";        }
        catch (SolarApiException ex)
        {
            _toast.Error($"加载更多失败:{ex.Message}");
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    private void AppendPostsSafe(IEnumerable<SnPost> posts)
    {
        foreach (var post in posts)
        {
            if (post.Id == Guid.Empty)
            {
                continue;
            }

            // Avoid duplicates when mixing timeline + public fallbacks
            if (Items.Any(i => i.Post.Id == post.Id))
            {
                continue;
            }

            try
            {
                Items.Add(new PostItemViewModel(post, _imageLoader, bindCachedImages: true));
            }
            catch
            {
                // One bad card must not wipe the whole feed.
            }
        }

        TrimFeedWindow();
    }

    /// <summary>Keep at most <see cref="MaxFeedItems"/> cards; drop from the top (older).</summary>
    private void TrimFeedWindow()
    {
        while (Items.Count > MaxFeedItems)
        {
            var old = Items[0];
            CancelImageRequest(old);
            _imageWindow.Remove(old);
            old.FirstImage = null;
            old.AvatarImage = null;
            Items.RemoveAt(0);
        }
    }

    /// <summary>Upload a local image to Drive and queue it as a post attachment.</summary>
    public async Task AttachLocalImageAsync(Stream stream, string fileName, string contentType, long size)
    {
        if (IsPosting || IsUploadingImage)
        {
            return;
        }

        try
        {
            IsUploadingImage = true;
            UploadProgress = 0;
            ErrorMessage = null;

            var progress = new Progress<double>(p => UploadProgress = p);
            var file = await _api.UploadFileDirectAsync(
                stream,
                fileName,
                contentType,
                size,
                parentId: null,
                progress,
                CancellationToken.None).ConfigureAwait(true);

            var id = file.Id ?? CloudFileUrlHelper.ResolveFileId(file);
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new SolarApiException("上传成功但未返回文件 id。");
            }

            // Local preview from cloud id (authenticated)
            BitmapImage? preview = null;
            try
            {
                preview = await _imageLoader.LoadSafeAsync(id, DysonFileImageLoader.FeedImageDecodeWidth).ConfigureAwait(true);
            }
            catch
            {
                // preview optional
            }

            PendingAttachments.Add(new PendingPostAttachment
            {
                FileId = id,
                FileName = file.Name ?? fileName,
                Preview = preview,
            });
            UploadProgress = 1;
            NotifyComposer();
            _toast.Success("图片已添加");
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.Message;
            _toast.Error("图片上传失败：" + (ex.ApiMessage ?? ex.Message));
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            _toast.Error("图片上传失败");
        }
        finally
        {
            IsUploadingImage = false;
            NotifyComposer();
        }
    }

    [RelayCommand]
    private void RemovePendingAttachment(PendingPostAttachment? item)
    {
        if (item is null)
        {
            return;
        }

        PendingAttachments.Remove(item);
        NotifyComposer();
    }

    [RelayCommand]
    private void ClearPendingAttachments()
    {
        PendingAttachments.Clear();
        NotifyComposer();
    }

    private void NotifyComposer()
    {
        OnPropertyChanged(nameof(CanPost));
        OnPropertyChanged(nameof(IsComposerEnabled));
        OnPropertyChanged(nameof(PendingAttachmentsVisibility));
        OnPropertyChanged(nameof(UploadProgressVisibility));
        CreatePostCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task CreatePostAsync()
    {
        var content = NewPostContent?.Trim() ?? string.Empty;
        var attachmentIds = PendingAttachments
            .Select(a => a.FileId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if ((content.Length == 0 && attachmentIds.Count == 0) || IsPosting || IsUploadingImage)
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
                // Server rejects empty content only when attachments are also empty.
                Content = content.Length > 0 ? content : (attachmentIds.Count > 0 ? null : content),
                Visibility = 0,
                Type = 0,
                Attachments = attachmentIds.Count > 0 ? attachmentIds : null,
            };

            // Some gateways still require non-empty content even with attachments.
            if (string.IsNullOrWhiteSpace(request.Content) && attachmentIds.Count > 0)
            {
                request.Content = " ";
            }

            var created = await _api.CreatePostAsync(request, pub).ConfigureAwait(true);
            try
            {
                Items.Insert(0, new PostItemViewModel(created, _imageLoader, bindCachedImages: false));
            }
            catch
            {
                // still clear composer; refresh will show the post
            }

            NewPostContent = string.Empty;
            PendingAttachments.Clear();
            NotifyComposer();
            StatusText = _usingTimeline
                ? $"时间线 · {Items.Count} 条"
                : $"公共 · {Items.Count} 条";            _toast.Success(attachmentIds.Count > 0 ? "已发布（含图片）" : "已发布");
        }
        catch (SolarApiException ex)
        {
            // Retry with explicit caption if content-required.
            if (IsContentRequiredError(ex) && attachmentIds.Count > 0)
            {
                try
                {
                    var pub = await ResolvePublisherNameAsync().ConfigureAwait(true);
                    var created = await _api.CreatePostAsync(new CreatePostRequest
                    {
                        Content = string.IsNullOrWhiteSpace(content) ? "分享图片" : content,
                        Visibility = 0,
                        Type = 0,
                        Attachments = attachmentIds,
                    }, pub).ConfigureAwait(true);

                    Items.Insert(0, new PostItemViewModel(created, _imageLoader, bindCachedImages: false));
                    NewPostContent = string.Empty;
                    PendingAttachments.Clear();
                    NotifyComposer();
                    _toast.Success("已发布（含图片）");
                    return;
                }
                catch (SolarApiException retryEx)
                {
                    ErrorMessage = retryEx.Message;
                    _toast.Error("发布失败：" + (retryEx.ApiMessage ?? retryEx.Message));
                    return;
                }
            }

            ErrorMessage = ex.Message;
            _toast.Error("发布失败：" + (ex.ApiMessage ?? ex.Message));
        }
        finally
        {
            IsPosting = false;
            NotifyComposer();
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(ShowContent));
            OnPropertyChanged(nameof(HasError));
        }
    }

    private static bool IsContentRequiredError(SolarApiException ex)
    {
        var body = ex.ResponseBody ?? string.Empty;
        var msg = ex.ApiMessage ?? ex.Message ?? string.Empty;
        return body.Contains("POST_CONTENT_REQUIRED", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("Content is required", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<List<SnPost>> FetchPageAsync(bool reset)
    {
        if (reset)
        {
            _offset = 0;
        }

        // Mode 1: public posts only.
        if (FeedModeIndex == 1)
        {
            _usingTimeline = false;
            var publicList = await _api.GetPostsAsync(_offset, PageSize).ConfigureAwait(true);
            _offset += publicList.Count;
            HasMore = publicList.Count >= PageSize;
            return publicList;
        }

        // Mode 0: home feed — GetHomeTimelineAsync tries timeline/home → timeline events → public posts.
        try
        {
            var list = await _api.GetHomeTimelineAsync(_offset, PageSize).ConfigureAwait(true);
            _usingTimeline = true;
            _offset += list.Count;
            // If we got a full page, assume more; timeline event feed may not paginate cleanly.
            HasMore = list.Count >= Math.Min(PageSize, 20);
            if (list.Count == 0 && reset)
            {
                return await LoadPublicFallbackAsync().ConfigureAwait(true);
            }

            return list;
        }
        catch (Exception) when (reset)
        {
            return await LoadPublicFallbackAsync().ConfigureAwait(true);
        }
    }

    private async Task<List<SnPost>> LoadPublicFallbackAsync()
    {
        _usingTimeline = false;
        _offset = 0;
        var publicList = await _api.GetPostsAsync(0, PageSize).ConfigureAwait(true);
        _offset = publicList.Count;
        HasMore = publicList.Count >= PageSize;
        return publicList;
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

    public void UpdateVisibleImageWindow(
        IReadOnlyCollection<PostItemViewModel> visibleItems,
        int prefetchCount)
    {
        var indexes = visibleItems
            .Select(item => Items.IndexOf(item))
            .Where(index => index >= 0)
            .ToList();

        if (indexes.Count == 0)
        {
            // Keep already-decoded bitmaps while briefly empty (virtualization churn).
            // Full clear only happens on page leave / reload.
            return;
        }

        var start = Math.Max(0, indexes.Min() - Math.Max(0, prefetchCount));
        var end = Math.Min(Items.Count - 1, indexes.Max() + Math.Max(0, prefetchCount));
        var window = Items
            .Skip(start)
            .Take(end - start + 1)
            .ToHashSet();

        var changed = !_imageWindow.SetEquals(window);
        if (changed)
        {
            foreach (var item in _imageRequests.Keys.ToList())
            {
                if (!window.Contains(item))
                {
                    CancelImageRequest(item);
                }
            }

            // Do not null AvatarImage / FirstImage when scrolling away — re-fetch was the
            // main reason feed images felt slow. Memory is bounded by MaxFeedItems + LRU.
            _imageWindow = window;
        }

        var version = _imageWindowVersion;
        foreach (var item in window)
        {
            LoadImagesForVisibleItem(item, version);
        }
    }

    public void ClearVisibleImageWindow()
        => ClearImageWindow(clearImages: true);

    private void LoadImagesForVisibleItem(PostItemViewModel item, int version)
    {
        if (_imageRequests.ContainsKey(item))
        {
            return;
        }

        if (!NeedsImageLoad(item))
        {
            return;
        }

        var cts = new CancellationTokenSource();
        _imageRequests[item] = cts;
        _ = LoadItemImagesAsync(item, version, cts);
    }

    private async Task LoadItemImagesAsync(
        PostItemViewModel item,
        int version,
        CancellationTokenSource requestCts)
    {
        try
        {
            Task<BitmapImage?>? avatarTask = null;
            Task<BitmapImage?>? imageTask = null;

            if (item.HasAvatar && item.AvatarImage is null && !string.IsNullOrWhiteSpace(item.AvatarUrl))
            {
                if (_imageLoader.TryGetCached(
                        item.AvatarUrl,
                        out var cachedAvatar,
                        DysonFileImageLoader.AvatarDecodeWidth)
                    && cachedAvatar is not null)
                {
                    if (CanApplyImage(item, version, requestCts))
                    {
                        item.AvatarImage = cachedAvatar;
                    }
                }
                else
                {
                    avatarTask = _imageLoader.LoadSafeAsync(
                        item.AvatarUrl,
                        DysonFileImageLoader.AvatarDecodeWidth,
                        requestCts.Token);
                }
            }

            if (item.HasImages && item.FirstImage is null && item.ImageUrls.Count > 0)
            {
                var url = item.ImageUrls[0];
                if (_imageLoader.TryGetCached(
                        url,
                        out var cachedImage,
                        DysonFileImageLoader.FeedImageDecodeWidth)
                    && cachedImage is not null)
                {
                    if (CanApplyImage(item, version, requestCts))
                    {
                        item.FirstImage = cachedImage;
                    }
                }
                else
                {
                    imageTask = _imageLoader.LoadSafeAsync(
                        url,
                        DysonFileImageLoader.FeedImageDecodeWidth,
                        requestCts.Token);
                }
            }

            // Avatar + post image in parallel (was sequential → ~2× wait per card).
            if (avatarTask is not null && imageTask is not null)
            {
                await Task.WhenAll(avatarTask, imageTask).ConfigureAwait(true);
            }
            else if (avatarTask is not null)
            {
                await avatarTask.ConfigureAwait(true);
            }
            else if (imageTask is not null)
            {
                await imageTask.ConfigureAwait(true);
            }

            if (avatarTask is not null
                && avatarTask.IsCompletedSuccessfully
                && avatarTask.Result is { } avatar
                && CanApplyImage(item, version, requestCts))
            {
                item.AvatarImage = avatar;
            }

            if (imageTask is not null
                && imageTask.IsCompletedSuccessfully
                && imageTask.Result is { } image
                && CanApplyImage(item, version, requestCts))
            {
                item.FirstImage = image;
            }
        }
        finally
        {
            var shouldRetry = false;
            if (_imageRequests.TryGetValue(item, out var current) && ReferenceEquals(current, requestCts))
            {
                _imageRequests.Remove(item);
                shouldRetry = requestCts.IsCancellationRequested
                              && _imageWindow.Contains(item)
                              && Items.Contains(item);
            }

            requestCts.Dispose();
            if (shouldRetry)
            {
                LoadImagesForVisibleItem(item, _imageWindowVersion);
            }
        }
    }

    private bool CanApplyImage(
        PostItemViewModel item,
        int version,
        CancellationTokenSource requestCts)
        => !requestCts.IsCancellationRequested
           && version == _imageWindowVersion
           && _imageWindow.Contains(item)
           && Items.Contains(item);

    private static bool NeedsImageLoad(PostItemViewModel item)
        => item.HasAvatar && item.AvatarImage is null && !string.IsNullOrWhiteSpace(item.AvatarUrl)
           || item.HasImages && item.FirstImage is null;

    private void CancelImageRequest(PostItemViewModel item)
    {
        if (_imageRequests.TryGetValue(item, out var cts))
        {
            cts.Cancel();
        }
    }

    private void ClearImageWindow(bool clearImages)
    {
        _imageWindowVersion++;
        foreach (var cts in _imageRequests.Values)
        {
            cts.Cancel();
        }

        _imageRequests.Clear();
        _imageWindow = [];

        if (!clearImages)
        {
            return;
        }

        foreach (var item in Items)
        {
            item.AvatarImage = null;
            item.FirstImage = null;
        }
    }
}

/// <summary>Local compose attachment after Drive upload.</summary>
public sealed class PendingPostAttachment
{
    public required string FileId { get; init; }

    public required string FileName { get; init; }

    public BitmapImage? Preview { get; init; }
}
