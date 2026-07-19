using System.Text.Json.Serialization;

namespace SolarWin.Models;

/// <summary>Solar Network account (OpenAPI SnAccount).</summary>
public sealed class SnAccount
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

    [JsonPropertyName("nick")]
    public string? Nick { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("region")]
    public string? Region { get; set; }

    [JsonPropertyName("activated_at")]
    public DateTimeOffset? ActivatedAt { get; set; }

    [JsonPropertyName("is_superuser")]
    public bool IsSuperuser { get; set; }

    [JsonPropertyName("automated_id")]
    public Guid? AutomatedId { get; set; }

    [JsonPropertyName("profile")]
    public SnAccountProfile? Profile { get; set; }

    [JsonPropertyName("contacts")]
    public List<SnAccountContact>? Contacts { get; set; }

    [JsonPropertyName("badges")]
    public List<SnAccountBadge>? Badges { get; set; }

    [JsonPropertyName("perk_subscription")]
    public SnSubscriptionReferenceObject? PerkSubscription { get; set; }

    [JsonPropertyName("perk_level")]
    public int PerkLevel { get; set; }
}
