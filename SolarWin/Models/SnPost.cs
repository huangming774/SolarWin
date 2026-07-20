using System.Text.Json.Serialization;

namespace SolarWin.Models;

/// <summary>
/// Social post (OpenAPI SnPost in DysonNetwork.Sphere).
/// visibility/type are raw ints: the enum ranges (0-5 / 0-2) are wider than the local enums.
/// </summary>
public sealed class SnPost
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("visibility")]
    public int Visibility { get; set; }

    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("edited_at")]
    public DateTimeOffset? EditedAt { get; set; }

    [JsonPropertyName("published_at")]
    public DateTimeOffset? PublishedAt { get; set; }

    [JsonPropertyName("upvotes")]
    public int Upvotes { get; set; }

    [JsonPropertyName("downvotes")]
    public int Downvotes { get; set; }

    [JsonPropertyName("boost_count")]
    public int BoostCount { get; set; }

    [JsonPropertyName("replies_count")]
    public int RepliesCount { get; set; }

    [JsonPropertyName("views_unique")]
    public int ViewsUnique { get; set; }

    [JsonPropertyName("views_total")]
    public int ViewsTotal { get; set; }

    [JsonPropertyName("is_truncated")]
    public bool IsTruncated { get; set; }

    [JsonPropertyName("attachments")]
    public List<SnCloudFile>? Attachments { get; set; }

    [JsonPropertyName("publisher_id")]
    public Guid? PublisherId { get; set; }

    [JsonPropertyName("publisher")]
    public SnPublisher? Publisher { get; set; }

    [JsonPropertyName("replied_post_id")]
    public Guid? RepliedPostId { get; set; }

    [JsonPropertyName("forwarded_post_id")]
    public Guid? ForwardedPostId { get; set; }

    [JsonPropertyName("forwarded_post")]
    public SnPost? ForwardedPost { get; set; }
}
