using System.Text.Json.Serialization;

namespace SolarWin.Models;

/// <summary>POST /sphere/publishers/* body (OpenAPI PublisherRequest).</summary>
public sealed class PublisherRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("nick")]
    public string? Nick { get; set; }

    [JsonPropertyName("bio")]
    public string? Bio { get; set; }

    [JsonPropertyName("picture_id")]
    public string? PictureId { get; set; }

    [JsonPropertyName("background_id")]
    public string? BackgroundId { get; set; }

    [JsonPropertyName("default_post_tags")]
    public List<string>? DefaultPostTags { get; set; }

    [JsonPropertyName("default_post_categories")]
    public List<string>? DefaultPostCategories { get; set; }

    [JsonPropertyName("payout_wallet_id")]
    public Guid? PayoutWalletId { get; set; }
}

/// <summary>POST /sphere/publishers/invites/{name}.</summary>
public sealed class PublisherMemberRequest
{
    [JsonPropertyName("related_user_id")]
    public Guid RelatedUserId { get; set; }

    [JsonPropertyName("role")]
    public PublisherMemberRole Role { get; set; } = PublisherMemberRole.Member;
}

/// <summary>POST /sphere/publishers/{name}/features.</summary>
public sealed class PublisherFeatureRequest
{
    [JsonPropertyName("flag")]
    public string? Flag { get; set; }

    [JsonPropertyName("expired_at")]
    public DateTimeOffset? ExpiredAt { get; set; }
}

/// <summary>OpenAPI SnPostCategory.</summary>
public sealed class SnPostCategory
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("usage")]
    public int Usage { get; set; }
}

/// <summary>OpenAPI SnPostTag.</summary>
public sealed class SnPostTag
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("owner_publisher_id")]
    public Guid? OwnerPublisherId { get; set; }

    [JsonPropertyName("is_protected")]
    public bool IsProtected { get; set; }

    [JsonPropertyName("is_event")]
    public bool IsEvent { get; set; }

    [JsonPropertyName("usage")]
    public int Usage { get; set; }
}

/// <summary>OpenAPI SnPostCollection.</summary>
public sealed class SnPostCollection
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("publisher_id")]
    public Guid PublisherId { get; set; }

    [JsonPropertyName("publisher")]
    public SnPublisher? Publisher { get; set; }

    [JsonPropertyName("item_count")]
    public int ItemCount { get; set; }
}

/// <summary>OpenAPI SnPublisherSubscription.</summary>
public sealed class SnPublisherSubscription
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("publisher_id")]
    public Guid PublisherId { get; set; }

    [JsonPropertyName("publisher")]
    public SnPublisher? Publisher { get; set; }

    [JsonPropertyName("account_id")]
    public Guid AccountId { get; set; }

    [JsonPropertyName("last_read_at")]
    public DateTimeOffset? LastReadAt { get; set; }

    [JsonPropertyName("notify")]
    public bool Notify { get; set; }

    [JsonPropertyName("ended_at")]
    public DateTimeOffset? EndedAt { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }
}

/// <summary>OpenAPI SnPostSubscription.</summary>
public sealed class SnPostSubscription
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("post_id")]
    public Guid PostId { get; set; }

    [JsonPropertyName("account_id")]
    public Guid AccountId { get; set; }

    [JsonPropertyName("notify_reactions")]
    public bool NotifyReactions { get; set; }

    [JsonPropertyName("notify_forwards")]
    public bool NotifyForwards { get; set; }

    [JsonPropertyName("notify_edits")]
    public bool NotifyEdits { get; set; }
}

/// <summary>OpenAPI StickerPack.</summary>
public sealed class StickerPack
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("prefix")]
    public string? Prefix { get; set; }

    [JsonPropertyName("publisher_id")]
    public Guid? PublisherId { get; set; }

    /// <summary>Pack cover icon (OpenAPI SnCloudFileReferenceObject).</summary>
    [JsonPropertyName("icon")]
    public SnCloudFile? Icon { get; set; }

    /// <summary>Optional embedded stickers (some list responses include a preview).</summary>
    [JsonPropertyName("stickers")]
    public List<SnSticker>? Stickers { get; set; }

    [JsonPropertyName("popularity")]
    public int Popularity { get; set; }

    [JsonPropertyName("resource_identifier")]
    public string? ResourceIdentifier { get; set; }
}

/// <summary>OpenAPI StickerPackOwnership.</summary>
public sealed class StickerPackOwnership
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("pack_id")]
    public Guid PackId { get; set; }

    [JsonPropertyName("pack")]
    public StickerPack? Pack { get; set; }

    [JsonPropertyName("account_id")]
    public Guid AccountId { get; set; }

    [JsonPropertyName("order")]
    public int Order { get; set; }
}

/// <summary>OpenAPI SnSticker.</summary>
public sealed class SnSticker
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Sticker bitmap (OpenAPI SnCloudFileReferenceObject → id for DysonFS).</summary>
    [JsonPropertyName("image")]
    public SnCloudFile? Image { get; set; }

    [JsonPropertyName("size")]
    public int Size { get; set; }

    [JsonPropertyName("mode")]
    public int Mode { get; set; }

    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("pack_id")]
    public Guid PackId { get; set; }

    [JsonPropertyName("resource_identifier")]
    public string? ResourceIdentifier { get; set; }

    [JsonIgnore]
    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Name))
            {
                return Name!;
            }

            if (!string.IsNullOrWhiteSpace(Slug))
            {
                return Slug!;
            }

            return Id != Guid.Empty ? Id.ToString("D")[..8] : "贴纸";
        }
    }
}

/// <summary>POST /sphere/stickers/lookup/batch body.</summary>
public sealed class BatchStickerLookupRequest
{
    [JsonPropertyName("placeholders")]
    public List<string>? Placeholders { get; set; }
}

/// <summary>One result from POST /sphere/stickers/lookup/batch.</summary>
public sealed class SnStickerBatchLookupItem
{
    /// <summary>Original placeholder, e.g. <c>:prefix+slug:</c>.</summary>
    [JsonPropertyName("placeholder")]
    public string? Placeholder { get; set; }

    [JsonPropertyName("sticker")]
    public SnSticker? Sticker { get; set; }
}

/// <summary>OpenAPI SnPostAward.</summary>
public sealed class SnPostAward
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("amount")]
    public double Amount { get; set; }

    [JsonPropertyName("attitude")]
    public int Attitude { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("post_id")]
    public Guid PostId { get; set; }

    [JsonPropertyName("account_id")]
    public Guid AccountId { get; set; }

    [JsonPropertyName("settled_at")]
    public DateTimeOffset? SettledAt { get; set; }
}

/// <summary>POST /sphere/posts/{id}/awards.</summary>
public sealed class PostAwardRequest
{
    [JsonPropertyName("amount")]
    public double Amount { get; set; }

    [JsonPropertyName("attitude")]
    public int Attitude { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

/// <summary>POST /sphere/posts/{id}/sponsor.</summary>
public sealed class PostSponsorRequest
{
    [JsonPropertyName("amount")]
    public double Amount { get; set; }
}
