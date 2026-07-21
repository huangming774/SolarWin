using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolarWin.Models;

// —— Room create / update ——

/// <summary>POST/PATCH /messager/chat (OpenAPI ChatRoomRequest).</summary>
public sealed class ChatRoomRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("picture_id")]
    public string? PictureId { get; set; }

    [JsonPropertyName("background_id")]
    public string? BackgroundId { get; set; }

    [JsonPropertyName("realm_id")]
    public Guid? RealmId { get; set; }

    [JsonPropertyName("is_community")]
    public bool? IsCommunity { get; set; }

    [JsonPropertyName("is_public")]
    public bool? IsPublic { get; set; }

    [JsonPropertyName("encryption_mode")]
    public int? EncryptionMode { get; set; }
}

/// <summary>POST /messager/chat/direct (OpenAPI DirectMessageRequest).</summary>
public sealed class DirectMessageRequest
{
    [JsonPropertyName("related_user_id")]
    public Guid RelatedUserId { get; set; }

    [JsonPropertyName("encryption_mode")]
    public int? EncryptionMode { get; set; }
}

// —— Messages ——

/// <summary>DELETE /messager/chat/{roomId}/messages/{messageId} body.</summary>
public sealed class DeleteMessageRequest
{
    [JsonPropertyName("client_message_id")]
    public string? ClientMessageId { get; set; }
}

/// <summary>DELETE /messager/chat/rooms/{roomId}/messages/{messageId} (moderation).</summary>
public sealed class DeleteChatRoomMessageRequest
{
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

/// <summary>POST .../reactions (OpenAPI MessageReactionRequest).</summary>
public sealed class MessageReactionRequest
{
    [JsonPropertyName("symbol")]
    public string? Symbol { get; set; }

    /// <summary>0=Neutral, 1=Positive, 2=Negative.</summary>
    [JsonPropertyName("attitude")]
    public int Attitude { get; set; } = 1;
}

/// <summary>POST .../pins (OpenAPI PinMessageRequest).</summary>
public sealed class PinMessageRequest
{
    [JsonPropertyName("message_id")]
    public Guid MessageId { get; set; }

    [JsonPropertyName("expires_at")]
    public DateTimeOffset? ExpiresAt { get; set; }
}

/// <summary>OpenAPI SnChatMessagePin.</summary>
public sealed class SnChatMessagePin
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("message_id")]
    public Guid MessageId { get; set; }

    [JsonPropertyName("chat_room_id")]
    public Guid ChatRoomId { get; set; }

    [JsonPropertyName("pinned_by_member_id")]
    public Guid PinnedByMemberId { get; set; }

    [JsonPropertyName("expires_at")]
    public DateTimeOffset? ExpiresAt { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }
}

/// <summary>POST .../messages/placeholder.</summary>
public sealed class SendPlaceholderMessageRequest
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;
}

/// <summary>POST .../messages/redirect.</summary>
public sealed class RedirectMessagesRequest
{
    [JsonPropertyName("message_ids")]
    public List<Guid> MessageIds { get; set; } = [];
}

// —— Members ——

public sealed class ChatMemberNotifyRequest
{
    [JsonPropertyName("notify_level")]
    public int NotifyLevel { get; set; }

    [JsonPropertyName("break_until")]
    public DateTimeOffset? BreakUntil { get; set; }
}

public sealed class ChatMemberProfileRequest
{
    [JsonPropertyName("nick")]
    public string? Nick { get; set; }
}

public sealed class ChatTimeoutRequest
{
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("timeout_until")]
    public DateTimeOffset? TimeoutUntil { get; set; }
}

public sealed class OnlineMembersResponse
{
    [JsonPropertyName("online_count")]
    public int OnlineCount { get; set; }

    [JsonPropertyName("online_user_names")]
    public List<string>? OnlineUserNames { get; set; }

    [JsonPropertyName("online_accounts")]
    public List<SnAccount>? OnlineAccounts { get; set; }
}

