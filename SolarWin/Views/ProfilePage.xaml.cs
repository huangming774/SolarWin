using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using SolarWin.Controls;
using SolarWin.Helpers;
using SolarWin.ViewModels;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SolarWin.Views;

public sealed partial class ProfilePage : Page
{
    public ProfileViewModel ViewModel { get; }

    public ProfilePage()
    {
        ViewModel = App.Services.GetRequiredService<ProfileViewModel>();
        InitializeComponent();
        ViewModel.LoggedOut += OnLoggedOut;
        ViewModel.EditProfileRequested += OnEditProfileRequested;
        Unloaded += OnUnloaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (ViewModel.LoadCommand.CanExecute(null))
        {
            ViewModel.LoadCommand.Execute(null);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.LoggedOut -= OnLoggedOut;
        ViewModel.EditProfileRequested -= OnEditProfileRequested;
        Unloaded -= OnUnloaded;
    }

    private void EditProfile_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.RequestEditProfileCommand.Execute(null);
    }

    private async void ChangeAvatar_OnClick(object sender, RoutedEventArgs e)
    {
        var picker = CreateImagePicker();
        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        try
        {
            var props = await file.GetBasicPropertiesAsync();
            await using var stream = await file.OpenStreamForReadAsync();
            var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "image/jpeg" : file.ContentType;
            await ViewModel.ChangeAvatarAndSaveAsync(stream, file.Name, contentType, (long)props.Size);
        }
        catch (Exception ex)
        {
            ViewModel.ErrorMessage = ex.Message;
        }
    }

    private async void OnEditProfileRequested(object? sender, EventArgs e)
    {
        var avatarPreview = new FastWin2DImage
        {
            Width = 72,
            Height = 72,
            CornerRadius = new CornerRadius(36),
            DecodeWidth = DysonFileImageLoader.ProfileDecodeWidth,
            Stretch = Stretch.UniformToFill,
            ShowPlaceholder = true,
            EnableFadeIn = true,
            Source = ViewModel.EditAvatarUrl,
        };

        var changeAvatarBtn = new Button { Content = "选择新头像" };
        changeAvatarBtn.Click += async (_, _) =>
        {
            if (await PickAndUploadAvatarAsync())
            {
                avatarPreview.Source = ViewModel.EditAvatarUrl;
            }
        };

        var firstNameBox = new TextBox { Header = "名", Text = ViewModel.EditFirstName };
        var lastNameBox = new TextBox { Header = "姓", Text = ViewModel.EditLastName };
        var genderBox = new TextBox { Header = "性别", Text = ViewModel.EditGender };
        var locationBox = new TextBox { Header = "位置", Text = ViewModel.EditLocation };
        var bioBox = new TextBox
        {
            Header = "简介",
            Text = ViewModel.EditBio,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 120,
        };

        var avatarRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
        };
        avatarRow.Children.Add(avatarPreview);
        avatarRow.Children.Add(changeAvatarBtn);

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(avatarRow);
        panel.Children.Add(firstNameBox);
        panel.Children.Add(lastNameBox);
        panel.Children.Add(genderBox);
        panel.Children.Add(locationBox);
        panel.Children.Add(bioBox);

        var dialog = new ContentDialog
        {
            Title = "编辑资料",
            Content = new ScrollViewer
            {
                Content = panel,
                MaxHeight = 480,
            },
            PrimaryButtonText = "保存",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        await ViewModel.SaveProfileAsync(
            firstNameBox.Text,
            lastNameBox.Text,
            bioBox.Text,
            locationBox.Text,
            genderBox.Text,
            ViewModel.PendingPictureId);
    }

    private async Task<bool> PickAndUploadAvatarAsync()
    {
        var picker = CreateImagePicker();
        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return false;
        }

        try
        {
            var props = await file.GetBasicPropertiesAsync();
            await using var stream = await file.OpenStreamForReadAsync();
            var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "image/jpeg" : file.ContentType;
            return await ViewModel.UploadAvatarAsync(stream, file.Name, contentType, (long)props.Size);
        }
        catch (Exception ex)
        {
            ViewModel.ErrorMessage = ex.Message;
            return false;
        }
    }

    private static FileOpenPicker CreateImagePicker()
    {
        var picker = new FileOpenPicker();
        var hwnd = WindowNative.GetWindowHandle(App.Window);
        InitializeWithWindow.Initialize(picker, hwnd);
        picker.ViewMode = PickerViewMode.Thumbnail;
        picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".webp");
        picker.FileTypeFilter.Add(".gif");
        picker.FileTypeFilter.Add(".bmp");
        return picker;
    }

    private void OpenSettings_OnClick(object sender, RoutedEventArgs e)
    {
        Frame?.Navigate(typeof(SettingsPage));
    }

    private void OnLoggedOut(object? sender, EventArgs e)
    {
        if (App.Window is MainWindow mainWindow)
        {
            mainWindow.NavigateToLogin();
        }
    }
}
