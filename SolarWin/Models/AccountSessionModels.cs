using System.Text.Json.Serialization;

namespace SolarWin.Models;

/// <summary>Saved desktop session profile (multi-account switcher).</summary>
public sealed class SavedAccountProfile
{
    [JsonPropertyName("account_id")]
    public Guid AccountId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("nick")]
    public string? Nick { get; set; }

    [JsonPropertyName("last_used_at")]
    public DateTimeOffset LastUsedAt { get; set; }

    [JsonIgnore]
    public string DisplayLabel
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Nick) && !string.IsNullOrWhiteSpace(Name))
            {
                return $"{Nick} (@{Name})";
            }

            return Nick ?? Name ?? AccountId.ToString("D")[..8];
        }
    }
}
