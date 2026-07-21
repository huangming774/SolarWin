namespace SolarWin.Models;

/// <summary>Navigate to another user's profile page.</summary>
public sealed class UserProfileNavArgs
{
    public UserProfileNavArgs(string name, Guid? accountId = null, string? displayName = null)
    {
        Name = name?.Trim() ?? string.Empty;
        AccountId = accountId;
        DisplayName = displayName;
    }

    /// <summary>Passport account name (handle without @).</summary>
    public string Name { get; }

    public Guid? AccountId { get; }

    public string? DisplayName { get; }
}

/// <summary>Navigate to a Realm detail page.</summary>
public sealed class RealmDetailNavArgs
{
    public RealmDetailNavArgs(string slug, string? displayName = null)
    {
        Slug = slug?.Trim() ?? string.Empty;
        DisplayName = displayName;
    }

    public string Slug { get; }

    public string? DisplayName { get; }
}

/// <summary>
/// Open a direct chat after navigating to chat (or open detail immediately when room is ready).
/// </summary>
public sealed class DirectChatNavArgs
{
    public DirectChatNavArgs(Guid accountId, string? displayName = null, string? accountName = null)
    {
        AccountId = accountId;
        DisplayName = displayName;
        AccountName = accountName;
    }

    public Guid AccountId { get; }

    public string? DisplayName { get; }

    public string? AccountName { get; }
}

/// <summary>Navigate to publisher detail page.</summary>
public sealed class PublisherNavArgs
{
    public PublisherNavArgs(string name, string? displayName = null)
    {
        Name = name?.Trim().TrimStart('@') ?? string.Empty;
        DisplayName = displayName;
    }

    public string Name { get; }

    public string? DisplayName { get; }
}

/// <summary>Filtered post feed: tag / category / collection / publisher.</summary>
public enum PostFeedKind
{
    Tag,
    Category,
    Collection,
    Publisher,
}

/// <summary>Navigate to PostFeedPage.</summary>
public sealed class PostFeedNavArgs
{
    public PostFeedNavArgs(
        PostFeedKind kind,
        string key,
        string? title = null,
        string? publisherName = null)
    {
        Kind = kind;
        Key = key?.Trim() ?? string.Empty;
        Title = title;
        PublisherName = publisherName?.Trim();
    }

    public PostFeedKind Kind { get; }

    /// <summary>Tag/category/collection slug, or publisher name.</summary>
    public string Key { get; }

    public string? Title { get; }

    /// <summary>Required for collection feeds.</summary>
    public string? PublisherName { get; }
}
