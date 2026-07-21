using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolarWin.Models;

/// <summary>GET /passport/accounts/me/leveling item (OpenAPI SnExperienceRecord).</summary>
public sealed class SnExperienceRecord
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("reason_type")]
    public string? ReasonType { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("delta")]
    public int Delta { get; set; }

    [JsonPropertyName("bonus_multiplier")]
    public double BonusMultiplier { get; set; }

    [JsonPropertyName("account_id")]
    public Guid AccountId { get; set; }
}

/// <summary>GET /passport/accounts/me/credits/history item (OpenAPI SnSocialCreditRecord).</summary>
public sealed class SnSocialCreditRecord
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("reason_type")]
    public string? ReasonType { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("delta")]
    public double Delta { get; set; }

    [JsonPropertyName("status")]
    public SocialCreditRecordStatus Status { get; set; }

    [JsonPropertyName("expired_at")]
    public DateTimeOffset? ExpiredAt { get; set; }

    [JsonPropertyName("account_id")]
    public Guid AccountId { get; set; }
}

/// <summary>GET /passport/accounts/me/actions item (OpenAPI SnActionLog).</summary>
public sealed class SnActionLog
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("meta")]
    public Dictionary<string, JsonElement>? Meta { get; set; }

    [JsonPropertyName("user_agent")]
    public string? UserAgent { get; set; }

    [JsonPropertyName("ip_address")]
    public string? IpAddress { get; set; }

    [JsonPropertyName("account_id")]
    public Guid AccountId { get; set; }

    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }
}

/// <summary>Calendar event (OpenAPI SnUserCalendarEvent / UserCalendarEventDto).</summary>
public sealed class SnUserCalendarEvent
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

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

    [JsonPropertyName("meta")]
    public Dictionary<string, JsonElement>? Meta { get; set; }

    [JsonPropertyName("account_id")]
    public Guid AccountId { get; set; }
}

/// <summary>GET /passport/accounts/me/calendar (OpenAPI DailyEventResponse).</summary>
public sealed class DailyEventResponse
{
    [JsonPropertyName("date")]
    public DateTimeOffset? Date { get; set; }

    [JsonPropertyName("check_in_result")]
    public SnCheckInResult? CheckInResult { get; set; }

    [JsonPropertyName("statuses")]
    public List<SnAccountStatus>? Statuses { get; set; }

    [JsonPropertyName("user_events")]
    public List<SnUserCalendarEvent>? UserEvents { get; set; }

    [JsonPropertyName("notable_days")]
    public List<NotableDay>? NotableDays { get; set; }
}

/// <summary>OpenAPI NotableDay.</summary>
public sealed class NotableDay
{
    [JsonPropertyName("local_name")]
    public string? LocalName { get; set; }

    [JsonPropertyName("global_name")]
    public string? GlobalName { get; set; }

    [JsonPropertyName("localizable_key")]
    public string? LocalizableKey { get; set; }

    [JsonPropertyName("country_code")]
    public string? CountryCode { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("occurrence_key")]
    public string? OccurrenceKey { get; set; }

    [JsonPropertyName("date")]
    public DateTimeOffset? Date { get; set; }
}

/// <summary>GET /passport/accounts/me/calendar/countdown item.</summary>
public sealed class EventCountdownItem
{
    [JsonPropertyName("event_id")]
    public Guid? EventId { get; set; }

    [JsonPropertyName("event_type")]
    public string? EventType { get; set; }

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

    [JsonPropertyName("days_remaining")]
    public int DaysRemaining { get; set; }

    [JsonPropertyName("hours_remaining")]
    public int HoursRemaining { get; set; }

    [JsonPropertyName("is_ongoing")]
    public bool IsOngoing { get; set; }

    [JsonPropertyName("account_id")]
    public Guid? AccountId { get; set; }
}

/// <summary>Progression achievement (OpenAPI ProgressionAchievementState).</summary>
public sealed class ProgressionAchievementState
{
    [JsonPropertyName("identifier")]
    public string? Identifier { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("series_identifier")]
    public string? SeriesIdentifier { get; set; }

    [JsonPropertyName("series_title")]
    public string? SeriesTitle { get; set; }

    [JsonPropertyName("series_order")]
    public int SeriesOrder { get; set; }

    [JsonPropertyName("sort_order")]
    public int SortOrder { get; set; }

    [JsonPropertyName("hidden")]
    public bool Hidden { get; set; }

    [JsonPropertyName("is_enabled")]
    public bool IsEnabled { get; set; }

    [JsonPropertyName("is_progress_enabled")]
    public bool IsProgressEnabled { get; set; }

    [JsonPropertyName("is_currently_available")]
    public bool IsCurrentlyAvailable { get; set; }

    [JsonPropertyName("target_count")]
    public int TargetCount { get; set; }

    [JsonPropertyName("progress_count")]
    public int ProgressCount { get; set; }

    [JsonPropertyName("current_streak")]
    public int CurrentStreak { get; set; }

    [JsonPropertyName("best_streak")]
    public int BestStreak { get; set; }

    [JsonPropertyName("is_completed")]
    public bool IsCompleted { get; set; }

