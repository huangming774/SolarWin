using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media.Imaging;
using SolarWin.Helpers;
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
        IsImage = !file.IsFolder && CloudFileUrlHelper.IsLikelyImage(file);
        IsVideo = !file.IsFolder && IsLikelyVideo(file);
        VideoSource = IsVideo ? CloudFileUrlHelper.Resolve(file) : null;
        FullImageSource = IsImage
            ? CloudFileUrlHelper.ResolveFileId(file) ?? CloudFileUrlHelper.Resolve(file)
            : null;
        ThumbnailUrl = ResolveThumbnail(file) ?? FullImageSource;
        ThumbnailFallbackUrl = !string.IsNullOrWhiteSpace(FullImageSource)
                               && !string.Equals(ThumbnailUrl, FullImageSource, StringComparison.OrdinalIgnoreCase)
            ? FullImageSource
            : null;
        CanLoadThumbnail = !string.IsNullOrWhiteSpace(ThumbnailUrl) && !file.IsFolder;
        IconGlyph = file.IsFolder ? "\uE8B7" : GuessFileGlyph(file.MimeType, file.Name);
        MimeType = file.MimeType ?? string.Empty;
        DownloadUrl = file.Url;
    }

    public SnCloudFile File { get; }

    public string Id { get; }

    public string Name { get; }

    public bool IsFolder { get; }

    public bool IsImage { get; }

    public bool IsVideo { get; }

    public string? VideoSource { get; }

    public string SizeText { get; }

    public string DateText { get; }

    /// <summary>GPU source for FastWin2DImage (thumb meta or image file id/url).</summary>
    [ObservableProperty]
    public partial string? ThumbnailUrl { get; set; }

    /// <summary>Original image id / URL when a generated thumbnail is missing or broken.</summary>
    public string? ThumbnailFallbackUrl { get; }

    /// <summary>Full image source used by the lightbox.</summary>
    public string? FullImageSource { get; }

    public bool CanLoadThumbnail { get; }

    public string IconGlyph { get; }

    public string MimeType { get; }

    public string? DownloadUrl { get; }

    /// <summary>Legacy BitmapImage slot (unused on GPU path).</summary>
    [ObservableProperty]
    public partial BitmapImage? Thumbnail { get; set; }

    /// <summary>Icon stays visible under FastWin2DImage until the GPU bitmap paints over it.</summary>
    public double IconOpacity => 1.0;

    private static bool IsLikelyVideo(SnCloudFile file)
    {
        if (file.MimeType?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) == true) return true;
        var extension = Path.GetExtension(file.Name);
        return string.Equals(extension, ".mp4", StringComparison.OrdinalIgnoreCase)
               || string.Equals(extension, ".mov", StringComparison.OrdinalIgnoreCase)
               || string.Equals(extension, ".mkv", StringComparison.OrdinalIgnoreCase)
               || string.Equals(extension, ".webm", StringComparison.OrdinalIgnoreCase)
               || string.Equals(extension, ".avi", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveThumbnail(SnCloudFile file)
    {
        if (file.IsFolder)
        {
            return null;
        }

        // Prefer server-provided thumbnails/previews when the gateway includes them.
        if (file.FileMeta is not null)
        {
            foreach (var key in new[] { "thumbnail_url", "thumb_url", "preview_url", "thumbnail", "thumb" })
            {
                if (file.FileMeta.TryGetValue(key, out var el) && el.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var s = el.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        return !s.Contains('/') && !s.Contains(':')
                            ? CloudFileUrlHelper.DriveFileUrl(s)
                            : CloudFileUrlHelper.Normalize(s);
                    }
                }
            }
        }

        // Image files: load via DysonFS id/url through the shared GPU loader.
        if (CloudFileUrlHelper.IsLikelyImage(file))
        {
            return CloudFileUrlHelper.ResolveFileId(file)
                   ?? CloudFileUrlHelper.Resolve(file);
        }

        return null;
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
