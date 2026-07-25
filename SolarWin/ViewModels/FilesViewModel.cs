using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using SolarWin.Helpers;
using SolarWin.Models;
using SolarWin.Services;

namespace SolarWin.ViewModels;

public partial class FilesViewModel : ObservableObject, IDisposable
{
    /// <summary>Files at or above this size use chunked upload (create → chunks → complete).</summary>
    private const long ChunkedUploadThresholdBytes = 5 * 1024 * 1024;

    private readonly ISolarApiClient _api;
    private readonly IToastService _toast;
    private readonly FileThumbnailLoader _thumbnailLoader;
    private readonly Stack<(string? Id, string Name)> _navStack = new();
    private readonly Dictionary<FileItemViewModel, CancellationTokenSource> _thumbnailRequests = new();
    private CancellationTokenSource _folderThumbnailCts = new();
    private int _folderVersion;
    private bool _disposed;

    public FilesViewModel(ISolarApiClient api, IToastService toast, FileThumbnailLoader thumbnailLoader)
    {
        _api = api;
        _toast = toast;
        _thumbnailLoader = thumbnailLoader;
        _navStack.Push((null, "我的文件"));
        Breadcrumb = "我的文件";
        UpdateModeVisibility();
    }

    public RangeObservableCollection<FileItemViewModel> Files { get; } = [];

    /// <summary>Folders available as move targets in the current listing (excludes selection).</summary>
    public RangeObservableCollection<FileItemViewModel> FolderTargets { get; } = [];

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsUploading { get; set; }

    [ObservableProperty]
    public partial double UploadProgress { get; set; }

    [ObservableProperty]
    public partial string Breadcrumb { get; set; } = "我的文件";

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    [ObservableProperty]
    public partial FileItemViewModel? SelectedFile { get; set; }

    [ObservableProperty]
    public partial bool IsRecycleBinMode { get; set; }

    [ObservableProperty]
    public partial Visibility NormalModeVisibility { get; set; } = Visibility.Visible;

    [ObservableProperty]
    public partial Visibility RecycleBinModeVisibility { get; set; } = Visibility.Collapsed;

    public bool CanGoUp => !IsRecycleBinMode && _navStack.Count > 1;

    public string? CurrentParentId => IsRecycleBinMode ? null : _navStack.Peek().Id;

