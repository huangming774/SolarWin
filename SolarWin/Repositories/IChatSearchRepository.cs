using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SolarWin.Models;

namespace SolarWin.Repositories;

/// <summary>
/// Raw-SQL search over the per-account <c>chat_messages</c> + <c>chat_messages_fts</c> tables.
/// Two entry points correspond to the routing rule in <c>IChatSearchService</c>:
/// the FTS5 trigram MATCH path for ≥3-character queries and the LIKE fallback for 1–2 character
/// (predominantly 2-character CJK) queries. The service chooses; the repository stays oblivious.
/// </summary>
public interface IChatSearchRepository
{
    /// <summary>
    /// FTS5 MATCH path. <paramref name="phrase"/> must already be wrapped as a quoted phrase
    /// (e.g. <c>"用户原始查询"</c> with internal <c>"</c> doubled) and any FTS5 operators should
    /// already be inert — phrase-quoting neutralises them.
    /// </summary>
    Task<IReadOnlyList<ChatSearchRow>> SearchByFtsAsync(
        string phrase,
        Guid? roomId,
        int take,
        int offset,
        CancellationToken cancellationToken);

    /// <summary>
    /// LIKE fallback path. <paramref name="likePattern"/> must already be escaped for
    /// <c>%</c>/<c>_</c>/<c>\</c> and wrapped with <c>%...%</c>; SQL is invoked with
    /// <c>ESCAPE '\\'</c>.
    /// </summary>
    Task<IReadOnlyList<ChatSearchRow>> SearchByLikeAsync(
        string likePattern,
        Guid? roomId,
        int take,
        int offset,
        CancellationToken cancellationToken);
}