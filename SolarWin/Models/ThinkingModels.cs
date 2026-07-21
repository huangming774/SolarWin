using System.Text.Json.Serialization;

namespace SolarWin.Models;

// —— Personality / Thoughts (Solian 寻思) ——

/// <summary>GET /personality/agents item.</summary>
public sealed class ThoughtAgent
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>Default / only model id exposed by some deployments.</summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("available_models")]
    public List<ThoughtAgentModel>? AvailableModels { get; set; }

    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Id : Name!;

    [JsonIgnore]
    public IReadOnlyList<ThoughtAgentModel> Models
    {
        get
        {
            if (AvailableModels is { Count: > 0 })
            {
                return AvailableModels;
            }

            if (!string.IsNullOrWhiteSpace(Model))
            {
                return
                [
                    new ThoughtAgentModel
                    {
                        Id = Model!,
                        DisplayName = Model!,
                        IsDefault = true,
                    },
                ];
            }

            return [];
        }
    }
}

public sealed class ThoughtAgentModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("min_perk_level")]
    public int MinPerkLevel { get; set; }

    [JsonPropertyName("is_default")]
    public bool IsDefault { get; set; }

    [JsonIgnore]
    public string Label => string.IsNullOrWhiteSpace(DisplayName) ? Id : DisplayName!;
}

/// <summary>POST /personality/conversations body.</summary>
public sealed class CreateThoughtConversationRequest
{
    [JsonPropertyName("agent_id")]
    public string AgentId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string? Title { get; set; }
}

/// <summary>Conversation / sequence (thread).</summary>
public sealed class ThoughtConversation
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("account_id")]
    public string? AccountId { get; set; }

    [JsonPropertyName("agent_id")]
    public string? AgentId { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonIgnore]
    public string DisplayTitle =>
        string.IsNullOrWhiteSpace(Title) ? "新对话" : Title!;
}

/// <summary>Message in a conversation.</summary>
public sealed class ThoughtMessage
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("thread_id")]
    public string? ThreadId { get; set; }

    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("sequence")]
    public int Sequence { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonIgnore]
    public bool IsUser =>
        string.Equals(Role, "user", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsAssistant =>
        string.Equals(Role, "assistant", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Role, "system", StringComparison.OrdinalIgnoreCase);
}

/// <summary>POST /personality/conversations/{id}/messages</summary>
public sealed class AddThoughtMessageRequest
{
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

/// <summary>POST /personality/conversations/{id}/runs body.</summary>
public sealed class ThoughtRunRequest
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }
}

/// <summary>Non-stream run response (best-effort shape).</summary>
public sealed class ThoughtRunResponse
{
    [JsonPropertyName("thread")]
    public ThoughtConversation? Thread { get; set; }

    [JsonPropertyName("run")]
    public ThoughtRunInfo? Run { get; set; }

    [JsonPropertyName("request_message")]
    public ThoughtMessage? RequestMessage { get; set; }

    [JsonPropertyName("response_message")]
    public ThoughtMessage? ResponseMessage { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

public sealed class ThoughtRunInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }
}

/// <summary>Normalized run outcome for UI (stream or JSON).</summary>
public sealed class ThoughtRunResult
{
    public string Content { get; init; } = string.Empty;

    public string? Model { get; init; }

    public string? ConversationId { get; init; }

    public string? ConversationTitle { get; init; }

    public bool UsedStream { get; init; }
}
