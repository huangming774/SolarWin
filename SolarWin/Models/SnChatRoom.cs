using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolarWin.Models;

/// <summary>Chat room (OpenAPI SnChatRoom).</summary>
public sealed class SnChatRoom
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("deleted_at")]
    public DateTimeOffset? DeletedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("type")]
    public ChatRoomType Type { get; set; }

    [JsonPropertyName("is_community")]
    public bool IsCommunity { get; set; }

    [JsonPropertyName("is_public")]
    public bool IsPublic { get; set; }

    [JsonPropertyName("encryption_mode")]
    public ChatRoomEncryptionMode EncryptionMode { get; set; }

    [JsonPropertyName("mls_group_id")]
    public string? MlsGroupId { get; set; }

    [JsonPropertyName("e2ee_policy")]
    public Dictionary<string, JsonElement>? E2eePolicy { get; set; }

    [JsonPropertyName("picture")]
    public SnCloudFile? Picture { get; set; }

    [JsonPropertyName("background")]
    public SnCloudFile? Background { get; set; }

    [JsonPropertyName("account_id")]
    public Guid? AccountId { get; set; }

    [JsonPropertyName("account")]
    public SnAccount? Account { get; set; }

    [JsonPropertyName("realm_id")]
    public Guid? RealmId { get; set; }

    [JsonPropertyName("realm")]
    public SnRealm? Realm { get; set; }

    [JsonPropertyName("members")]
    public List<ChatMemberTransmissionObject>? Members { get; set; }

    [JsonPropertyName("resource_identifier")]
    public string? ResourceIdentifier { get; set; }
}

/// <summary>Member projection on rooms (OpenAPI ChatMemberTransmissionObject).</summary>
public sealed class ChatMemberTransmissionObject
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

    [JsonPropertyName("account_id")]
    public Guid AccountId { get; set; }

    [JsonPropertyName("account")]
    public SnAccount? Account { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

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

    [JsonPropertyName("notify")]
    public ChatMemberNotify Notify { get; set; }

    [JsonPropertyName("joined_at")]
    public DateTimeOffset? JoinedAt { get; set; }

    [JsonPropertyName("leave_at")]
    public DateTimeOffset? LeaveAt { get; set; }

    [JsonPropertyName("chat_group_id")]
    public Guid? ChatGroupId { get; set; }
}
