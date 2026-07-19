using CommunityToolkit.Mvvm.ComponentModel;
using SolarWin.Models;

namespace SolarWin.ViewModels;

public partial class FileItemViewModel : ObservableObject
{
    public FileItemViewModel(SnCloudFile file)
    {
        File = file;
        Id = file.Id ?? string.Empty;
        Name = string.IsNullOrWhiteSpace(file.Name) ? (file.IsFolder ? "文件夹" : "未命名") : file.Name!;
        IsFolder = file.IsFolder;
        SizeText = file.IsFolder ? "—" : FormatSize(file.Size);
        DateText = FormatDate(file.UpdatedAt ?? file.CreatedAt);
        ThumbnailUrl = ResolveThumbnail(file);
        HasThumbnail = !string.IsNullOrWhiteSpace(ThumbnailUrl) && !file.IsFolder;
        IconGlyph = file.IsFolder ? "\uE8B7" : GuessFileGlyph(file.MimeType, file.Name);
        MimeType = file.MimeType ?? string.Empty;
        DownloadUrl = file.Url;
    }

    public SnCloudFile File { get; }

    public string Id { get; }

    public string Name { get; }

    public bool IsFolder { get; }

    public string SizeText { get; }

    public string DateText { get; }

    public string? ThumbnailUrl { get; }

    public bool HasThumbnail { get; }

    public string IconGlyph { get; }

    public string MimeType { get; }

    public string? DownloadUrl { get; }

    public double ThumbnailOpacity => HasThumbnail ? 1.0 : 0.0;

    public double IconOpacity => HasThumbnail ? 0.0 : 1.0;

    private static string? ResolveThumbnail(SnCloudFile file)
    {
        if (file.IsFolder)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(file.Url) && IsImage(file.MimeType, file.Name))
        {
            return file.Url;
        }

        // Some deployments put thumbnail url in file_meta.
        if (file.FileMeta is not null)
        {
            foreach (var key in new[] { "thumbnail_url", "thumb_url", "preview_url" })
            {
                if (file.FileMeta.TryGetValue(key, out var el) && el.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var s = el.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        return s;
                    }
                }
            }
        }

        return null;
    }

    private static bool IsImage(string? mime, string? name)
    {
        if (!string.IsNullOrWhiteSpace(mime) && mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var ext = Path.GetExtension(name ?? string.Empty);
        return ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp";
    }

    private static string GuessFileGlyph(string? mime, string? name)
    {
        if (!string.IsNullOrWhiteSpace(mime))
        {
            if (mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return "\uEB9F";
            if (mime.StartsWith("video/", StringComparison.OrdinalIgnoreCase)) return "\uE714";
            if (mime.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)) return "\uE8D6";
            if (mime.Contains("pdf", StringComparison.OrdinalIgnoreCase)) return "\uEA90";
            if (mime.Contains("zip", StringComparison.OrdinalIgnoreCase) || mime.Contains("compressed", StringComparison.OrdinalIgnoreCase))
                return "\uF012";
        }

        return "\uE8A5";
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        double v = bytes;
        string[] units = ["KB", "MB", "GB", "TB"];
        var u = -1;
        do
        {
            v /= 1024;
            u++;
        } while (v >= 1024 && u < units.Length - 1);

        return $"{v:0.##} {units[u]}";
    }

    private static string FormatDate(DateTimeOffset? time)
    {
        if (time is null) return "—";
        return time.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    }
}
