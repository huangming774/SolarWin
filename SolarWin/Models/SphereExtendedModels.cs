using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolarWin.Models;

// —— Surveys ——

public enum SurveyQuestionType
{
    Text = 0,
    SingleChoice = 1,
    MultiChoice = 2,
    Rating = 3,
    Other = 4,
}

public sealed class SnSurvey
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("is_anonymous")]
    public bool IsAnonymous { get; set; }

    [JsonPropertyName("hide_results")]
    public bool HideResults { get; set; }

    [JsonPropertyName("notify_subscribers")]
    public bool NotifySubscribers { get; set; }

    [JsonPropertyName("published_at")]
    public DateTimeOffset? PublishedAt { get; set; }

    [JsonPropertyName("ended_at")]
    public DateTimeOffset? EndedAt { get; set; }

    [JsonPropertyName("archived_at")]
    public DateTimeOffset? ArchivedAt { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("questions")]
    public List<SnSurveyQuestion>? Questions { get; set; }

    [JsonPropertyName("answers_count")]
    public int AnswersCount { get; set; }

    [JsonPropertyName("subscribers_count")]
    public int SubscribersCount { get; set; }

    [JsonIgnore]
    public string StatusLabel
    {
        get
        {
            if (ArchivedAt is not null) return "已归档";
            if (EndedAt is { } e && e < DateTimeOffset.UtcNow) return "已结束";
            if (PublishedAt is not null) return "已发布";
            return "草稿";
        }
    }

    [JsonIgnore]
    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? Id.ToString("D")[..8] : Title!;
}

public sealed class SnSurveyQuestion
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("type")]
    public SurveyQuestionType Type { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("required")]
    public bool Required { get; set; }

    [JsonPropertyName("options")]
    public List<SnSurveyOption>? Options { get; set; }

    [JsonIgnore]
    public string DisplayText => Title ?? Content ?? Id.ToString("D")[..8];
}

public sealed class SnSurveyOption
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonIgnore]
    public string Display => Label ?? Value ?? Id.ToString("D")[..6];
}

public sealed class SnSurveyAnswer
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("answer")]
    public Dictionary<string, JsonElement>? Answer { get; set; }

    [JsonPropertyName("account_id")]
    public Guid? AccountId { get; set; }
}

public sealed class SurveyRequest
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("is_anonymous")]
    public bool? IsAnonymous { get; set; }

    [JsonPropertyName("hide_results")]
    public bool? HideResults { get; set; }

    [JsonPropertyName("notify_subscribers")]
    public bool? NotifySubscribers { get; set; }

    [JsonPropertyName("ended_at")]
    public DateTimeOffset? EndedAt { get; set; }
}

public sealed class SurveyAnswerRequest
{
    [JsonPropertyName("answer")]
    public Dictionary<string, object?>? Answer { get; set; }
}

// —— Scrap / Translate ——

public sealed class ScrapLinkRequest
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

public sealed class ScrapLinkResult
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("site_name")]
    public string? SiteName { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }

    [JsonPropertyName("favicon")]
    public string? Favicon { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }

    [JsonIgnore]
    public string DisplayTitle => Title ?? Url ?? "（无标题）";

    [JsonIgnore]
    public string DisplayBody =>
        string.Join("\n", new[] { SiteName, Description }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

public sealed class TranslateRequest
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("source_lang")]
    public string? SourceLang { get; set; }

    [JsonPropertyName("target_lang")]
    public string? TargetLang { get; set; } = "zh";

    [JsonPropertyName("to")]
    public string? To { get; set; }
}

public sealed class TranslateResult
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("translated")]
    public string? Translated { get; set; }

    [JsonPropertyName("result")]
    public string? Result { get; set; }

    [JsonPropertyName("translation")]
    public string? Translation { get; set; }

    [JsonPropertyName("source_lang")]
    public string? SourceLang { get; set; }

    [JsonPropertyName("target_lang")]
    public string? TargetLang { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }

    [JsonIgnore]
    public string DisplayText =>
        Translated ?? Result ?? Translation ?? Text
        ?? ExtensionData?.Values.FirstOrDefault(v => v.ValueKind == JsonValueKind.String).GetString()
        ?? string.Empty;
}

// —— Quote authorizations ——

public sealed class CreateQuoteAuthorizationRequest
{
    [JsonPropertyName("post_id")]
    public Guid? PostId { get; set; }

    [JsonPropertyName("account_id")]
    public Guid? AccountId { get; set; }

    [JsonPropertyName("expires_at")]
    public DateTimeOffset? ExpiresAt { get; set; }
}

