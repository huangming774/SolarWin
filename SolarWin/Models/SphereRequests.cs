using System.Text.Json.Serialization;

namespace SolarWin.Models;

/// <summary>
/// POST /sphere/posts body (OpenAPI PostRequest, trimmed to compose-box fields).
/// Null fields are omitted by JsonDefaults.
/// </summary>
public sealed class CreatePostRequest
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    /// <summary>0 = Public (PostVisibility 0-5).</summary>
    [JsonPropertyName("visibility")]
    public int Visibility { get; set; }

    /// <summary>0 = moment/short post (PostType 0-2).</summary>
    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    [JsonPropertyName("attachments")]
    public List<string>? Attachments { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }
}
