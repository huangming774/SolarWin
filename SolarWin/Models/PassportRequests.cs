using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolarWin.Models;

/// <summary>PATCH /passport/accounts/me/profile (OpenAPI ProfileRequest).</summary>
public sealed class ProfileRequest
{
    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }

    [JsonPropertyName("middle_name")]
    public string? MiddleName { get; set; }

    [JsonPropertyName("last_name")]
    public string? LastName { get; set; }

    [JsonPropertyName("gender")]
    public string? Gender { get; set; }

    [JsonPropertyName("pronouns")]
    public string? Pronouns { get; set; }

    [JsonPropertyName("time_zone")]
    public string? TimeZone { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("bio")]
    public string? Bio { get; set; }

    [JsonPropertyName("username_color")]
    public UsernameColor? UsernameColor { get; set; }

    [JsonPropertyName("links")]
    public List<SnProfileLink>? Links { get; set; }

    [JsonPropertyName("picture_id")]
    public string? PictureId { get; set; }

    [JsonPropertyName("background_id")]
    public string? BackgroundId { get; set; }
}

/// <summary>POST/PATCH /passport/accounts/me/statuses (OpenAPI StatusRequest).</summary>
public sealed class StatusRequest
{
    [JsonPropertyName("attitude")]
    public StatusAttitude Attitude { get; set; }

    [JsonPropertyName("type")]
    public StatusType Type { get; set; } = StatusType.None;

    [JsonPropertyName("is_automated")]
    public bool IsAutomated { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("symbol")]
    public string? Symbol { get; set; }

    [JsonPropertyName("app_identifier")]
    public string? AppIdentifier { get; set; }

    [JsonPropertyName("icon_id")]
    public string? IconId { get; set; }

    [JsonPropertyName("background_id")]
    public string? BackgroundId { get; set; }

    [JsonPropertyName("meta")]
    public Dictionary<string, JsonElement>? Meta { get; set; }
}
