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
    private readonly DysonFileImageLoader _thumbnailLoader;
    private readonly VideoMediaCache _videoMediaCache;
    private readonly Stack<(string? Id, string Name)> _navStack = new();
    private readonly Dictionary<FileItemViewModel, CancellationTokenSource> _thumbnailRequests = new();
    private CancellationTokenSource _folderThumbnailCts = new();
    private int _folderVersion;
    private bool _disposed;

    public FilesViewModel(
        ISolarApiClient api,
        IToastService toast,
        DysonFileImageLoader thumbnailLoader,
        VideoMediaCache videoMediaCache)
    {
        _api = api;
        _toast = toast;
        _thumbnailLoader = thumbnailLoader;
        _videoMediaCache = videoMediaCache;
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

    /// <summary>
    /// Thumbnails paint via FastWin2DImage + ThumbnailUrl (GPU). Viewport cancel is owned by the control.
    /// Kept for FilesPage call sites.
    /// </summary>
    public void UpdateVisibleThumbnailWindow(IReadOnlyCollection<FileItemViewModel> visibleItems, int prefetchCount)
    {
        _ = prefetchCount;
        var wanted = visibleItems.Where(static item => item.IsVideo).ToHashSet();
        foreach (var item in _thumbnailRequests.Keys.Where(item => !wanted.Contains(item)).ToList())
        {
            CancelThumbnailForItem(item);
        }

        foreach (var item in wanted)
        {
            LoadThumbnailForVisibleItem(item);
        }
    }

    public void LoadThumbnailForVisibleItem(FileItemViewModel? item)
    {
        if (item is null || !item.IsVideo || string.IsNullOrWhiteSpace(item.VideoSource)
            || !string.IsNullOrWhiteSpace(item.ThumbnailUrl) || _thumbnailRequests.ContainsKey(item))
        {
            return;
        }

        // Avoid downloading very large videos merely because their cards scrolled into view.
        if (item.File.Size > 128L * 1024 * 1024)
        {
            return;
        }

        var cts = CancellationTokenSource.CreateLinkedTokenSource(_folderThumbnailCts.Token);
        _thumbnailRequests[item] = cts;
        _ = LoadVideoThumbnailAsync(item, cts);
    }

    public void CancelThumbnailForItem(FileItemViewModel? item)
    {
        if (item is not null && _thumbnailRequests.Remove(item, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
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
        _folderThumbnailCts.Dispose();
        _folderThumbnailCts = new CancellationTokenSource();
        foreach (var cts in _thumbnailRequests.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }
        _thumbnailRequests.Clear();
        if (resetThumbnails)
        {
            foreach (var item in Files.Where(static item => item.IsVideo)) item.ThumbnailUrl = null;
        }
    }

    public int ThumbnailMaxConcurrency => _thumbnailLoader.MaxConcurrency;

    public int ThumbnailCacheMaxEntries => _thumbnailLoader.CacheMaxEntries;

    public long ThumbnailCacheMaxEstimatedBytes => _thumbnailLoader.CacheMaxEstimatedBytes;

    private async Task LoadVideoThumbnailAsync(FileItemViewModel item, CancellationTokenSource request)
    {
        try
        {
            var path = await _videoMediaCache.GetThumbnailAsync(
                item.VideoSource!, item.Name, item.MimeType, 256, 144, request.Token).ConfigureAwait(true);
            if (!request.IsCancellationRequested && !string.IsNullOrWhiteSpace(path))
            {
                item.ThumbnailUrl = path;
            }
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested)
        {
            // Viewport/folder changed.
        }
        finally
        {
            if (_thumbnailRequests.TryGetValue(item, out var current) && ReferenceEquals(current, request))
            {
                _thumbnailRequests.Remove(item);
                request.Dispose();
            }
        }
    }

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
