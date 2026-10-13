using System;

namespace SolarWin.Models;

/// <summary>
/// One hit row from a chat full-text search, returned from the repository layer.
/// Carries enough fields for the service to render a result item (snippet, sender name,
/// room name) and to construct the jump-to-message navigation arguments.
/// </summary>
public sealed record ChatSearchRow(
    Guid MessageId,
    Guid RoomId,
    long RoomSequence,
    DateTimeOffset? CreatedAt,
    Guid SenderId,
    string? SenderName,
    string? RoomName,
    string Content);

/// <summary>
/// Source of a hit — used by callers that want to surface the routing decision
/// (e.g., a subtle UI hint that short queries went via the LIKE fallback path).
/// </summary>
public enum ChatSearchSource
{
    Fts = 0,
    Like = 1,
}

/// <summary>
/// Result of a search, scoped to the caller's filter. <see cref="Rows"/> is the visible
/// page; <see cref="TotalApproximate"/> is a best-effort count for "showing X of Y" UI
/// (currently a separate COUNT query; flagged approximate because the underlying table
/// can change between the two reads).
/// </summary>
public sealed record ChatSearchPage(
    IReadOnlyList<ChatSearchResult> Rows,
    int TotalApproximate,
    ChatSearchSource Source,
    string Query,
    Guid? RoomIdFilter);

/// <summary>
/// UI-ready search result. <see cref="Snippet"/> is a sequence of alternating normal and
/// highlighted segments so the renderer can bold the matches without any text post-processing.
/// </summary>
public sealed record ChatSearchResult(
    ChatSearchRow Row,
    IReadOnlyList<SnippetSegment> Snippet,
    string HighlightedQuery);

/// <summary>
/// One piece of a snippet: either plain text (<see cref="IsMatch"/> false) or the matched
/// query (<see cref="IsMatch"/> true). The renderer switches between two <c>Run</c> styles
/// based on <see cref="IsMatch"/>.
/// </summary>
public readonly record struct SnippetSegment(string Text, bool IsMatch);

/// <summary>
/// Caller-supplied filter for a single search invocation. Mirrors
/// <c>ChatAnalyticsRepository.BuildWhereClause</c> conventions but is intentionally smaller
/// (room only) — time range and sender filter are deferred to v2.
/// </summary>
public sealed record ChatSearchFilter(
    Guid? RoomId = null,
    int Take = 100,
    int Offset = 0);

/// <summary>
/// Aggregate request passed to <c>IChatSearchService.SearchAsync</c>.
/// </summary>
public sealed record ChatSearchRequest(
    string Query,
    ChatSearchFilter Filter);