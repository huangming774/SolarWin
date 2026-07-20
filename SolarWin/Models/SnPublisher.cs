using System.Text.Json.Serialization;

namespace SolarWin.Models;

/// <summary>
/// Sphere publisher (OpenAPI SnPublisher, trimmed to feed-relevant fields).
/// </summary>
public sealed class SnPublisher
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("nick")]
    public string? Nick { get; set; }

    [JsonPropertyName("bio")]
    public string? Bio { get; set; }

    [JsonPropertyName("picture")]
    public SnCloudFile? Picture { get; set; }

    [JsonPropertyName("account_id")]
    public Guid? AccountId { get; set; }
}
