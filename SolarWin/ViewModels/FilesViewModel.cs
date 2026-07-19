using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SolarWin.Models;
using SolarWin.Services;

namespace SolarWin.ViewModels;

public partial class FilesViewModel : ObservableObject
{
    private readonly ISolarApiClient _api;
    private readonly IToastService _toast;
    private readonly Stack<(string? Id, string Name)> _navStack = new();

    public FilesViewModel(ISolarApiClient api, IToastService toast)
    {
        _api = api;
        _toast = toast;
        _navStack.Push((null, "我的文件"));
        Breadcrumb = "我的文件";
    }

    public ObservableCollection<FileItemViewModel> Files { get; } = [];

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

    public bool CanGoUp => _navStack.Count > 1;

    public string? CurrentParentId => _navStack.Peek().Id;

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            Files.Clear();

            var parentId = CurrentParentId;
            var list = await _api.GetMyFilesAsync(parentId, offset: 0, take: 100).ConfigureAwait(true);

            foreach (var file in list
                         .OrderByDescending(f => f.IsFolder)
                         .ThenByDescending(f => f.UpdatedAt ?? f.CreatedAt)
                         .ThenBy(f => f.Name))
            {
                Files.Add(new FileItemViewModel(file));
            }

            StatusMessage = $"共 {Files.Count} 项";
            OnPropertyChanged(nameof(CanGoUp));
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = "加载失败";
            _toast.Error("文件列表加载失败");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenItemAsync(FileItemViewModel? item)
    {
        if (item is null)
        {
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
        if (_navStack.Count <= 1)
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
        try
        {
            IsUploading = true;
            UploadProgress = 0;
            ErrorMessage = null;
            StatusMessage = $"正在上传 {fileName}…";

            var progress = new Progress<double>(p =>
            {
                UploadProgress = p;
            });

            await _api.UploadFileDirectAsync(
                    stream,
                    fileName,
                    contentType,
                    size,
                    CurrentParentId,
                    progress,
                    CancellationToken.None)
                .ConfigureAwait(true);

            UploadProgress = 1;
            StatusMessage = $"已上传 {fileName}";
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

    public async Task DownloadAsync(Stream destination, FileItemViewModel item, IProgress<double>? progress)
    {
        if (string.IsNullOrWhiteSpace(item.DownloadUrl))
        {
            throw new SolarApiException("该文件没有可下载的 URL。");
        }

        await _api.DownloadToStreamAsync(item.DownloadUrl, destination, progress, CancellationToken.None)
            .ConfigureAwait(true);
    }

    private void RebuildBreadcrumb()
    {
        var parts = _navStack.Reverse().Select(x => x.Name);
        Breadcrumb = string.Join(" / ", parts);
        OnPropertyChanged(nameof(CanGoUp));
    }
}
