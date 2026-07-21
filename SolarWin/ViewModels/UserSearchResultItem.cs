using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media.Imaging;
using SolarWin.Helpers;
using SolarWin.Models;

namespace SolarWin.ViewModels;

/// <summary>One row from Passport account search, used for one-tap DM.</summary>
public partial class UserSearchResultItem : ObservableObject
{
    public UserSearchResultItem(SnAccount account, DysonFileImageLoader imageLoader)
    {
        Account = account;
        AccountId = account.Id;
        Name = account.Name ?? string.Empty;
        Nick = account.Nick ?? account.Name ?? "未知用户";
        Handle = string.IsNullOrWhiteSpace(account.Name) ? string.Empty : $"@{account.Name}";
        Subtitle = string.IsNullOrWhiteSpace(account.Name)
            ? account.Id.ToString("D")
            : $"{Handle} · {account.Id.ToString("D")[..8]}…";
        Initials = Nick.Length > 0 ? Nick[..1].ToUpperInvariant() : "?";

        AvatarUrl = CloudFileUrlHelper.ResolveAccountAvatar(account)
            ?? CloudFileUrlHelper.Resolve(account.Profile?.Picture);
        HasAvatar = !string.IsNullOrWhiteSpace(AvatarUrl);
        if (HasAvatar && imageLoader.TryGetCached(AvatarUrl, out var cached) && cached is not null)
        {
            AvatarImage = cached;
        }
    }

    public SnAccount Account { get; }

    public Guid AccountId { get; }

    public string Name { get; }

    public string Nick { get; }

    public string Handle { get; }

    public string Subtitle { get; }

    public string Initials { get; }

    public string? AvatarUrl { get; }

    public bool HasAvatar { get; }

    [ObservableProperty]
    public partial BitmapImage? AvatarImage { get; set; }
}
