using System.Text.Json.Serialization;

namespace SolarWin.Models;

/// <summary>
/// Social post model for feed APIs.
/// Not present as a single OpenAPI schema name in the contract dump; fields follow Node 1 + common SN wire shape.
/// </summary>
public sealed class SnPost
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("visibility")]
    public PostVisibility Visibility { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("deleted_at")]
    public DateTimeOffset? DeletedAt { get; set; }

    [JsonPropertyName("author_id")]
    public Guid? AuthorId { get; set; }

    [JsonPropertyName("author")]
    public SnAccount? Author { get; set; }

    [JsonPropertyName("attachments")]
    public List<SnCloudFile>? Attachments { get; set; }
}
