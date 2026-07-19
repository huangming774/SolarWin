using System.Text.Json.Serialization;

namespace SolarWin.Models;

/// <summary>POST /messager/chat/{roomId}/messages body (OpenAPI SendMessageRequest).</summary>
public sealed class SendMessageRequest
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("nonce")]
    public string? Nonce { get; set; }

    [JsonPropertyName("client_message_id")]
    public string? ClientMessageId { get; set; }

    [JsonPropertyName("replied_message_id")]
    public Guid? RepliedMessageId { get; set; }

    [JsonPropertyName("attachments_id")]
    public List<string>? AttachmentsId { get; set; }
}

/// <summary>POST /messager/chat/{roomId}/sync body (OpenAPI SyncRequest).</summary>
public sealed class SyncRequest
{
    [JsonPropertyName("last_sync_timestamp")]
    public long LastSyncTimestamp { get; set; }

    [JsonPropertyName("last_sync_message_id")]
    public Guid? LastSyncMessageId { get; set; }

    [JsonPropertyName("missing_sequences")]
    public List<long>? MissingSequences { get; set; }
}

/// <summary>POST /messager/chat/{roomId}/sync response (OpenAPI SyncResponse).</summary>
public sealed class SyncResponse
{
    [JsonPropertyName("messages")]
    public List<SnChatMessage>? Messages { get; set; }

    /// <summary>
    /// Server clock. OpenAPI Instant is opaque; many deployments return ISO-8601 or unix ms.
    /// </summary>
    [JsonPropertyName("current_timestamp")]
    public DateTimeOffset? CurrentTimestamp { get; set; }

    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; }
}
