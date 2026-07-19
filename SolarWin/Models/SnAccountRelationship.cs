using System.Text.Json.Serialization;

namespace SolarWin.Models;

/// <summary>Account relationship (OpenAPI SnAccountRelationship).</summary>
public sealed class SnAccountRelationship
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("deleted_at")]
    public DateTimeOffset? DeletedAt { get; set; }

    [JsonPropertyName("account_id")]
    public Guid AccountId { get; set; }

    [JsonPropertyName("account")]
    public SnAccount? Account { get; set; }

    [JsonPropertyName("related_id")]
    public Guid RelatedId { get; set; }

    [JsonPropertyName("related")]
    public SnAccount? Related { get; set; }

    [JsonPropertyName("expired_at")]
    public DateTimeOffset? ExpiredAt { get; set; }

    [JsonPropertyName("degrade_to_status")]
    public RelationshipStatus DegradeToStatus { get; set; }

    [JsonPropertyName("status")]
    public RelationshipStatus Status { get; set; }

    [JsonPropertyName("alias")]
    public string? Alias { get; set; }
}
