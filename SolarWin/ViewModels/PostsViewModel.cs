using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
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
    public const int MaxPendingAttachments = 9;
    private const long MaxPostImageBytes = 25L * 1024 * 1024;
    private const long MaxPostVideoBytes = 512L * 1024 * 1024;

    private readonly ISolarApiClient _api;
    private readonly IToastService _toast;
    private readonly DysonFileImageLoader _imageLoader;
    private readonly IAuthService _auth;

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
    [NotifyPropertyChangedFor(nameof(CanAddAttachments))]
    public partial bool IsPosting { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPost))]
    [NotifyPropertyChangedFor(nameof(IsComposerEnabled))]
    [NotifyPropertyChangedFor(nameof(CanAddAttachments))]
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

    public bool CanAddAttachments => IsComposerEnabled && PendingAttachments.Count < MaxPendingAttachments;

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
                Items.Add(new PostItemViewModel(post, _imageLoader));
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
            Items.RemoveAt(0);
        }
    }

    /// <summary>Upload a local image to Drive and queue it as a post attachment.</summary>
    public async Task AttachLocalImageAsync(Stream stream, string fileName, string contentType, long size)
        => await AttachLocalMediaAsync(stream, fileName, contentType, size, isVideo: false).ConfigureAwait(true);

    public async Task AttachLocalVideoAsync(Stream stream, string fileName, string contentType, long size)
        => await AttachLocalMediaAsync(stream, fileName, contentType, size, isVideo: true).ConfigureAwait(true);

    private async Task AttachLocalMediaAsync(
        Stream stream,
        string fileName,
        string contentType,
        long size,
        bool isVideo)
    {
        if (PendingAttachments.Count >= MaxPendingAttachments)
        {
            _toast.Warning($"每条帖子最多添加 {MaxPendingAttachments} 个附件");
            return;
        }

        var maxBytes = isVideo ? MaxPostVideoBytes : MaxPostImageBytes;
        if (size > maxBytes)
        {
            _toast.Warning(isVideo ? "单个视频不能超过 512 MB" : "单张图片不能超过 25 MB");
            return;
        }

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
            const long chunkedUploadThreshold = 5L * 1024 * 1024;
            var file = size >= chunkedUploadThreshold
                ? await _api.UploadFileChunkedAsync(
                    stream, fileName, contentType, size, parentId: null, progress, CancellationToken.None).ConfigureAwait(true)
                : await _api.UploadFileDirectAsync(
                    stream, fileName, contentType, size, parentId: null, progress, CancellationToken.None).ConfigureAwait(true);

            var id = file.Id ?? CloudFileUrlHelper.ResolveFileId(file);
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new SolarApiException("上传成功但未返回文件 id。");
            }

            PendingAttachments.Add(new PendingPostAttachment
            {
                FileId = id,
                FileName = file.Name ?? fileName,
                MimeType = file.MimeType ?? contentType,
                IsVideo = isVideo,
            });
            UploadProgress = 1;
            NotifyComposer();
            _toast.Success(isVideo ? "视频已添加" : "图片已添加");
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.Message;
            _toast.Error((isVideo ? "视频" : "图片") + "上传失败：" + (ex.ApiMessage ?? ex.Message));
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            _toast.Error((isVideo ? "视频" : "图片") + "上传失败");
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
        OnPropertyChanged(nameof(CanAddAttachments));
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

}

/// <summary>Local compose attachment after Drive upload.</summary>
public sealed class PendingPostAttachment
{
    public required string FileId { get; init; }

    public required string FileName { get; init; }

    public string MimeType { get; init; } = string.Empty;

    public bool IsVideo { get; init; }

    public Visibility ImageVisibility => IsVideo ? Visibility.Collapsed : Visibility.Visible;

    public Visibility VideoVisibility => IsVideo ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>FastWin2DImage resolves this id and releases its GPU lease offscreen.</summary>
    public string PreviewSource => FileId;
}
