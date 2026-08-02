using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SolarWin.Services;
using Windows.System;

namespace SolarWin.Controls;

public sealed partial class LinkPreviewCard : UserControl
{
    private readonly LinkPreviewService _previewService;
    private int _loadVersion;

    public LinkPreviewCard()
    {
        InitializeComponent();
        _previewService = App.Services.GetRequiredService<LinkPreviewService>();
    }

    public static readonly DependencyProperty UrlProperty = DependencyProperty.Register(
        nameof(Url),
        typeof(string),
        typeof(LinkPreviewCard),
        new PropertyMetadata(string.Empty, OnUrlChanged));

    public string Url
    {
        get => (string)GetValue(UrlProperty);
        set => SetValue(UrlProperty, value);
    }

    private static void OnUrlChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is LinkPreviewCard card)
        {
            card.CardButton.Visibility = Visibility.Collapsed;
            card.PreviewImage.Source = null;
            if (card.IsLoaded)
            {
                _ = card.LoadPreviewAsync();
            }
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _ = LoadPreviewAsync();
    }

    private async Task LoadPreviewAsync()
    {
        var version = ++_loadVersion;
        var url = Url;
        if (string.IsNullOrWhiteSpace(url))
        {
            CardButton.Visibility = Visibility.Collapsed;
            return;
        }

        var preview = await _previewService.GetPreviewAsync(url).ConfigureAwait(true);
        if (version != _loadVersion || !string.Equals(url, Url, StringComparison.Ordinal))
        {
            return;
        }

        if (preview is null)
        {
            CardButton.Visibility = Visibility.Collapsed;
            return;
        }

        SiteNameText.Text = preview.SiteName;
        TitleText.Text = preview.Title;
        DescriptionText.Text = preview.Description;
        DescriptionText.Visibility = string.IsNullOrWhiteSpace(preview.Description)
            ? Visibility.Collapsed
            : Visibility.Visible;
        PreviewImage.Source = preview.ImageUrl;
        ToolTipService.SetToolTip(CardButton, preview.Url);
        CardButton.Visibility = Visibility.Visible;
    }

    private async void CardButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (Uri.TryCreate(Url, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https")
        {
            await Launcher.LaunchUriAsync(uri);
        }
    }
}
