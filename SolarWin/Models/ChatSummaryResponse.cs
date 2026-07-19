using System.Text.Json.Serialization;

namespace SolarWin.Models;

/// <summary>Per-room chat summary (OpenAPI ChatSummaryResponse).</summary>
public sealed class ChatSummaryResponse
{
    [JsonPropertyName("unread_count")]
    public int UnreadCount { get; set; }

    [JsonPropertyName("last_message")]
    public SnChatMessage? LastMessage { get; set; }
}
