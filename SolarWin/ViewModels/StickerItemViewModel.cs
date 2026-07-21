using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media.Imaging;
using SolarWin.Helpers;
using SolarWin.Models;
using SolarWin.Services;

namespace SolarWin.ViewModels;

/// <summary>Sticker pack row with cover image (icon, or first embedded sticker as fallback).</summary>
public partial class StickerPackItemViewModel : ObservableObject
{
    public StickerPackItemViewModel(StickerPack pack, DysonFileImageLoader images)
    {
        Pack = pack;
        PackId = pack.Id;
        Title = pack.Name ?? pack.Slug ?? "贴纸包";
        Subtitle = string.IsNullOrWhiteSpace(pack.Prefix)
            ? (pack.Description ?? "")
            : $"/{pack.Prefix}";
        IconFileId = ResolveCoverFileId(pack);
        _images = images;
    }

    public StickerPackItemViewModel(StickerPackOwnership ownership, DysonFileImageLoader images)
        : this(ownership.Pack ?? new StickerPack { Id = ownership.PackId, Name = "贴纸包" }, images)
    {
        Ownership = ownership;
        if (PackId == Guid.Empty)
        {
            PackId = ownership.PackId;
        }
    }

    private readonly DysonFileImageLoader _images;

    public StickerPack Pack { get; }

    public StickerPackOwnership? Ownership { get; }

    public Guid PackId { get; }

    public string Title { get; }

    public string Subtitle { get; }

    public string? IconFileId { get; }

    [ObservableProperty]
    public partial BitmapImage? IconImage { get; set; }

    [ObservableProperty]
    public partial bool IsLoadingIcon { get; set; }

    public async Task LoadIconAsync()
    {
        if (string.IsNullOrWhiteSpace(IconFileId))
        {
            return;
        }

        IsLoadingIcon = true;
        try
        {
            IconImage = await _images.LoadSafeAsync(IconFileId).ConfigureAwait(true);
        }
        finally
        {
            IsLoadingIcon = false;
        }
    }

    /// <summary>
    /// Prefer pack.icon; if missing, try first embedded sticker.image
    /// (some ownership/list payloads omit icon but include stickers preview).
    /// </summary>
    private static string? ResolveCoverFileId(StickerPack pack)
    {
        var fromIcon = CloudFileUrlHelper.ResolveFileId(pack.Icon)
            ?? CloudFileUrlHelper.Resolve(pack.Icon);
        if (!string.IsNullOrWhiteSpace(fromIcon))
        {
            return fromIcon;
        }

        if (pack.Stickers is { Count: > 0 })
        {
            foreach (var s in pack.Stickers.OrderBy(x => x.Order))
            {
                var id = CloudFileUrlHelper.ResolveFileId(s.Image)
                    ?? CloudFileUrlHelper.Resolve(s.Image);
                if (!string.IsNullOrWhiteSpace(id))
                {
                    return id;
                }
            }
        }

        return null;
    }
}

/// <summary>Single sticker tile with bitmap.</summary>
public partial class StickerItemViewModel : ObservableObject
{
    public StickerItemViewModel(SnSticker sticker, DysonFileImageLoader images)
    {
        Sticker = sticker;
        Id = sticker.Id;
        PackId = sticker.PackId;
        Title = sticker.DisplayName;
        ImageFileId = CloudFileUrlHelper.ResolveFileId(sticker.Image)
            ?? CloudFileUrlHelper.Resolve(sticker.Image);
        _images = images;
    }

    private readonly DysonFileImageLoader _images;

    public SnSticker Sticker { get; }

    public Guid Id { get; }

    public Guid PackId { get; }

    public string Title { get; }

    public string? ImageFileId { get; }

    [ObservableProperty]
    public partial BitmapImage? Image { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    public async Task LoadImageAsync()
    {
        if (string.IsNullOrWhiteSpace(ImageFileId))
        {
            return;
        }

        IsLoading = true;
        try
        {
            Image = await _images.LoadSafeAsync(ImageFileId).ConfigureAwait(true);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