public sealed class QuoteAuthorizationItem
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("post_id")]
    public Guid? PostId { get; set; }

    [JsonPropertyName("account_id")]
    public Guid? AccountId { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("expires_at")]
    public DateTimeOffset? ExpiresAt { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }

    [JsonIgnore]
    public string Display =>
        $"授权 {Id.ToString("D")[..8]} · 帖 {PostId?.ToString("D")[..8] ?? "—"}";
}

// —— Fediverse ——

public sealed class SnFediverseActor
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("uri")]
    public string? Uri { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("domain")]
    public string? Domain { get; set; }

    [JsonPropertyName("instance")]
    public string? Instance { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("avatar")]
    public string? Avatar { get; set; }

    [JsonPropertyName("followers_count")]
    public int FollowersCount { get; set; }

    [JsonPropertyName("following_count")]
    public int FollowingCount { get; set; }

    [JsonPropertyName("posts_count")]
    public int PostsCount { get; set; }

    [JsonPropertyName("is_following")]
    public bool IsFollowing { get; set; }

    [JsonPropertyName("is_followed_by")]
    public bool IsFollowedBy { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }

    [JsonIgnore]
    public string Handle
    {
        get
        {
            var u = Username ?? "?";
            var d = Domain ?? Instance;
            return string.IsNullOrWhiteSpace(d) ? u : $"{u}@{d}";
        }
    }

    [JsonIgnore]
    public string DisplayTitle => DisplayName ?? Handle;
}

public sealed class FediverseRelationship
{
    [JsonPropertyName("following")]
    public bool Following { get; set; }

    [JsonPropertyName("followed_by")]
    public bool FollowedBy { get; set; }

    [JsonPropertyName("blocking")]
    public bool Blocking { get; set; }

    [JsonPropertyName("muting")]
    public bool Muting { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class FediverseModerationRule
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("domain")]
    public string? Domain { get; set; }

    [JsonPropertyName("pattern")]
    public string? Pattern { get; set; }

    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }

    [JsonIgnore]
    public string Display =>
        $"{(Enabled ? "✓" : "×")} {Domain ?? Pattern ?? Id.ToString("D")[..8]} · {Action ?? "rule"}";
}

// —— Automod ——

public sealed class SnAutomodRule
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("default_action")]
    public int DefaultAction { get; set; }

    [JsonPropertyName("pattern")]
    public string? Pattern { get; set; }

    [JsonPropertyName("is_regex")]
    public bool IsRegex { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("derank_weight")]
    public int DerankWeight { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }

    [JsonIgnore]
    public string Display =>
        $"{Name ?? "规则"} · {(IsRegex ? "regex" : "text")}: {Pattern ?? "—"}";
}

public sealed class AutomodRuleDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("default_action")]
    public int DefaultAction { get; set; } = 1;

    [JsonPropertyName("pattern")]
    public string? Pattern { get; set; }

    [JsonPropertyName("is_regex")]
    public bool IsRegex { get; set; }
}

// —— Ads / Admin ——

public sealed class PublicAdvertisingPostStats
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("post_id")]
    public Guid? PostId { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("impressions")]
    public long Impressions { get; set; }

    [JsonPropertyName("clicks")]
    public long Clicks { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }

    [JsonIgnore]
    public string Display =>
        $"{Title ?? Name ?? PostId?.ToString("D")[..8] ?? Id.ToString("D")[..8]} · 曝光 {Impressions} / 点击 {Clicks}";
}

public sealed class SphereAdminStats
{
    [JsonPropertyName("posts")]
    public long Posts { get; set; }

    [JsonPropertyName("publishers")]
    public long Publishers { get; set; }

    [JsonPropertyName("users")]
    public long Users { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }

    [JsonIgnore]
    public string DisplaySummary
    {
        get
        {
            var parts = new List<string>();
            if (Posts > 0) parts.Add($"帖子 {Posts}");
            if (Publishers > 0) parts.Add($"发布者 {Publishers}");
            if (Users > 0) parts.Add($"用户 {Users}");
            if (ExtensionData is { Count: > 0 })
            {
                foreach (var kv in ExtensionData.Take(8))
                {
                    parts.Add($"{kv.Key}={kv.Value}");
                }
            }

            return parts.Count == 0 ? "（无统计字段或无权限）" : string.Join(" · ", parts);
        }
    }
}

public sealed class ActivityPubSearchResult
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("preferredUsername")]
    public string? PreferredUsername { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }

    [JsonIgnore]
    public string Display => Name ?? PreferredUsername ?? Id ?? Type ?? "AP object";
}