// —— Subscriptions / status ——

public sealed class RoomSubscriptionEntry
{
    [JsonPropertyName("room_id")]
    public Guid RoomId { get; set; }

    [JsonPropertyName("member_id")]
    public Guid MemberId { get; set; }

    [JsonPropertyName("account_id")]
    public Guid AccountId { get; set; }
}

public sealed class AccountSubscriptionEntry
{
    [JsonPropertyName("room_id")]
    public Guid RoomId { get; set; }

    [JsonPropertyName("member_id")]
    public Guid MemberId { get; set; }

    [JsonPropertyName("room")]
    public SnChatRoom? Room { get; set; }
}

public sealed class ChatAccountStatusResponse
{
    [JsonPropertyName("account_id")]
    public Guid AccountId { get; set; }

    [JsonPropertyName("has_active_subscriptions")]
    public bool HasActiveSubscriptions { get; set; }

    [JsonPropertyName("has_any_web_socket_connection")]
    public bool HasAnyWebSocketConnection { get; set; }

    [JsonPropertyName("push_notifications_may_send_for_unsubscribed_rooms")]
    public bool PushNotificationsMaySendForUnsubscribedRooms { get; set; }
}

// —— Sync ——

public sealed class GlobalSyncResponse
{
    [JsonPropertyName("messages")]
    public List<SnChatMessage>? Messages { get; set; }

    [JsonPropertyName("current_timestamp")]
    public DateTimeOffset? CurrentTimestamp { get; set; }

    [JsonPropertyName("current_message_id")]
    public Guid? CurrentMessageId { get; set; }

    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; }
}

public sealed class ChatRoomSyncRequest
{
    [JsonPropertyName("last_sync_timestamp")]
    public long LastSyncTimestamp { get; set; }
}

public sealed class ChatRoomSyncResponse
{
    [JsonPropertyName("changes")]
    public List<ChatRoomSyncChange>? Changes { get; set; }

    [JsonPropertyName("summaries")]
    public List<ChatRoomSummarySyncChange>? Summaries { get; set; }

    [JsonPropertyName("groups")]
    public List<SnChatGroup>? Groups { get; set; }

    [JsonPropertyName("current_timestamp")]
    public DateTimeOffset? CurrentTimestamp { get; set; }

    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; }
}

public sealed class ChatRoomSyncChange
{
    [JsonPropertyName("room_id")]
    public Guid RoomId { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("room")]
    public SnChatRoom? Room { get; set; }
}

public sealed class ChatRoomSummarySyncChange
{
    [JsonPropertyName("room_id")]
    public Guid RoomId { get; set; }

    [JsonPropertyName("unread_count")]
    public int UnreadCount { get; set; }

    [JsonPropertyName("last_message")]
    public SnChatMessage? LastMessage { get; set; }

    [JsonPropertyName("changed_at")]
    public DateTimeOffset? ChangedAt { get; set; }
}

// —— Groups ——

public sealed class SnChatGroup
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("account_id")]
    public Guid AccountId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("room_ids")]
    public List<Guid>? RoomIds { get; set; }
}

public sealed class UpdateGroupRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("order")]
    public int? Order { get; set; }
}

public sealed class MoveToGroupRequest
{
    [JsonPropertyName("group_id")]
    public Guid? GroupId { get; set; }
}

// —— Autocomplete / bots ——

