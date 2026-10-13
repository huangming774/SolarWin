using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SolarWin.Models;
using SolarWin.Repositories;

namespace SolarWin.Services;

/// <summary>
/// See <see cref="IChatSearchService"/>. Pure helpers (<see cref="BuildFtsPhrase"/>,
/// <see cref="BuildLikePattern"/>, <see cref="BuildSnippet"/>) are <c>internal static</c> and
/// directly unit-testable.
/// </summary>
public sealed class ChatSearchService : IChatSearchService
{
    /// <summary>FTS5 trigram MATCH requires ≥3 characters; below that we fall back to LIKE.</summary>
    internal const int FtsMinChars = 3;

    /// <summary>Snippet window on either side of the first match (in characters).</summary>
    internal const int SnippetPadding = 24;

    private readonly IChatSearchRepository _repository;

    public ChatSearchService(IChatSearchRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    public async Task<ChatSearchPage> SearchAsync(
        ChatSearchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = (request.Query ?? string.Empty).Trim();
        if (query.Length == 0)
        {
            return new ChatSearchPage(
                Rows: Array.Empty<ChatSearchResult>(),
                TotalApproximate: 0,
                Source: ChatSearchSource.Fts,
                Query: string.Empty,
                RoomIdFilter: request.Filter?.RoomId);
        }

        var take = Math.Max(1, request.Filter?.Take ?? 100);
        var offset = Math.Max(0, request.Filter?.Offset ?? 0);

        ChatSearchSource source;
        IReadOnlyList<ChatSearchRow> rows;
        // SQLite trigram counts Unicode scalars, not UTF-16 code units.
        if (query.EnumerateRunes().Count() >= FtsMinChars)
        {
            source = ChatSearchSource.Fts;
            rows = await _repository
                .SearchByFtsAsync(BuildFtsPhrase(query), request.Filter?.RoomId, take, offset, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            source = ChatSearchSource.Like;
            rows = await _repository
                .SearchByLikeAsync(BuildLikePattern(query), request.Filter?.RoomId, take, offset, cancellationToken)
                .ConfigureAwait(false);
        }

        var results = new List<ChatSearchResult>(rows.Count);
        foreach (var row in rows)
        {
            results.Add(new ChatSearchResult(
                Row: row,
                Snippet: BuildSnippet(row.Content, query, SnippetPadding),
                HighlightedQuery: query));
        }

        return new ChatSearchPage(
            Rows: results,
            TotalApproximate: results.Count, // v1: no separate count query
            Source: source,
            Query: query,
            RoomIdFilter: request.Filter?.RoomId);
    }

    /// <summary>
    /// Wrap the user query as an FTS5 phrase so all syntax characters (including <c>*</c>,
    /// <c>^</c>, <c>AND</c>, <c>OR</c>, <c>NOT</c>, <c>NEAR</c>) are treated as literals.
    /// With the trigram tokenizer, a phrase match equals substring match — exactly the chat
    /// search semantic we want.
    /// </summary>
    internal static string BuildFtsPhrase(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        return "\"" + raw.Replace("\"", "\"\"") + "\"";
    }

    /// <summary>
    /// Escape LIKE special characters (<c>%</c>, <c>_</c>, <c>\</c>) and wrap with
    /// <c>%...%</c> so the pattern matches the query as a literal substring anywhere in
    /// <c>Content</c>. The SQL is invoked with <c>ESCAPE '\'</c>.
    /// </summary>
    internal static string BuildLikePattern(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        var escaped = raw
            .Replace(@"\", @"\\")
            .Replace("%", @"\%")
            .Replace("_", @"\_");
        return "%" + escaped + "%";
    }

    /// <summary>
    /// Build the snippet segments: case-insensitive first-match window. If the query appears
    /// multiple times, only the first occurrence is highlighted (matching typical chat-search
    /// expectations). If the query isn't found verbatim (e.g. LIKE wildcard surprises), the
    /// whole content is returned as a single non-highlighted segment truncated to
    /// <paramref name="padding"/>×2 characters.
    /// </summary>
    internal static IReadOnlyList<SnippetSegment> BuildSnippet(
        string content,
        string query,
        int padding = SnippetPadding)
    {
        if (string.IsNullOrEmpty(content) || string.IsNullOrEmpty(query))
        {
            return Array.Empty<SnippetSegment>();
        }

        var comparison = StringComparison.OrdinalIgnoreCase;
        var matchIndex = content.IndexOf(query, comparison);
        if (matchIndex < 0)
        {
            // Rare for LIKE path (LIKE matched a wildcard pattern not present as substring) and
            // impossible for FTS path. Fall back to a head-only preview so the UI still shows
            // something instead of nothing.
            var preview = content.Length > padding * 2
                ? content.Substring(0, padding * 2) + "…"
                : content;
            return new[] { new SnippetSegment(preview, IsMatch: false) };
        }

        var start = Math.Max(0, matchIndex - padding);
        var end = Math.Min(content.Length, matchIndex + query.Length + padding);

        var prefix = start > 0 ? "…" + content.Substring(start, matchIndex - start) : content.Substring(0, matchIndex);
        var match = content.Substring(matchIndex, query.Length);
        var suffix = end < content.Length ? content.Substring(matchIndex + query.Length, end - matchIndex - query.Length) + "…" : content.Substring(matchIndex + query.Length);

        // Compact: drop empty plain segments (common at start or end).
        var segments = new List<SnippetSegment>(3);
        if (prefix.Length > 0) segments.Add(new SnippetSegment(prefix, IsMatch: false));
        segments.Add(new SnippetSegment(match, IsMatch: true));
        if (suffix.Length > 0) segments.Add(new SnippetSegment(suffix, IsMatch: false));
        return segments;
    }
}
