using SolarWin.Models;
using SolarWin.Services;

namespace SolarWin.Helpers;

/// <summary>
/// Resolve DysonFS / cloud file URLs.
/// Canonical form: https://api.solian.app/drive/files/{id}
/// </summary>
public static class CloudFileUrlHelper
{
    public static string DriveFileUrl(string fileId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileId);
        return $"{SolarApiClient.BaseUrl.TrimEnd('/')}/drive/files/{Uri.EscapeDataString(fileId.Trim())}";
    }

    public static string? Resolve(SnCloudFile? file)
    {
        if (file is null)
        {
            return null;
        }

        // Prefer id → stable DysonFS gateway URL (almost all SN media reuses this).
        if (!string.IsNullOrWhiteSpace(file.Id))
        {
            return DriveFileUrl(file.Id);
        }

        var fromUrl = Normalize(file.Url);
        if (fromUrl is not null)
        {
            // If url already points at drive/files/{id}, keep it; else still use it.
            return fromUrl;
        }

        if (file.FileMeta is not null)
        {
            foreach (var key in new[] { "url", "thumbnail_url", "thumb_url", "preview_url", "content_url", "download_url", "id" })
            {
                if (file.FileMeta.TryGetValue(key, out var el) && el.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var s = el.GetString();
                    if (string.IsNullOrWhiteSpace(s))
                    {
                        continue;
                    }

                    // meta id without path
                    if (key == "id" || (!s.Contains('/') && !s.Contains(':')))
                    {
                        return DriveFileUrl(s);
                    }

                    var n = Normalize(s);
                    if (n is not null)
                    {
                        return n;
                    }
                }
            }
        }

        return null;
    }

    public static string? ResolveFileId(SnCloudFile? file)
    {
        if (file is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(file.Id))
        {
            return file.Id.Trim();
        }

        // Parse id from url: .../drive/files/{id}
        var url = file.Url;
        if (!string.IsNullOrWhiteSpace(url))
        {
            var marker = "/drive/files/";
            var idx = url.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var rest = url[(idx + marker.Length)..].Trim('/');
                var id = rest.Split('?', '#')[0];
                if (!string.IsNullOrWhiteSpace(id))
                {
                    return id;
                }
            }
        }

        return null;
    }

    public static bool IsLikelyImage(SnCloudFile? file)
    {
        if (file is null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(file.MimeType)
            && file.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var name = file.Name ?? string.Empty;
        var ext = Path.GetExtension(name).ToLowerInvariant();
        if (ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp" or ".heic" or ".avif")
        {
            return true;
        }

        // Width/height hint
        if (file.Width is > 0 || file.Height is > 0)
        {
            return true;
        }

        // application_type / usage
        if (string.Equals(file.ApplicationType, "image", StringComparison.OrdinalIgnoreCase)
            || string.Equals(file.Usage, "image", StringComparison.OrdinalIgnoreCase)
            || string.Equals(file.Usage, "picture", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    public static string? ResolveRoomAvatar(SnChatRoom room, Guid? currentAccountId = null)
    {
        var roomPic = Resolve(room.Picture);
        if (roomPic is not null)
        {
            return roomPic;
        }

        var realmPic = Resolve(room.Realm?.Picture);
        if (realmPic is not null)
        {
            return realmPic;
        }

        if (room.Members is { Count: > 0 })
        {
            IEnumerable<ChatMemberTransmissionObject> ordered = room.Members;
            if (currentAccountId is Guid me && me != Guid.Empty)
            {
                ordered = room.Members.OrderBy(m => m.AccountId == me ? 1 : 0);
            }

            foreach (var m in ordered)
            {
                var memberPic = Resolve(m.Account?.Profile?.Picture);
                if (memberPic is not null)
                {
                    return memberPic;
                }
            }
        }

        return Resolve(room.Account?.Profile?.Picture);
    }

    public static string? ResolveAccountAvatar(SnAccount? account)
        => Resolve(account?.Profile?.Picture);

    public static string? Normalize(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        url = url.Trim();
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        if (url.StartsWith("//", StringComparison.Ordinal))
        {
            return "https:" + url;
        }

        if (url.StartsWith('/'))
        {
            return SolarApiClient.BaseUrl.TrimEnd('/') + url;
        }

        return SolarApiClient.BaseUrl.TrimEnd('/') + "/" + url.TrimStart('/');
    }

    public static bool TryCreateUri(string? url, out Uri? uri)
    {
        uri = null;
        var n = Normalize(url);
        if (n is null)
        {
            return false;
        }

        return Uri.TryCreate(n, UriKind.Absolute, out uri);
    }
}