    [JsonPropertyName("completed_at")]
    public DateTimeOffset? CompletedAt { get; set; }
}

/// <summary>OpenAPI ProgressionAchievementStats.</summary>
public sealed class ProgressionAchievementStats
{
    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; }

    [JsonPropertyName("completed_count")]
    public int CompletedCount { get; set; }

    [JsonPropertyName("hidden_total_count")]
    public int HiddenTotalCount { get; set; }

    [JsonPropertyName("hidden_completed_count")]
    public int HiddenCompletedCount { get; set; }

    [JsonPropertyName("completion_percentage")]
    public double CompletionPercentage { get; set; }
}

/// <summary>Progression quest (OpenAPI ProgressionQuestState).</summary>
public sealed class ProgressionQuestState
{
    [JsonPropertyName("identifier")]
    public string? Identifier { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("target_count")]
    public int TargetCount { get; set; }

    [JsonPropertyName("progress_count")]
    public int ProgressCount { get; set; }

    [JsonPropertyName("is_completed")]
    public bool IsCompleted { get; set; }

    [JsonPropertyName("completed_at")]
    public DateTimeOffset? CompletedAt { get; set; }

    [JsonPropertyName("period_key")]
    public string? PeriodKey { get; set; }

    [JsonPropertyName("next_reset_at")]
    public DateTimeOffset? NextResetAt { get; set; }

    [JsonPropertyName("is_currently_available")]
    public bool IsCurrentlyAvailable { get; set; }
}

/// <summary>OpenAPI SnProgressRewardGrant.</summary>
public sealed class SnProgressRewardGrant
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("account_id")]
    public Guid AccountId { get; set; }

    [JsonPropertyName("definition_type")]
    public string? DefinitionType { get; set; }

    [JsonPropertyName("definition_identifier")]
    public string? DefinitionIdentifier { get; set; }

    [JsonPropertyName("definition_title")]
    public string? DefinitionTitle { get; set; }

    [JsonPropertyName("reward_token")]
    public string? RewardToken { get; set; }

    [JsonPropertyName("period_key")]
    public string? PeriodKey { get; set; }
}

/// <summary>Realm member (OpenAPI SnRealmMember).</summary>
public sealed class SnRealmMember
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("realm_id")]
    public Guid RealmId { get; set; }

    [JsonPropertyName("realm")]
    public SnRealm? Realm { get; set; }

    [JsonPropertyName("account_id")]
    public Guid AccountId { get; set; }

    [JsonPropertyName("account")]
    public SnAccount? Account { get; set; }

    [JsonPropertyName("status")]
    public SnAccountStatus? Status { get; set; }

    [JsonPropertyName("nick")]
    public string? Nick { get; set; }

    [JsonPropertyName("bio")]
    public string? Bio { get; set; }

    [JsonPropertyName("label_id")]
    public Guid? LabelId { get; set; }

    [JsonPropertyName("label")]
    public SnRealmLabel? Label { get; set; }

    [JsonPropertyName("experience")]
    public int Experience { get; set; }

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("leveling_progress")]
    public double LevelingProgress { get; set; }

    [JsonPropertyName("role")]
    public int Role { get; set; }

    [JsonPropertyName("joined_at")]
    public DateTimeOffset? JoinedAt { get; set; }

    [JsonPropertyName("leave_at")]
    public DateTimeOffset? LeaveAt { get; set; }
}

/// <summary>Role permissions for a realm (OpenAPI SnRealmRolePermission).</summary>
public sealed class SnRealmRolePermission
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("realm_id")]
    public Guid RealmId { get; set; }

    [JsonPropertyName("role_level")]
    public int RoleLevel { get; set; }

    [JsonPropertyName("can_chat")]
    public bool CanChat { get; set; }

    [JsonPropertyName("can_post")]
    public bool CanPost { get; set; }

    [JsonPropertyName("can_comment")]
    public bool CanComment { get; set; }

    [JsonPropertyName("can_upload_media")]
    public bool CanUploadMedia { get; set; }

    [JsonPropertyName("can_moderate_posts")]
    public bool CanModeratePosts { get; set; }

    [JsonPropertyName("can_moderate_chat")]
    public bool CanModerateChat { get; set; }

    [JsonPropertyName("can_manage_members")]
    public bool CanManageMembers { get; set; }

    [JsonPropertyName("can_manage_realm")]
    public bool CanManageRealm { get; set; }
}

/// <summary>GET /passport/relationships/inspect/{accountId}.</summary>
public sealed class InspectRelationshipResponse
{
    [JsonPropertyName("friends")]
    public List<SnAccount>? Friends { get; set; }

    [JsonPropertyName("blocked")]
    public List<SnAccount>? Blocked { get; set; }

    [JsonPropertyName("muted")]
    public List<SnAccount>? Muted { get; set; }

    [JsonPropertyName("pending")]
    public List<SnAccount>? Pending { get; set; }

    [JsonPropertyName("close_friends")]
    public List<SnAccount>? CloseFriends { get; set; }
}

/// <summary>GET /passport/friends/overview item.</summary>
public sealed class FriendOverviewItem
{
    [JsonPropertyName("account")]
    public SnAccount? Account { get; set; }

