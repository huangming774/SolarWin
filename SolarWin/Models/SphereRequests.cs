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

    /// <summary>When set, creates a reply to this post.</summary>
    [JsonPropertyName("replied_post_id")]
    public Guid? RepliedPostId { get; set; }

    /// <summary>When set, creates a quote/forward of this post.</summary>
    [JsonPropertyName("forwarded_post_id")]
    public Guid? ForwardedPostId { get; set; }
}

/// <summary>POST /sphere/posts/{id}/reactions (OpenAPI PostReactionRequest).</summary>
public sealed class PostReactionRequest
{
    [JsonPropertyName("symbol")]
    public string? Symbol { get; set; }

    /// <summary>0=Positive, 1=Neutral, 2=Negative (Solian / server PostReactionAttitude).</summary>
    [JsonPropertyName("attitude")]
    public int Attitude { get; set; }
}

/// <summary>POST /sphere/posts/{id}/boost (OpenAPI BoostRequest).</summary>
public sealed class BoostRequest
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

/// <summary>OpenAPI SnPostReaction.</summary>
public sealed class SnPostReaction
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("symbol")]
    public string? Symbol { get; set; }

    [JsonPropertyName("attitude")]
    public int Attitude { get; set; }

    [JsonPropertyName("post_id")]
    public Guid PostId { get; set; }

    [JsonPropertyName("account_id")]
    public Guid? AccountId { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }
}

/// <summary>OpenAPI SnBoost (fields kept nullable for gateway variance).</summary>
public sealed class SnBoost
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("post_id")]
    public Guid PostId { get; set; }

    [JsonPropertyName("actor_id")]
    public Guid? ActorId { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("boosted_at")]
    public DateTimeOffset? BoostedAt { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }
}

/// <summary>OpenAPI SnPostBookmark.</summary>
public sealed class SnPostBookmark
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("post_id")]
    public Guid PostId { get; set; }

    [JsonPropertyName("account_id")]
    public Guid AccountId { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }
}

/// <summary>OpenAPI SnTimelinePage.</summary>
public sealed class SnTimelinePage
{
    [JsonPropertyName("items")]
    public List<SnTimelineEvent>? Items { get; set; }

    [JsonPropertyName("next_cursor")]
    public string? NextCursor { get; set; }

    [JsonPropertyName("mode")]
    public string? Mode { get; set; }
}

/// <summary>OpenAPI SnTimelineEvent. <see cref="Data"/> is untyped in the schema.</summary>
public sealed class SnTimelineEvent
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("resource_identifier")]
    public string? ResourceIdentifier { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>Payload may be an SnPost, or a wrapper containing one.</summary>
    [JsonPropertyName("data")]
    public System.Text.Json.JsonElement Data { get; set; }
}

/// <summary>OpenAPI ThreadedReplyNode.</summary>
public sealed class ThreadedReplyNode
{
    [JsonPropertyName("post")]
    public SnPost Post { get; set; } = null!;

    [JsonPropertyName("depth")]
    public int Depth { get; set; }

    [JsonPropertyName("parent_id")]
    public Guid? ParentId { get; set; }
}

/// <summary>OpenAPI PostThreadResponse.</summary>
public sealed class PostThreadResponse
{
    [JsonPropertyName("ancestors")]
    public List<ThreadedReplyNode>? Ancestors { get; set; }

    [JsonPropertyName("current")]
    public ThreadedReplyNode? Current { get; set; }

    [JsonPropertyName("descendants")]
    public List<ThreadedReplyNode>? Descendants { get; set; }

    [JsonPropertyName("has_more")]
    public bool HasMore { get; set; }
}
