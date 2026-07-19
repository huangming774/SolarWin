using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolarWin.Models;

/// <summary>Account profile (OpenAPI SnAccountProfile).</summary>
public sealed class SnAccountProfile
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("deleted_at")]
    public DateTimeOffset? DeletedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }

    [JsonPropertyName("middle_name")]
    public string? MiddleName { get; set; }

    [JsonPropertyName("last_name")]
    public string? LastName { get; set; }

    [JsonPropertyName("bio")]
    public string? Bio { get; set; }

    [JsonPropertyName("gender")]
    public string? Gender { get; set; }

    [JsonPropertyName("pronouns")]
    public string? Pronouns { get; set; }

    [JsonPropertyName("time_zone")]
    public string? TimeZone { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("links")]
    public List<SnProfileLink>? Links { get; set; }

    [JsonPropertyName("username_color")]
    public UsernameColor? UsernameColor { get; set; }

    [JsonPropertyName("birthday")]
    public DateTimeOffset? Birthday { get; set; }

    [JsonPropertyName("last_seen_at")]
    public DateTimeOffset? LastSeenAt { get; set; }

    [JsonPropertyName("verification")]
    public SnVerificationMark? Verification { get; set; }

    [JsonPropertyName("active_badge")]
    public SnAccountBadgeRef? ActiveBadge { get; set; }

    [JsonPropertyName("experience")]
    public int Experience { get; set; }

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("leveling_progress")]
    public double LevelingProgress { get; set; }

    [JsonPropertyName("social_credits")]
    public double SocialCredits { get; set; }

    [JsonPropertyName("social_credits_level")]
    public int SocialCreditsLevel { get; set; }

    [JsonPropertyName("picture")]
    public SnCloudFile? Picture { get; set; }

    [JsonPropertyName("background")]
    public SnCloudFile? Background { get; set; }

    [JsonPropertyName("board")]
    public List<SnAccountBoardItem>? Board { get; set; }

    [JsonPropertyName("account_id")]
    public Guid AccountId { get; set; }

    [JsonPropertyName("resource_identifier")]
    public string? ResourceIdentifier { get; set; }
}

public sealed class SnProfileLink
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

public sealed class UsernameColor
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("direction")]
    public string? Direction { get; set; }

    [JsonPropertyName("colors")]
    public List<string>? Colors { get; set; }
}

public sealed class SnVerificationMark
{
    [JsonPropertyName("type")]
    public VerificationMarkType Type { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("verified_by")]
    public string? VerifiedBy { get; set; }
}

public sealed class SnAccountBoardItem
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("deleted_at")]
    public DateTimeOffset? DeletedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("account_id")]
    public Guid AccountId { get; set; }

    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("kind")]
    public SnAccountBoardItemKind Kind { get; set; }

    [JsonPropertyName("widget_key")]
    public string? WidgetKey { get; set; }

    [JsonPropertyName("custom_app_id")]
    public Guid? CustomAppId { get; set; }

    [JsonPropertyName("custom_app_widget_key")]
    public string? CustomAppWidgetKey { get; set; }

    [JsonPropertyName("is_enabled")]
    public bool IsEnabled { get; set; }

    [JsonPropertyName("payload")]
    public Dictionary<string, JsonElement>? Payload { get; set; }

    [JsonPropertyName("resource_identifier")]
    public string? ResourceIdentifier { get; set; }
}

public sealed class SnAccountContact
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
    public AccountContactType Type { get; set; }

    [JsonPropertyName("verified_at")]
    public DateTimeOffset? VerifiedAt { get; set; }

    [JsonPropertyName("is_primary")]
    public bool IsPrimary { get; set; }

    [JsonPropertyName("is_public")]
    public bool IsPublic { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("account_id")]
    public Guid AccountId { get; set; }
}

public sealed class SnSubscriptionReferenceObject
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("deleted_at")]
    public DateTimeOffset? DeletedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("identifier")]
    public string? Identifier { get; set; }

    [JsonPropertyName("group_identifier")]
    public string? GroupIdentifier { get; set; }

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("perk_level")]
    public int PerkLevel { get; set; }

    [JsonPropertyName("is_testing")]
    public bool IsTesting { get; set; }

    [JsonPropertyName("begun_at")]
    public DateTimeOffset? BegunAt { get; set; }

    [JsonPropertyName("ended_at")]
    public DateTimeOffset? EndedAt { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("is_available")]
    public bool IsAvailable { get; set; }

    [JsonPropertyName("is_pending_activation")]
    public bool IsPendingActivation { get; set; }

    [JsonPropertyName("is_free_trial")]
    public bool IsFreeTrial { get; set; }

    [JsonPropertyName("status")]
    public SubscriptionStatus Status { get; set; }

    [JsonPropertyName("base_price")]
    public double BasePrice { get; set; }

    [JsonPropertyName("final_price")]
    public double FinalPrice { get; set; }

    [JsonPropertyName("renewal_at")]
    public DateTimeOffset? RenewalAt { get; set; }

    [JsonPropertyName("account_id")]
    public Guid AccountId { get; set; }
}
