using System.Text.Json.Serialization;

namespace SolarWin.Models;

/// <summary>GET /wallet/subscriptions/groups/solian.stellar.</summary>
public sealed class SnStellarSubscriptionGroup
{
    [JsonPropertyName("group_identifier")]
    public string GroupIdentifier { get; set; } = string.Empty;

    [JsonPropertyName("catalog")]
    public SnSubscriptionGroupCatalog Catalog { get; set; } = new();

    [JsonPropertyName("current")]
    public SnSubscriptionGroupStateItem? Current { get; set; }

    [JsonPropertyName("next")]
    public SnSubscriptionGroupStateItem? Next { get; set; }

    [JsonPropertyName("subscriptions")]
    public List<SnSubscriptionGroupStateItem> Subscriptions { get; set; } = [];
}

public sealed class SnSubscriptionGroupCatalog
{
    [JsonPropertyName("group_identifier")]
    public string GroupIdentifier { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("max_perk_level")]
    public int MaxPerkLevel { get; set; }

    [JsonPropertyName("display_config")]
    public SnSubscriptionDisplayConfig? DisplayConfig { get; set; }

    [JsonPropertyName("items")]
    public List<SnSubscriptionCatalogItem> Items { get; set; } = [];
}

public sealed class SnSubscriptionGroupStateItem
{
    [JsonPropertyName("subscription")]
    public SnStellarSubscription Subscription { get; set; } = new();

    [JsonPropertyName("definition")]
    public SnSubscriptionCatalogItem? Definition { get; set; }
}

public sealed class SnSubscriptionCatalogItem
{
    [JsonPropertyName("identifier")]
    public string Identifier { get; set; } = string.Empty;

    [JsonPropertyName("group_identifier")]
    public string? GroupIdentifier { get; set; }

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = string.Empty;

    [JsonPropertyName("base_price")]
    public decimal BasePrice { get; set; }

    [JsonPropertyName("perk_level")]
    public int PerkLevel { get; set; }

    [JsonPropertyName("minimum_account_level")]
    public int? MinimumAccountLevel { get; set; }

    [JsonPropertyName("experience_multiplier")]
    public decimal? ExperienceMultiplier { get; set; }

    [JsonPropertyName("golden_point_reward")]
    public int? GoldenPointReward { get; set; }

    [JsonPropertyName("display_config")]
    public SnSubscriptionDisplayConfig? DisplayConfig { get; set; }

    [JsonPropertyName("allowed_payment_methods")]
    public List<string> AllowedPaymentMethods { get; set; } = [];

    [JsonPropertyName("provider_mappings")]
    public Dictionary<string, List<string>> ProviderMappings { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class SnSubscriptionDisplayConfig
{
    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("background_color")]
    public string? BackgroundColor { get; set; }

    [JsonPropertyName("badge_text")]
    public string? BadgeText { get; set; }
}

/// <summary>
/// Subscription fields are deliberately nullable: group responses use the compact
/// subscription reference, while create/cancel endpoints return the full entity.
/// </summary>
public sealed class SnStellarSubscription
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("identifier")]
    public string Identifier { get; set; } = string.Empty;

    [JsonPropertyName("group_identifier")]
    public string? GroupIdentifier { get; set; }

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("begun_at")]
    public DateTimeOffset? BegunAt { get; set; }

    [JsonPropertyName("ended_at")]
    public DateTimeOffset? EndedAt { get; set; }

    [JsonPropertyName("renewal_at")]
    public DateTimeOffset? RenewalAt { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("is_available")]
    public bool IsAvailable { get; set; } = true;

    [JsonPropertyName("is_pending_activation")]
    public bool IsPendingActivation { get; set; }

    [JsonPropertyName("is_free_trial")]
    public bool IsFreeTrial { get; set; }

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("payment_method")]
    public string? PaymentMethod { get; set; }

    [JsonPropertyName("base_price")]
    public decimal? BasePrice { get; set; }

    [JsonPropertyName("final_price")]
    public decimal? FinalPrice { get; set; }

    [JsonPropertyName("perk_level")]
    public int PerkLevel { get; set; }
}