    [JsonPropertyName("status")]
    public SnAccountStatus? Status { get; set; }
}

/// <summary>Public third-party connection on another account.</summary>
public sealed class PublicAccountConnectionResponse
{
    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [JsonPropertyName("provided_identifier")]
    public string? ProvidedIdentifier { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

/// <summary>Support ticket (OpenAPI SnTicket).</summary>
public sealed class SnTicket
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("type")]
    public TicketType Type { get; set; }

    [JsonPropertyName("status")]
    public TicketStatus Status { get; set; }

    [JsonPropertyName("priority")]
    public TicketPriority Priority { get; set; }

    [JsonPropertyName("creator_id")]
    public Guid CreatorId { get; set; }

    [JsonPropertyName("creator")]
    public SnAccount? Creator { get; set; }

    [JsonPropertyName("assignee_id")]
    public Guid? AssigneeId { get; set; }

    [JsonPropertyName("assignee")]
    public SnAccount? Assignee { get; set; }

    [JsonPropertyName("resolved_at")]
    public DateTimeOffset? ResolvedAt { get; set; }

    [JsonPropertyName("messages")]
    public List<SnTicketMessage>? Messages { get; set; }

    [JsonPropertyName("resources")]
    public List<string>? Resources { get; set; }
}

/// <summary>Ticket message (OpenAPI SnTicketMessage).</summary>
public sealed class SnTicketMessage
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("ticket_id")]
    public Guid TicketId { get; set; }

    [JsonPropertyName("sender_id")]
    public Guid SenderId { get; set; }

    [JsonPropertyName("sender")]
    public SnAccount? Sender { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

/// <summary>Location pin (OpenAPI SnLocationPin).</summary>
public sealed class SnLocationPin
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("account_id")]
    public Guid AccountId { get; set; }

    [JsonPropertyName("account")]
    public SnAccount? Account { get; set; }

    [JsonPropertyName("meet_id")]
    public Guid? MeetId { get; set; }

    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("location_name")]
    public string? LocationName { get; set; }

    [JsonPropertyName("location_address")]
    public string? LocationAddress { get; set; }

    [JsonPropertyName("location_wkt")]
    public string? LocationWkt { get; set; }

    [JsonPropertyName("visibility")]
    public LocationVisibility Visibility { get; set; }

    [JsonPropertyName("status")]
    public LocationPinStatus Status { get; set; }

    [JsonPropertyName("last_heartbeat_at")]
    public DateTimeOffset? LastHeartbeatAt { get; set; }

    [JsonPropertyName("expires_at")]
    public DateTimeOffset? ExpiresAt { get; set; }

    [JsonPropertyName("keep_on_disconnect")]
    public bool KeepOnDisconnect { get; set; }

    [JsonPropertyName("resource_identifier")]
    public string? ResourceIdentifier { get; set; }
}

/// <summary>Meet / gathering (OpenAPI SnMeet).</summary>
public sealed class SnMeet
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("host_id")]
    public Guid HostId { get; set; }

    [JsonPropertyName("host")]
    public SnAccount? Host { get; set; }

    [JsonPropertyName("status")]
    public MeetStatus Status { get; set; }

    [JsonPropertyName("visibility")]
    public LocationVisibility Visibility { get; set; }

    [JsonPropertyName("expires_at")]
    public DateTimeOffset? ExpiresAt { get; set; }

    [JsonPropertyName("completed_at")]
    public DateTimeOffset? CompletedAt { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("location_name")]
    public string? LocationName { get; set; }

    [JsonPropertyName("location_address")]
    public string? LocationAddress { get; set; }

    [JsonPropertyName("location_wkt")]
    public string? LocationWkt { get; set; }

    [JsonPropertyName("is_final")]
    public bool IsFinal { get; set; }

    [JsonPropertyName("resource_identifier")]
    public string? ResourceIdentifier { get; set; }
}

/// <summary>NFC resolve (OpenAPI NfcResolveResponse).</summary>
public sealed class NfcResolveResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("account")]
    public SnAccount? Account { get; set; }

    [JsonPropertyName("is_friend")]
    public bool IsFriend { get; set; }

    [JsonPropertyName("is_claimed")]
    public bool IsClaimed { get; set; }

    [JsonPropertyName("actions")]
    public List<string>? Actions { get; set; }
}

/// <summary>NFC tag (OpenAPI NfcTagResponse).</summary>
public sealed class NfcTagResponse
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("uid")]
    public string? Uid { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("is_locked")]
    public bool IsLocked { get; set; }

    [JsonPropertyName("is_encrypted")]
    public bool IsEncrypted { get; set; }

    [JsonPropertyName("user_id")]
    public Guid? UserId { get; set; }

    [JsonPropertyName("last_seen_at")]
    public DateTimeOffset? LastSeenAt { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }
}

/// <summary>GET /passport/realms/quota simplified.</summary>
public sealed class RealmQuotaResponse
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("used")]
    public int Used { get; set; }

    [JsonPropertyName("remaining")]
    public int Remaining { get; set; }

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("perk_level")]
    public int PerkLevel { get; set; }
}
