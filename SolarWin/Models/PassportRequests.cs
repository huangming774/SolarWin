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

/// <summary>POST/PATCH /passport/relationships/{accountId} (OpenAPI RelationshipRequest).</summary>
public sealed class RelationshipRequest
{
    [JsonPropertyName("status")]
    public RelationshipStatus Status { get; set; }
}

/// <summary>POST block/mute (OpenAPI RelationshipActionRequest).</summary>
public sealed class RelationshipActionRequest
{
    /// <summary>Duration string, e.g. "7d" / "24h". Null = permanent.</summary>
    [JsonPropertyName("expires_in")]
    public string? ExpiresIn { get; set; }

    [JsonPropertyName("degrade_to")]
    public RelationshipStatus? DegradeTo { get; set; }
}

/// <summary>PATCH /passport/relationships/{accountId}/alias.</summary>
public sealed class AliasRequest
{
    [JsonPropertyName("alias")]
    public string? Alias { get; set; }
}

/// <summary>POST /passport/realms (OpenAPI RealmRequest).</summary>
public sealed class RealmRequest
{
    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("picture_id")]
    public string? PictureId { get; set; }

    [JsonPropertyName("background_id")]
    public string? BackgroundId { get; set; }

    [JsonPropertyName("is_community")]
    public bool IsCommunity { get; set; }

    [JsonPropertyName("is_public")]
    public bool IsPublic { get; set; } = true;
}

/// <summary>POST /passport/realms/invites/{slug} (OpenAPI RealmMemberRequest).</summary>
public sealed class RealmMemberRequest
{
    [JsonPropertyName("related_user_id")]
    public Guid RelatedUserId { get; set; }

    [JsonPropertyName("role")]
    public int Role { get; set; }
}

/// <summary>POST /passport/accounts/me/calendar/events.</summary>
public sealed class CreateCalendarEventRequest
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("start_time")]
    public DateTimeOffset? StartTime { get; set; }

    [JsonPropertyName("end_time")]
    public DateTimeOffset? EndTime { get; set; }

    [JsonPropertyName("is_all_day")]
    public bool IsAllDay { get; set; }

    [JsonPropertyName("visibility")]
    public int Visibility { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }
}

/// <summary>POST /passport/tickets (OpenAPI CreateTicketRequest).</summary>
public sealed class CreateTicketRequest
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("type")]
    public TicketType Type { get; set; }

    [JsonPropertyName("priority")]
    public TicketPriority Priority { get; set; } = TicketPriority.Normal;

    [JsonPropertyName("file_ids")]
    public List<string>? FileIds { get; set; }

    [JsonPropertyName("resources")]
    public List<string>? Resources { get; set; }
}

/// <summary>POST /passport/tickets/{id}/messages.</summary>
public sealed class AddTicketMessageRequest
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("file_ids")]
    public List<string>? FileIds { get; set; }
}

/// <summary>POST /passport/pins.</summary>
public sealed class CreatePinRequest
{
    [JsonPropertyName("visibility")]
    public LocationVisibility Visibility { get; set; } = LocationVisibility.Friends;

    [JsonPropertyName("location_name")]
    public string? LocationName { get; set; }

    [JsonPropertyName("location_address")]
    public string? LocationAddress { get; set; }

    [JsonPropertyName("location_wkt")]
    public string? LocationWkt { get; set; }

    [JsonPropertyName("keep_on_disconnect")]
    public bool KeepOnDisconnect { get; set; }
}

/// <summary>POST /passport/meets.</summary>
public sealed class CreateMeetRequest
{
    [JsonPropertyName("visibility")]
    public LocationVisibility Visibility { get; set; } = LocationVisibility.Friends;

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("location_name")]
    public string? LocationName { get; set; }

    [JsonPropertyName("location_address")]
    public string? LocationAddress { get; set; }

    [JsonPropertyName("location_wkt")]
    public string? LocationWkt { get; set; }

    [JsonPropertyName("expires_in_seconds")]
    public int? ExpiresInSeconds { get; set; }
}

/// <summary>POST /passport/nfc/tags/claim.</summary>
public sealed class ClaimTagRequest
{
    [JsonPropertyName("record_id")]
    public string? RecordId { get; set; }

    [JsonPropertyName("uid")]
    public string? Uid { get; set; }
}

