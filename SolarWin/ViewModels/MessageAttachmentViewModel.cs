using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media.Imaging;
using SolarWin.Helpers;
using SolarWin.Models;

namespace SolarWin.ViewModels;

/// <summary>One attachment on a chat message (image or generic file).</summary>
public partial class MessageAttachmentViewModel : ObservableObject
{
    public MessageAttachmentViewModel(SnCloudFile file)
    {
        File = file;
        FileId = CloudFileUrlHelper.ResolveFileId(file);
        Url = CloudFileUrlHelper.Resolve(file);
        Name = string.IsNullOrWhiteSpace(file.Name) ? (FileId ?? "附件") : file.Name!;
        IsImage = CloudFileUrlHelper.IsLikelyImage(file)
                  || string.Equals(file.MimeType, "image/*", StringComparison.OrdinalIgnoreCase)
                  || LooksLikeImageById(file);
        MimeType = file.MimeType ?? string.Empty;
        SizeText = FormatSize(file.Size);
    }

    public SnCloudFile File { get; }

    public string? FileId { get; }

    public string? Url { get; }

    public string Name { get; }

    public bool IsImage { get; }

    public string MimeType { get; }

    public string SizeText { get; }

    [ObservableProperty]
    public partial BitmapImage? Image { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial double ImageOpacity { get; set; }

    /// <summary>When not image, still show a chip.</summary>
    public double FileChipOpacity => IsImage ? 0.0 : 1.0;

    public double ImageAreaOpacity => IsImage ? 1.0 : 0.0;

    private static bool LooksLikeImageById(SnCloudFile file)
    {
        // Some payloads only have id; treat as image if message type implies media
        // Caller can force IsImage true via message type "image".
        return false;
    }

    private static string FormatSize(long bytes)
    {
        if (bytes <= 0)
        {
            return string.Empty;
        }

        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        double v = bytes;
        string[] units = ["KB", "MB", "GB"];
        var u = -1;
        do
        {
            v /= 1024;
            u++;
        } while (v >= 1024 && u < units.Length - 1);

        return $"{v:0.#} {units[u]}";
    }
}
