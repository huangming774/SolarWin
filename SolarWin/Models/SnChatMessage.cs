using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolarWin.Models;

/// <summary>Chat message (OpenAPI SnChatMessage).</summary>
public sealed class SnChatMessage
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("deleted_at")]
    public DateTimeOffset? DeletedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("room_sequence")]
    public long RoomSequence { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("is_encrypted")]
    public bool IsEncrypted { get; set; }

    [JsonPropertyName("ciphertext")]
    public byte[]? Ciphertext { get; set; }

    [JsonPropertyName("encryption_header")]
    public byte[]? EncryptionHeader { get; set; }

    [JsonPropertyName("encryption_signature")]
    public byte[]? EncryptionSignature { get; set; }

    [JsonPropertyName("encryption_scheme")]
    public string? EncryptionScheme { get; set; }

    [JsonPropertyName("encryption_epoch")]
    public long? EncryptionEpoch { get; set; }

    [JsonPropertyName("encryption_message_type")]
    public string? EncryptionMessageType { get; set; }

    [JsonPropertyName("client_message_id")]
    public string? ClientMessageId { get; set; }

    [JsonPropertyName("meta")]
    public Dictionary<string, JsonElement>? Meta { get; set; }

    [JsonPropertyName("members_mentioned")]
    public List<Guid>? MembersMentioned { get; set; }

    [JsonPropertyName("nonce")]
    public string? Nonce { get; set; }

    [JsonPropertyName("edited_at")]
    public DateTimeOffset? EditedAt { get; set; }

    [JsonPropertyName("attachments")]
    public List<SnCloudFile>? Attachments { get; set; }

    [JsonPropertyName("reactions_count")]
    public Dictionary<string, int>? ReactionsCount { get; set; }

    [JsonPropertyName("reactions_made")]
    public Dictionary<string, bool>? ReactionsMade { get; set; }

    [JsonPropertyName("reactions")]
    public List<SnChatReaction>? Reactions { get; set; }

    [JsonPropertyName("replied_message_id")]
    public Guid? RepliedMessageId { get; set; }

    [JsonPropertyName("replied_message")]
    public SnChatMessage? RepliedMessage { get; set; }

    [JsonPropertyName("forwarded_message_id")]
    public Guid? ForwardedMessageId { get; set; }

    [JsonPropertyName("forwarded_message")]
    public SnChatMessage? ForwardedMessage { get; set; }

    [JsonPropertyName("sender_id")]
    public Guid SenderId { get; set; }

    [JsonPropertyName("sender")]
    public SnChatMember? Sender { get; set; }

    [JsonPropertyName("chat_room_id")]
    public Guid ChatRoomId { get; set; }

    [JsonPropertyName("chat_room")]
    public SnChatRoom? ChatRoom { get; set; }

    [JsonPropertyName("resource_identifier")]
    public string? ResourceIdentifier { get; set; }
}

public sealed class SnChatReaction
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("deleted_at")]
    public DateTimeOffset? DeletedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("message_id")]
    public Guid MessageId { get; set; }

    [JsonPropertyName("sender_id")]
    public Guid SenderId { get; set; }

    [JsonPropertyName("sender")]
    public SnChatMember? Sender { get; set; }

    [JsonPropertyName("symbol")]
    public string? Symbol { get; set; }

    [JsonPropertyName("attitude")]
    public MessageReactionAttitude Attitude { get; set; }
}

/// <summary>Full chat member (OpenAPI SnChatMember).</summary>
public sealed class SnChatMember
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("deleted_at")]
    public DateTimeOffset? DeletedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("chat_room_id")]
    public Guid ChatRoomId { get; set; }

    [JsonPropertyName("chat_room")]
    public SnChatRoom? ChatRoom { get; set; }

    [JsonPropertyName("account_id")]
    public Guid AccountId { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("account")]
    public SnAccount? Account { get; set; }

    [JsonPropertyName("status")]
    public SnAccountStatus? Status { get; set; }

    [JsonPropertyName("nick")]
    public string? Nick { get; set; }

    [JsonPropertyName("realm_nick")]
    public string? RealmNick { get; set; }

    [JsonPropertyName("realm_bio")]
    public string? RealmBio { get; set; }

    [JsonPropertyName("realm_experience")]
    public int? RealmExperience { get; set; }

    [JsonPropertyName("realm_level")]
    public int? RealmLevel { get; set; }

    [JsonPropertyName("realm_leveling_progress")]
    public double? RealmLevelingProgress { get; set; }

    [JsonPropertyName("realm_label")]
    public SnRealmLabel? RealmLabel { get; set; }

    [JsonPropertyName("chat_group_id")]
    public Guid? ChatGroupId { get; set; }

    [JsonPropertyName("notify")]
    public ChatMemberNotify Notify { get; set; }

    [JsonPropertyName("last_read_at")]
    public DateTimeOffset? LastReadAt { get; set; }

    [JsonPropertyName("joined_at")]
    public DateTimeOffset? JoinedAt { get; set; }

    [JsonPropertyName("leave_at")]
    public DateTimeOffset? LeaveAt { get; set; }

    [JsonPropertyName("invited_by_id")]
    public Guid? InvitedById { get; set; }

    [JsonPropertyName("invited_by")]
    public SnChatMember? InvitedBy { get; set; }

    [JsonPropertyName("break_until")]
    public DateTimeOffset? BreakUntil { get; set; }

    [JsonPropertyName("timeout_until")]
    public DateTimeOffset? TimeoutUntil { get; set; }
}
