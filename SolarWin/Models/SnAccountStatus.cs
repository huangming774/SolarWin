using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolarWin.Models;

/// <summary>Account presence / status (OpenAPI SnAccountStatus).</summary>
public sealed class SnAccountStatus
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("deleted_at")]
    public DateTimeOffset? DeletedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("attitude")]
    public StatusAttitude Attitude { get; set; }

    [JsonPropertyName("is_online")]
    public bool IsOnline { get; set; }

    [JsonPropertyName("is_idle")]
    public bool IsIdle { get; set; }

    [JsonPropertyName("idle_since")]
    public DateTimeOffset? IdleSince { get; set; }

    [JsonPropertyName("is_customized")]
    public bool IsCustomized { get; set; }

    [JsonPropertyName("type")]
    public StatusType Type { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("symbol")]
    public string? Symbol { get; set; }

    [JsonPropertyName("icon")]
    public SnCloudFile? Icon { get; set; }

    [JsonPropertyName("background")]
    public SnCloudFile? Background { get; set; }

    [JsonPropertyName("meta")]
    public Dictionary<string, JsonElement>? Meta { get; set; }

    [JsonPropertyName("cleared_at")]
    public DateTimeOffset? ClearedAt { get; set; }

    [JsonPropertyName("app_identifier")]
    public string? AppIdentifier { get; set; }

    [JsonPropertyName("is_automated")]
    public bool IsAutomated { get; set; }

    [JsonPropertyName("account_id")]
    public Guid AccountId { get; set; }

    [JsonPropertyName("account")]
    public SnAccount? Account { get; set; }
}
