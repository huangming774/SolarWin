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