public sealed class AutocompletionRequest
{
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

public sealed class Autocompletion
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("keyword")]
    public string? Keyword { get; set; }

    [JsonPropertyName("data")]
    public JsonElement Data { get; set; }

    /// <summary>Best-effort display title from flexible <see cref="Data"/>.</summary>
    public string ResolveTitle()
    {
        if (!string.IsNullOrWhiteSpace(Keyword))
        {
            return Keyword!;
        }

        return TryDataString("display_name", "displayName", "nick", "name", "title", "label")
            ?? Type
            ?? "建议";
    }

    public string? ResolveSubtitle()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(Type))
        {
            parts.Add(Type!);
        }

        var extra = TryDataString("description", "bio", "usage", "username", "name", "handle");
        if (!string.IsNullOrWhiteSpace(extra) &&
            !string.Equals(extra, ResolveTitle(), StringComparison.OrdinalIgnoreCase))
        {
            parts.Add(extra!);
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    public string ResolveInsertText()
    {
        var k = Keyword;
        if (!string.IsNullOrWhiteSpace(k))
        {
            return k!;
        }

        return TryDataString("keyword", "mention", "name", "username", "handle") ?? string.Empty;
    }

    private string? TryDataString(params string[] keys)
    {
        if (Data.ValueKind is not JsonValueKind.Object)
        {
            if (Data.ValueKind == JsonValueKind.String)
            {
                return Data.GetString();
            }

            return null;
        }

        foreach (var key in keys)
        {
            if (Data.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String)
            {
                var s = p.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    return s;
                }
            }
        }

        return null;
    }
}

/// <summary>OpenAPI SnBotCommand (+ optional source bot key from map response).</summary>
public sealed class ChatBotCommand
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("usage")]
    public string? Usage { get; set; }

    [JsonPropertyName("parameters")]
    public List<ChatBotCommandParameter>? Parameters { get; set; }

    /// <summary>When API returns { "botKey": [ commands ] }, filled by client parser.</summary>
    [JsonIgnore]
    public string? BotKey { get; set; }

    [JsonIgnore]
    public string DisplayName
    {
        get
        {
            var n = Name?.Trim() ?? string.Empty;
            if (n.Length == 0)
            {
                return "/?";
            }

            return n.StartsWith('/') ? n : "/" + n;
        }
    }

    [JsonIgnore]
    public string DisplayDetail
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(BotKey))
            {
                parts.Add(BotKey!);
            }

            if (!string.IsNullOrWhiteSpace(Description))
            {
                parts.Add(Description!);
            }
            else if (!string.IsNullOrWhiteSpace(Usage))
            {
                parts.Add(Usage!);
            }

            if (Parameters is { Count: > 0 })
            {
                parts.Add(string.Join(" ", Parameters.Select(p =>
                    (p.Required ? "<" : "[") + (p.Name ?? "?") + (p.Required ? ">" : "]"))));
            }

            return parts.Count == 0 ? "机器人命令" : string.Join(" · ", parts);
        }
    }
}

/// <summary>OpenAPI SnBotCommandParameter.</summary>
public sealed class ChatBotCommandParameter
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("required")]
    public bool Required { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

// —— E2EE / MLS ——

public sealed class EnableE2eeRequest
{
    [JsonPropertyName("encryption_mode")]
    public int EncryptionMode { get; set; } = 3;
}

public sealed class EnableMlsRequest
{
    [JsonPropertyName("mls_group_id")]
    public string? MlsGroupId { get; set; }
}

// —— Realtime voice ——

public sealed class JoinCallResponse
{
    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [JsonPropertyName("endpoint")]
    public string? Endpoint { get; set; }

    [JsonPropertyName("token")]
    public string? Token { get; set; }

    [JsonPropertyName("call_id")]
    public Guid CallId { get; set; }

    [JsonPropertyName("room_name")]
    public string? RoomName { get; set; }

    [JsonPropertyName("room_title")]
    public string? RoomTitle { get; set; }

    [JsonPropertyName("is_admin")]
    public bool IsAdmin { get; set; }

    [JsonPropertyName("participants")]
    public List<CallParticipant>? Participants { get; set; }
}

public sealed class CallParticipant
{
    [JsonPropertyName("identity")]
    public string? Identity { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("account_id")]
    public Guid? AccountId { get; set; }

    [JsonPropertyName("joined_at")]
    public DateTimeOffset? JoinedAt { get; set; }
}

public sealed class KickParticipantRequest
{
    [JsonPropertyName("ban_duration_minutes")]
    public int? BanDurationMinutes { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}