    partial void OnIsRecycleBinModeChanged(bool value)
    {
        UpdateModeVisibility();
        OnPropertyChanged(nameof(CanGoUp));
        OnPropertyChanged(nameof(CurrentParentId));
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        CancelAllThumbnailRequests(resetThumbnails: true);
        var loadVersion = _folderVersion;

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            SelectedFile = null;
            Files.ReplaceAll([]);
            FolderTargets.ReplaceAll([]);

            var parentId = CurrentParentId;
            var list = await _api
                .GetMyFilesAsync(parentId, offset: 0, take: 100, recycled: IsRecycleBinMode)
                .ConfigureAwait(true);

            if (loadVersion != _folderVersion)
            {
                return;
            }

            var items = list
                .OrderByDescending(f => f.IsFolder)
                .ThenByDescending(f => f.UpdatedAt ?? f.CreatedAt)
                .ThenBy(f => f.Name)
                .Select(file => new FileItemViewModel(file))
                .ToList();

            Files.ReplaceAll(items);
            RebuildFolderTargets();
            StatusMessage = IsRecycleBinMode
                ? $"回收站 · 共 {Files.Count} 项"
                : $"共 {Files.Count} 项";
            OnPropertyChanged(nameof(CanGoUp));
        }
        catch (SolarApiException ex)
        {
            if (loadVersion != _folderVersion)
            {
                return;
            }

            ErrorMessage = ex.Message;
            StatusMessage = "加载失败";
            _toast.Error("文件列表加载失败");
        }
        finally
        {
            if (loadVersion == _folderVersion)
            {
                IsBusy = false;
            }
        }
    }

    [RelayCommand]
    private async Task ToggleRecycleBinAsync()
    {
        IsRecycleBinMode = !IsRecycleBinMode;
        SelectedFile = null;

        if (IsRecycleBinMode)
        {
            Breadcrumb = "回收站";
        }
        else
        {
            RebuildBreadcrumb();
        }

        await LoadAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task OpenItemAsync(FileItemViewModel? item)
    {
        if (item is null || IsRecycleBinMode)
        {
            if (item is not null)
            {
                SelectedFile = item;
            }

            return;
        }

        if (item.IsFolder)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
            {
                ErrorMessage = "文件夹缺少 id，无法打开。";
                return;
            }

            _navStack.Push((item.Id, item.Name));
            RebuildBreadcrumb();
            await LoadAsync().ConfigureAwait(true);
            return;
        }

        SelectedFile = item;
    }

    [RelayCommand]
    private async Task GoUpAsync()
    {
        if (IsRecycleBinMode || _navStack.Count <= 1)
        {
            return;
        }

        _navStack.Pop();
        RebuildBreadcrumb();
        await LoadAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task CreateFolderAsync(string? name)
    {
        if (IsRecycleBinMode)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            ErrorMessage = "请输入文件夹名称。";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            await _api.CreateFolderAsync(name.Trim(), CurrentParentId).ConfigureAwait(true);
            StatusMessage = $"已创建文件夹「{name.Trim()}」";
            _toast.Success(StatusMessage);
            await LoadAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.Message;
            _toast.Error("创建文件夹失败");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task UploadAsync(Stream stream, string fileName, string contentType, long size)
    {
        if (IsRecycleBinMode)
        {
            ErrorMessage = "回收站中无法上传文件。";
            return;
        }

        try
        {
            IsUploading = true;
            UploadProgress = 0;
            ErrorMessage = null;

            var progress = new Progress<double>(p => UploadProgress = p);
            var useChunked = size >= ChunkedUploadThresholdBytes;
            StatusMessage = useChunked
                ? $"正在分块上传 {fileName}…"
                : $"正在上传 {fileName}…";

            if (useChunked)
            {
                await _api.UploadFileChunkedAsync(
                        stream,
                        fileName,
                        contentType,
                        size,
                        CurrentParentId,
                        progress,
                        CancellationToken.None)
                    .ConfigureAwait(true);
            }
            else
            {
                await _api.UploadFileDirectAsync(
                        stream,
                        fileName,
                        contentType,
                        size,
                        CurrentParentId,
                        progress,
                        CancellationToken.None)
                    .ConfigureAwait(true);
            }

            UploadProgress = 1;
            StatusMessage = useChunked
                ? $"已分块上传 {fileName}"
                : $"已上传 {fileName}";
            _toast.Success(StatusMessage);
            await LoadAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = "上传失败";
            _toast.Error("上传失败");
        }
        finally
        {
            IsUploading = false;
        }
    }

    [RelayCommand]
    private async Task RenameAsync(string? newName)
    {
        if (IsRecycleBinMode)
        {
            return;
        }

        var item = SelectedFile;
        if (item is null || string.IsNullOrWhiteSpace(item.Id))
        {
            ErrorMessage = "请先选择文件。";
            return;
        }

        if (string.IsNullOrWhiteSpace(newName))
        {
            ErrorMessage = "名称不能为空。";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            await _api.RenameFileAsync(item.Id, newName.Trim()).ConfigureAwait(true);
            StatusMessage = "已重命名";
            _toast.Success("已重命名");
            await LoadAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.Message;
            _toast.Error("重命名失败");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RecycleSelectedAsync()
    {
        if (IsRecycleBinMode)
        {
            return;
        }

        var item = SelectedFile;
        if (item is null || string.IsNullOrWhiteSpace(item.Id))
        {
            ErrorMessage = "请先选择文件。";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            await _api.RecycleFilesAsync([item.Id]).ConfigureAwait(true);
            StatusMessage = $"已移入回收站：{item.Name}";
            _toast.Success(StatusMessage);
            SelectedFile = null;
            await LoadAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.Message;
            _toast.Error("删除失败");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RestoreSelectedAsync()
    {
        if (!IsRecycleBinMode)
        {
            return;
        }

        var item = SelectedFile;
        if (item is null || string.IsNullOrWhiteSpace(item.Id))
        {
            ErrorMessage = "请先选择要恢复的文件。";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            await _api.RestoreFilesAsync([item.Id]).ConfigureAwait(true);
            StatusMessage = $"已恢复：{item.Name}";
            _toast.Success(StatusMessage);
            SelectedFile = null;
            await LoadAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.Message;
            _toast.Error("恢复失败");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeletePermanentlyAsync()
    {
        if (!IsRecycleBinMode)
        {
            return;
        }

        var item = SelectedFile;
        if (item is null || string.IsNullOrWhiteSpace(item.Id))
        {
            ErrorMessage = "请先选择要永久删除的文件。";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            await _api.DeleteFilesPermanentlyAsync([item.Id]).ConfigureAwait(true);
            StatusMessage = $"已永久删除：{item.Name}";
            _toast.Success(StatusMessage);
            SelectedFile = null;
            await LoadAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.Message;
            _toast.Error("永久删除失败");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Move selected item. <paramref name="targetParentId"/> null = root.
    /// Pass empty string to mean "stay / cancel" is not used — caller resolves target.
    /// </summary>
    public async Task MoveSelectedAsync(string? targetParentId)
    {
        if (IsRecycleBinMode)
        {
            ErrorMessage = "回收站中无法移动文件。";
            return;
        }

        var item = SelectedFile;
        if (item is null || string.IsNullOrWhiteSpace(item.Id))
        {
            ErrorMessage = "请先选择要移动的文件。";
            return;
        }

        // Prevent moving a folder into itself.
        if (item.IsFolder
            && !string.IsNullOrWhiteSpace(targetParentId)
            && string.Equals(item.Id, targetParentId, StringComparison.Ordinal))
        {
            ErrorMessage = "不能将文件夹移动到自身。";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            await _api.MoveFilesAsync([item.Id], targetParentId).ConfigureAwait(true);
            StatusMessage = string.IsNullOrWhiteSpace(targetParentId)
                ? $"已移动到根目录：{item.Name}"
                : $"已移动：{item.Name}";
            _toast.Success(StatusMessage);
            SelectedFile = null;
            await LoadAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.Message;
            _toast.Error("移动失败");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task DownloadAsync(Stream destination, FileItemViewModel item, IProgress<double>? progress)
    {
        if (string.IsNullOrWhiteSpace(item.DownloadUrl))
        {
            throw new SolarApiException("该文件没有可下载的 URL。");
        }

        await _api.DownloadToStreamAsync(item.DownloadUrl, destination, progress, CancellationToken.None)
            .ConfigureAwait(true);
    }

    public void UpdateVisibleThumbnailWindow(IReadOnlyCollection<FileItemViewModel> visibleItems, int prefetchCount)
    {
        if (_disposed)
        {
            return;
        }

        var indexes = visibleItems
            .Select(item => Files.IndexOf(item))
            .Where(index => index >= 0)
            .ToList();

        if (indexes.Count == 0)
        {
            foreach (var item in _thumbnailRequests.Keys.ToList())
            {
                CancelThumbnailForItem(item);
            }

            return;
        }

        var start = Math.Max(0, indexes.Min() - Math.Max(0, prefetchCount));
        var end = Math.Min(Files.Count - 1, indexes.Max() + Math.Max(0, prefetchCount));
        var window = Files
            .Skip(start)
            .Take(end - start + 1)
            .Where(item => item.CanLoadThumbnail)
            .ToHashSet();

        foreach (var item in _thumbnailRequests.Keys.ToList())
        {
            if (!window.Contains(item))
            {
                CancelThumbnailForItem(item);
            }
        }

        foreach (var item in window)
        {
            LoadThumbnailForVisibleItem(item);
        }
    }

    public void LoadThumbnailForVisibleItem(FileItemViewModel? item)
    {
        if (_disposed || item is null || !item.CanLoadThumbnail)
        {
            return;
        }

        if (item.Thumbnail is not null)
        {
            return;
        }

        if (_thumbnailLoader.TryGetCached(item.ThumbnailUrl, FileThumbnailLoader.DefaultDecodePixelWidth, out var cached)
            && cached is not null)
        {
            ApplyCachedThumbnailOnUiThread(item, cached);
            return;
        }

        if (_thumbnailRequests.ContainsKey(item))
        {
            return;
        }

        var requestVersion = _folderVersion;
        var requestParentId = CurrentParentId;
        var cts = CancellationTokenSource.CreateLinkedTokenSource(_folderThumbnailCts.Token);
        _thumbnailRequests[item] = cts;
        _ = LoadThumbnailAsync(item, requestVersion, requestParentId, cts);
    }

    public void CancelThumbnailForItem(FileItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        if (_thumbnailRequests.Remove(item, out var cts))
        {
            cts.Cancel();
        }
    }

    public void CancelAllThumbnailRequests(bool resetThumbnails = false)
    {
        if (_disposed)
        {
            return;
        }

        _folderVersion++;
        _folderThumbnailCts.Cancel();
        foreach (var cts in _thumbnailRequests.Values)
        {
            cts.Cancel();
        }

        _thumbnailRequests.Clear();
        _folderThumbnailCts.Dispose();
        _folderThumbnailCts = new CancellationTokenSource();

        if (resetThumbnails)
        {
            foreach (var item in Files)
            {
                item.Thumbnail = null;
            }
        }
    }

    private async Task LoadThumbnailAsync(
        FileItemViewModel item,
        int requestVersion,
        string? requestParentId,
        CancellationTokenSource requestCts)
    {
        try
        {
            var image = await _thumbnailLoader
                .LoadSafeAsync(item.ThumbnailUrl, FileThumbnailLoader.DefaultDecodePixelWidth, requestCts.Token)
                .ConfigureAwait(true);

            if (image is null
                || requestCts.IsCancellationRequested
                || requestVersion != _folderVersion
                || !string.Equals(requestParentId, CurrentParentId, StringComparison.Ordinal)
                || !Files.Contains(item))
            {
                return;
            }

            await ApplyThumbnailOnUiThreadAsync(item, image, requestVersion, requestParentId, requestCts)
                .ConfigureAwait(true);
        }
        finally
        {
            if (_thumbnailRequests.TryGetValue(item, out var current) && ReferenceEquals(current, requestCts))
            {
                _thumbnailRequests.Remove(item);
            }

            requestCts.Dispose();
        }
    }

    private void ApplyCachedThumbnailOnUiThread(FileItemViewModel item, BitmapImage image)
    {
        var dq = App.DispatcherQueue;
        if (dq is null || dq.HasThreadAccess)
        {
            if (!_disposed && Files.Contains(item))
            {
                item.Thumbnail = image;
            }

            return;
        }

        dq.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (!_disposed && Files.Contains(item))
            {
                item.Thumbnail = image;
            }
        });
    }

    private async Task ApplyThumbnailOnUiThreadAsync(
        FileItemViewModel item,
        BitmapImage image,
        int requestVersion,
        string? requestParentId,
        CancellationTokenSource requestCts)
    {
        var dq = App.DispatcherQueue;
        if (dq is null || dq.HasThreadAccess)
        {
            Apply();
            return;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!dq.TryEnqueue(DispatcherQueuePriority.Low, () =>
            {
                try
                {
                    Apply();
                    tcs.TrySetResult();
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }))
        {
            return;
        }

        await tcs.Task.ConfigureAwait(false);

        void Apply()
        {
            if (!_disposed
                && !requestCts.IsCancellationRequested
                && requestVersion == _folderVersion
                && string.Equals(requestParentId, CurrentParentId, StringComparison.Ordinal)
                && Files.Contains(item))
            {
                item.Thumbnail = image;
            }
        }
    }

    public int ThumbnailMaxConcurrency => _thumbnailLoader.MaxConcurrency;

    public int ThumbnailCacheMaxEntries => _thumbnailLoader.CacheMaxEntries;

    public long ThumbnailCacheMaxEstimatedBytes => _thumbnailLoader.CacheMaxEstimatedBytes;

    private void RebuildBreadcrumb()
    {
        if (IsRecycleBinMode)
        {
            Breadcrumb = "回收站";
            OnPropertyChanged(nameof(CanGoUp));
            return;
        }

        var parts = _navStack.Reverse().Select(x => x.Name);
        Breadcrumb = string.Join(" / ", parts);
        OnPropertyChanged(nameof(CanGoUp));
    }

    private void RebuildFolderTargets()
    {
        FolderTargets.ReplaceAll(Files.Where(x => x.IsFolder && !string.IsNullOrWhiteSpace(x.Id)));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        CancelAllThumbnailRequests(resetThumbnails: false);
        _disposed = true;
        _folderThumbnailCts.Dispose();
    }

    private void UpdateModeVisibility()
    {
        NormalModeVisibility = IsRecycleBinMode ? Visibility.Collapsed : Visibility.Visible;
        RecycleBinModeVisibility = IsRecycleBinMode ? Visibility.Visible : Visibility.Collapsed;
    }
}
