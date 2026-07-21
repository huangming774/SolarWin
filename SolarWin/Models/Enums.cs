namespace SolarWin.Models;

/// <summary>
/// Padlock client platform (OpenAPI ClientPlatform).
/// Node 2: Windows desktop clients send platform = 5.
/// </summary>
public enum ClientPlatform
{
    Unspecified = 0,
    Web = 1,
    Android = 2,
    Ios = 3,
    MacOs = 4,
    Windows = 5,
    Linux = 6,
}

/// <summary>Account contact channel (OpenAPI AccountContactType).</summary>
public enum AccountContactType
{
    Email = 0,
    Phone = 1,
    Im = 2,
}

/// <summary>Auth factor type (OpenAPI AccountAuthFactorType).</summary>
public enum AccountAuthFactorType
{
    Password = 0,
    Email = 1,
    PhoneCode = 2,
    Totp = 3,
    Passkey = 4,
    InApp = 5,
    Recovery = 6,
    Oidc = 7,
    Other = 8,
}

/// <summary>Profile board item kind (OpenAPI SnAccountBoardItemKind).</summary>
public enum SnAccountBoardItemKind
{
    BuiltIn = 0,
    CustomApp = 1,
}

/// <summary>Chat room type (OpenAPI ChatRoomType).</summary>
public enum ChatRoomType
{
    Direct = 0,
    Group = 1,
}

/// <summary>Chat room encryption mode (OpenAPI ChatRoomEncryptionMode).</summary>
public enum ChatRoomEncryptionMode
{
    None = 0,
    Mls = 3,
}

/// <summary>
/// Presence attitude (OpenAPI StatusAttitude).
/// Node 1 mapping: Online=0, Idle=1, DoNotDisturb=2.
/// </summary>
public enum StatusAttitude
{
    Online = 0,
    Idle = 1,
    DoNotDisturb = 2,
}

/// <summary>
/// Status content type (OpenAPI StatusType).
/// Node 1 mapping: None=0, Custom=1, …
/// </summary>
public enum StatusType
{
    None = 0,
    Custom = 1,
    Activity = 2,
    Media = 3,
}

/// <summary>
/// Relationship status (OpenAPI RelationshipStatus).
/// None=0, Friend=100, Pending=200, Blocked=-100; API also defines -50.
/// </summary>
public enum RelationshipStatus
{
    None = 0,
    Friend = 100,
    Pending = 200,
    Blocked = -100,
    Restricted = -50,
}

/// <summary>Check-in result tier (OpenAPI CheckInResultLevel).</summary>
public enum CheckInResultLevel
{
    Level0 = 0,
    Level1 = 1,
    Level2 = 2,
    Level3 = 3,
    Level4 = 4,
    Level5 = 5,
}

/// <summary>Post visibility for social feed posts.</summary>
public enum PostVisibility
{
    Public = 0,
    Friends = 1,
    Private = 2,
    Unlisted = 3,
}

/// <summary>Subscription lifecycle (OpenAPI SubscriptionStatus).</summary>
public enum SubscriptionStatus
{
    Inactive = 0,
    Active = 1,
    Expired = 2,
    Pending = 3,
}

/// <summary>Verification mark type (OpenAPI VerificationMarkType).</summary>
public enum VerificationMarkType
{
    None = 0,
    Official = 1,
    Organization = 2,
    Individual = 3,
    Government = 4,
    Media = 5,
    Other = 6,
}

/// <summary>Chat member notify preference (OpenAPI ChatMemberNotify).</summary>
public enum ChatMemberNotify
{
    All = 0,
    Mentions = 1,
    None = 2,
}

/// <summary>Message reaction attitude (OpenAPI MessageReactionAttitude).</summary>
public enum MessageReactionAttitude
{
    Neutral = 0,
    Positive = 1,
    Negative = 2,
}

/// <summary>
/// Post reaction attitude (server / Solian client).
/// Positive=0, Neutral=1, Negative=2 — not the same as MessageReactionAttitude.
/// </summary>
public enum PostReactionAttitude
{
    Positive = 0,
    Neutral = 1,
    Negative = 2,
}

/// <summary>Support ticket type (OpenAPI TicketType).</summary>
public enum TicketType
{
    General = 0,
    Bug = 1,
    Feature = 2,
    Account = 3,
    Other = 4,
}

/// <summary>Support ticket status (OpenAPI TicketStatus).</summary>
public enum TicketStatus
{
    Open = 0,
    InProgress = 1,
    Waiting = 2,
    Resolved = 3,
    Closed = 4,
    Cancelled = 5,
}

/// <summary>Support ticket priority (OpenAPI TicketPriority).</summary>
public enum TicketPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Urgent = 3,
}

/// <summary>Location visibility for pins / meets (OpenAPI LocationVisibility).</summary>
public enum LocationVisibility
{
    Private = 0,
    Friends = 1,
    Public = 2,
}

/// <summary>Meet lifecycle (OpenAPI MeetStatus).</summary>
public enum MeetStatus
{
    Active = 0,
    Completed = 1,
    Expired = 2,
    Cancelled = 3,
}

/// <summary>Location pin status (OpenAPI LocationPinStatus).</summary>
public enum LocationPinStatus
{
    Active = 0,
    Expired = 1,
    Disconnected = 2,
}

/// <summary>Social credit record status (OpenAPI SocialCreditRecordStatus).</summary>
public enum SocialCreditRecordStatus
{
    Active = 0,
    Expired = 1,
    Revoked = 2,
}

/// <summary>Publisher kind (OpenAPI PublisherType). 0=Individual, 1=Organization.</summary>
public enum PublisherType
{
    Individual = 0,
    Organization = 1,
}

/// <summary>
/// Publisher team role (OpenAPI PublisherMemberRole).
/// Values: 25 / 50 / 75 / 100.
/// </summary>
public enum PublisherMemberRole
{
    Member = 25,
    Editor = 50,
    Admin = 75,
    Owner = 100,
}
