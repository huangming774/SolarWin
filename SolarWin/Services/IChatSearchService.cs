using System.Threading;
using System.Threading.Tasks;
using SolarWin.Models;

namespace SolarWin.Services;

/// <summary>
/// Orchestrates a chat search end-to-end: validates the query, decides between the FTS5 path
/// (≥3 chars) and the LIKE fallback (1–2 chars — predominantly 2-character CJK words), escapes
/// user input for the chosen path, builds snippet highlights, and returns a UI-ready page.
/// All non-DB logic lives here so it can be unit-tested without touching SQLite.
/// </summary>
public interface IChatSearchService
{
    Task<ChatSearchPage> SearchAsync(ChatSearchRequest request, CancellationToken cancellationToken);
}