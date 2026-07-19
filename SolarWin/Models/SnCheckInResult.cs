using System.Text.Json.Serialization;

namespace SolarWin.Models;

/// <summary>Daily check-in result (OpenAPI SnCheckInResult).</summary>
public sealed class SnCheckInResult
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("deleted_at")]
    public DateTimeOffset? DeletedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("level")]
    public CheckInResultLevel Level { get; set; }

    [JsonPropertyName("reward_points")]
    public double? RewardPoints { get; set; }

    [JsonPropertyName("reward_experience")]
    public int? RewardExperience { get; set; }

    [JsonPropertyName("tips")]
    public List<CheckInFortuneTip>? Tips { get; set; }

    [JsonPropertyName("fortune_report")]
    public CheckInFortuneReport? FortuneReport { get; set; }

    [JsonPropertyName("account_id")]
    public Guid AccountId { get; set; }

    [JsonPropertyName("account")]
    public SnAccount? Account { get; set; }

    [JsonPropertyName("backdated_from")]
    public DateTimeOffset? BackdatedFrom { get; set; }
}

public sealed class CheckInFortuneTip
{
    [JsonPropertyName("is_positive")]
    public bool IsPositive { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

public sealed class CheckInFortuneReport
{
    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("poem")]
    public string? Poem { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("summary_detail")]
    public string? SummaryDetail { get; set; }

    [JsonPropertyName("wish")]
    public string? Wish { get; set; }

    [JsonPropertyName("love")]
    public string? Love { get; set; }

    [JsonPropertyName("study")]
    public string? Study { get; set; }

    [JsonPropertyName("career")]
    public string? Career { get; set; }

    [JsonPropertyName("health")]
    public string? Health { get; set; }

    [JsonPropertyName("lost_item")]
    public string? LostItem { get; set; }

    [JsonPropertyName("lucky_color")]
    public string? LuckyColor { get; set; }

    [JsonPropertyName("lucky_direction")]
    public string? LuckyDirection { get; set; }

    [JsonPropertyName("lucky_time")]
    public string? LuckyTime { get; set; }

    [JsonPropertyName("lucky_item")]
    public string? LuckyItem { get; set; }

    [JsonPropertyName("lucky_action")]
    public string? LuckyAction { get; set; }

    [JsonPropertyName("avoid_action")]
    public string? AvoidAction { get; set; }

    [JsonPropertyName("ritual")]
    public string? Ritual { get; set; }
}
