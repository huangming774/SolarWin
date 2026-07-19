using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SolarWin.ViewModels;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using WinRT.Interop;

namespace SolarWin.Views;

public sealed partial class FilesPage : Page
{
    public FilesViewModel ViewModel { get; }

    public FilesPage()
    {
        ViewModel = App.Services.GetRequiredService<FilesViewModel>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (ViewModel.LoadCommand.CanExecute(null))
        {
            ViewModel.LoadCommand.Execute(null);
        }
    }

    private void Refresh_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.LoadCommand.CanExecute(null))
        {
            ViewModel.LoadCommand.Execute(null);
        }
    }

    private void GoUp_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.GoUpCommand.CanExecute(null))
        {
            ViewModel.GoUpCommand.Execute(null);
        }
    }

    private async void CreateFolder_OnClick(object sender, RoutedEventArgs e)
    {
        var box = new TextBox { Header = "文件夹名称", PlaceholderText = "新建文件夹" };
        var dialog = new ContentDialog
        {
            Title = "创建文件夹",
            Content = box,
            PrimaryButtonText = "创建",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        if (ViewModel.CreateFolderCommand.CanExecute(box.Text))
        {
            await ViewModel.CreateFolderCommand.ExecuteAsync(box.Text);
        }
    }

    private async void Upload_OnClick(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        InitializePicker(picker);
        picker.ViewMode = PickerViewMode.List;
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add("*");

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        try
        {
            var props = await file.GetBasicPropertiesAsync();
            await using var stream = await file.OpenStreamForReadAsync();
            var contentType = file.ContentType;
            if (string.IsNullOrWhiteSpace(contentType))
            {
                contentType = "application/octet-stream";
            }

            await ViewModel.UploadAsync(stream, file.Name, contentType, (long)props.Size);
        }
        catch (Exception ex)
        {
            ViewModel.ErrorMessage = ex.Message;
        }
    }

    private async void Rename_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedFile is null)
        {
            ViewModel.ErrorMessage = "请先选择文件或文件夹。";
            return;
        }

        var box = new TextBox
        {
            Header = "新名称",
            Text = ViewModel.SelectedFile.Name,
        };
        var dialog = new ContentDialog
        {
            Title = "重命名",
            Content = box,
            PrimaryButtonText = "保存",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        if (ViewModel.RenameCommand.CanExecute(box.Text))
        {
            await ViewModel.RenameCommand.ExecuteAsync(box.Text);
        }
    }

    private async void Recycle_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedFile is null)
        {
            ViewModel.ErrorMessage = "请先选择要删除的文件。";
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "移入回收站",
            Content = $"确定将「{ViewModel.SelectedFile.Name}」移入回收站？",
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        if (ViewModel.RecycleSelectedCommand.CanExecute(null))
        {
            await ViewModel.RecycleSelectedCommand.ExecuteAsync(null);
        }
    }

    private async void Download_OnClick(object sender, RoutedEventArgs e)
    {
        var item = ViewModel.SelectedFile;
        if (item is null || item.IsFolder)
        {
            ViewModel.ErrorMessage = "请选择可下载的文件。";
            return;
        }

        if (string.IsNullOrWhiteSpace(item.DownloadUrl))
        {
            ViewModel.ErrorMessage = "该文件没有下载地址。";
            return;
        }

        var picker = new FileSavePicker();
        InitializePicker(picker);
        picker.SuggestedStartLocation = PickerLocationId.Downloads;
        picker.SuggestedFileName = item.Name;
        var ext = Path.GetExtension(item.Name);
        if (string.IsNullOrWhiteSpace(ext))
        {
            ext = ".bin";
        }

        picker.FileTypeChoices.Add("文件", new List<string> { ext });

        var target = await picker.PickSaveFileAsync();
        if (target is null)
        {
            return;
        }

        try
        {
            ViewModel.IsBusy = true;
            ViewModel.StatusMessage = $"正在下载 {item.Name}…";
            ViewModel.UploadProgress = 0;

            var progress = new Progress<double>(p => ViewModel.UploadProgress = p);
            await using var stream = await target.OpenStreamForWriteAsync();
            // Truncate existing file.
            stream.SetLength(0);
            await ViewModel.DownloadAsync(stream, item, progress);
            ViewModel.StatusMessage = $"已保存：{target.Path}";
            ViewModel.UploadProgress = 1;
        }
        catch (Exception ex)
        {
            ViewModel.ErrorMessage = ex.Message;
            ViewModel.StatusMessage = "下载失败";
        }
        finally
        {
            ViewModel.IsBusy = false;
        }
    }

    private void FileGrid_OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is FileItemViewModel item)
        {
            ViewModel.SelectedFile = item;
            if (item.IsFolder && ViewModel.OpenItemCommand.CanExecute(item))
            {
                ViewModel.OpenItemCommand.Execute(item);
            }
        }
    }

    private void FileGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FileGrid.SelectedItem is FileItemViewModel item)
        {
            ViewModel.SelectedFile = item;
        }
    }

    private static void InitializePicker(object picker)
    {
        var hwnd = WindowNative.GetWindowHandle(App.Window);
        InitializeWithWindow.Initialize(picker, hwnd);
    }
}
