using CommunityToolkit.Mvvm.ComponentModel;

namespace SolarWin.ViewModels;

/// <summary>One row in the chat composer suggestion list (bots / autocomplete / mentions).</summary>
public partial class ChatSuggestionItem : ObservableObject
{
    public required string Kind { get; init; }

    public required string Title { get; init; }

    public string? Subtitle { get; init; }

    /// <summary>Text inserted into the draft when chosen.</summary>
    public required string InsertText { get; init; }

    /// <summary>Optional group label (bot key, "成员", "服务端"…).</summary>
    public string? Group { get; init; }

    public string KindLabel => Kind switch
    {
        "bot" => "命令",
        "user" or "member" or "mention" or "account" => "成员",
        "emoji" => "表情",
        "sticker" => "贴纸",
        "local" => "本地",
        _ => "建议",
    };

    /// <summary>Segoe Fluent glyph for the kind badge.</summary>
    public string Glyph => Kind switch
    {
        "bot" => "\uE99A",
        "user" or "member" or "mention" or "account" => "\uE77B",
        "emoji" or "sticker" => "\uE76E",
        "local" => "\uE8F1",
        _ => "\uE721",
    };

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
