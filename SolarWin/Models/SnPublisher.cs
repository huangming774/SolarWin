using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolarWin.Models;

/// <summary>Sphere publisher (OpenAPI SnPublisher).</summary>
public sealed class SnPublisher
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("deleted_at")]
    public DateTimeOffset? DeletedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("type")]
    public PublisherType Type { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("nick")]
    public string? Nick { get; set; }

    [JsonPropertyName("bio")]
    public string? Bio { get; set; }

    [JsonPropertyName("picture")]
    public SnCloudFile? Picture { get; set; }

    [JsonPropertyName("background")]
    public SnCloudFile? Background { get; set; }

    [JsonPropertyName("verification")]
    public SnVerificationMark? Verification { get; set; }

    [JsonPropertyName("meta")]
    public Dictionary<string, JsonElement>? Meta { get; set; }

    [JsonPropertyName("account_id")]
    public Guid? AccountId { get; set; }

    [JsonPropertyName("account")]
    public SnAccount? Account { get; set; }

    [JsonPropertyName("realm_id")]
    public Guid? RealmId { get; set; }

    [JsonPropertyName("realm")]
    public SnRealm? Realm { get; set; }

    [JsonPropertyName("payout_wallet_id")]
    public Guid? PayoutWalletId { get; set; }

    /// <summary>API may send null — treated as false via FlexibleBoolConverter / nullable.</summary>
    [JsonPropertyName("gatekept_follows")]
    public bool? GatekeptFollows { get; set; }

    [JsonPropertyName("moderate_subscription")]
    public bool? ModerateSubscription { get; set; }

    [JsonPropertyName("rating")]
    public double Rating { get; set; }

    [JsonPropertyName("rating_level")]
    public int RatingLevel { get; set; }

    [JsonPropertyName("is_shadowbanned")]
    public bool? IsShadowbanned { get; set; }

    [JsonPropertyName("is_gatekept")]
    public bool? IsGatekept { get; set; }

    [JsonPropertyName("is_moderate_subscription")]
    public bool? IsModerateSubscription { get; set; }

    [JsonPropertyName("shadowban_reason")]
    public string? ShadowbanReason { get; set; }

    [JsonPropertyName("shadowbanned_at")]
    public DateTimeOffset? ShadowbannedAt { get; set; }
}

/// <summary>Publisher team member (OpenAPI SnPublisherMember).</summary>
public sealed class SnPublisherMember
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("publisher_id")]
    public Guid PublisherId { get; set; }

    [JsonPropertyName("publisher")]
    public SnPublisher? Publisher { get; set; }

    [JsonPropertyName("account_id")]
    public Guid AccountId { get; set; }

    [JsonPropertyName("account")]
    public SnAccount? Account { get; set; }

    [JsonPropertyName("role")]
    public PublisherMemberRole Role { get; set; }

    [JsonPropertyName("joined_at")]
    public DateTimeOffset? JoinedAt { get; set; }
}

/// <summary>GET /sphere/publishers/{name}/stats (OpenAPI PublisherStats).</summary>
public sealed class PublisherStats
{
    [JsonPropertyName("posts_created")]
    public int PostsCreated { get; set; }

    [JsonPropertyName("sticker_packs_created")]
    public int StickerPacksCreated { get; set; }

    [JsonPropertyName("stickers_created")]
    public int StickersCreated { get; set; }

    [JsonPropertyName("upvote_received")]
    public int UpvoteReceived { get; set; }

    [JsonPropertyName("downvote_received")]
    public int DownvoteReceived { get; set; }

    [JsonPropertyName("subscribers_count")]
    public int SubscribersCount { get; set; }
}

/// <summary>Feature flag row (OpenAPI SnPublisherFeature).</summary>
public sealed class SnPublisherFeature
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("flag")]
    public string? Flag { get; set; }

    [JsonPropertyName("expired_at")]
    public DateTimeOffset? ExpiredAt { get; set; }

    [JsonPropertyName("publisher_id")]
    public Guid PublisherId { get; set; }
}
