using System;
using System.Collections.Generic;
using SolarWin.Models;

namespace SolarWin.ViewModels;

/// <summary>
/// View-model wrapper around <see cref="ChatSearchResult"/> for the inline search-results
/// panel on <see cref="Views.ChatPage"/>. Exposes flat strings (room name, sender name,
/// snippet text) and pre-formatted fields so XAML bindings stay simple.
/// </summary>
public sealed class ChatSearchResultItem
{
    public ChatSearchResultItem(ChatSearchResult result, SnippetRenderKind renderKind = SnippetRenderKind.Markers)
    {
        Result = result;
        MessageId = result.Row.MessageId;
        RoomId = result.Row.RoomId;
        RoomName = result.Row.RoomName ?? "私聊";
        SenderName = result.Row.SenderName ?? "对方";
        TimeText = FormatRelativeTime(result.Row.CreatedAt);
        Snippet = result.Snippet;
        SnippetText = renderKind == SnippetRenderKind.Markers
            ? FlattenWithMarkers(result.Snippet)
            : FlattenPlain(result.Snippet);
    }

    public ChatSearchResult Result { get; }

    public Guid MessageId { get; }

    public Guid RoomId { get; }

    public string RoomName { get; }

    public string SenderName { get; }

    public string TimeText { get; }

    /// <summary>Raw segments for templates that want to render mixed styles per segment.</summary>
    public IReadOnlyList<SnippetSegment> Snippet { get; }

    /// <summary>Single-line snippet with the matched substring wrapped in 「…」.</summary>
    public string SnippetText { get; }

    private static string FlattenWithMarkers(IReadOnlyList<SnippetSegment> segments)
    {
        if (segments.Count == 0)
        {
            return string.Empty;
        }

        var sb = new System.Text.StringBuilder(segments.Count * 12);
        foreach (var s in segments)
        {
            if (s.IsMatch)
            {
                sb.Append('「').Append(s.Text).Append('」');
            }
            else
            {
                sb.Append(s.Text);
            }
        }

        return sb.ToString();
    }

    private static string FlattenPlain(IReadOnlyList<SnippetSegment> segments)
    {
        var sb = new System.Text.StringBuilder(segments.Count * 12);
        foreach (var s in segments)
        {
            sb.Append(s.Text);
        }

        return sb.ToString();
    }

    private static string FormatRelativeTime(DateTimeOffset? time)
    {
        if (time is null)
        {
            return string.Empty;
        }

        var local = time.Value.ToLocalTime();
        var diff = DateTimeOffset.Now - local;
        if (diff.TotalSeconds < 60) return "刚刚";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} 分钟前";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} 小时前";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} 天前";
        return local.ToString("MM-dd");
    }
}

/// <summary>How the snippet should be flattened for display.</summary>
public enum SnippetRenderKind
{
    /// <summary>Matches wrapped in 「…」 markers, plain text otherwise. Used by the inline panel.</summary>
    Markers,

    /// <summary>Just the concatenation — no markers. Useful for accessibility/clipboard.</summary>
    Plain,
}