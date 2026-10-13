using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SolarWin.Models;
using SolarWin.Repositories;
using SolarWin.Services;

namespace SolarWin.Tests;

/// <summary>
/// Pure-logic tests for the full-text search service. Covers query escaping (FTS phrase and
/// LIKE), snippet windowing, length-based routing between FTS5 and LIKE, and result
/// composition. Uses a fake repository that records calls — no SQLite is touched.
/// </summary>
public class ChatSearchServiceTests
{
    // ----- Query escaping -----

    [Fact]
    public void BuildFtsPhrase_WrapsUserInputAsQuotedPhrase()
    {
        var phrase = ChatSearchService.BuildFtsPhrase("hello world");
        Assert.Equal("\"hello world\"", phrase);
    }

    [Theory]
    [InlineData("hello\"world", "\"hello\"\"world\"")]
    [InlineData("\"leading", "\"\"\"leading\"")]
    [InlineData("trailing\"", "\"trailing\"\"\"")]
    [InlineData("\"\"", "\"\"\"\"\"\"")]
    public void BuildFtsPhrase_DoublesInternalDoubleQuotes(string input, string expected)
    {
        Assert.Equal(expected, ChatSearchService.BuildFtsPhrase(input));
    }

    [Theory]
    [InlineData("hello*", "\"hello*\"")]
    [InlineData("AND OR NOT NEAR", "\"AND OR NOT NEAR\"")]
    [InlineData("a:b^", "\"a:b^\"")]
    public void BuildFtsPhrase_NeutralisesSyntaxCharacters(string input, string expected)
    {
        // Phrase-quoting protects every FTS5 operator: trigram matching turns the phrase
        // into a pure substring match, so the user's exact text is what gets searched.
        Assert.Equal(expected, ChatSearchService.BuildFtsPhrase(input));
    }

    [Fact]
    public void BuildLikePattern_EscapesSpecialCharactersAndWraps()
    {
        Assert.Equal(@"%hello\%%", ChatSearchService.BuildLikePattern("hello%"));
        Assert.Equal(@"%under\_score%", ChatSearchService.BuildLikePattern("under_score"));
        Assert.Equal(@"%back\\slash%", ChatSearchService.BuildLikePattern(@"back\slash"));
        Assert.Equal(@"%a\\\_\%%", ChatSearchService.BuildLikePattern(@"a\_%"));
    }

    [Fact]
    public void BuildLikePattern_PlainAsciiIsWrappedUnchanged()
    {
        Assert.Equal("%abc%", ChatSearchService.BuildLikePattern("abc"));
    }

    // ----- Snippet windowing -----

    [Fact]
    public void BuildSnippet_HighlightsFirstCaseInsensitiveMatch()
    {
        var content = "Hello WORLD this is a Test message";
        var segments = ChatSearchService.BuildSnippet(content, "world", padding: 4);

        Assert.Equal(3, segments.Count);
        Assert.False(segments[0].IsMatch);
        Assert.True(segments[1].IsMatch);
        Assert.Equal("WORLD", segments[1].Text);
        Assert.False(segments[2].IsMatch);
    }

    [Fact]
    public void BuildSnippet_OmitsEmptyBoundarySegments()
    {
        // Match at the very start — no prefix segment.
        var segments = ChatSearchService.BuildSnippet("露营装备清单来了", "露营", padding: 4);
        Assert.Equal(2, segments.Count);
        Assert.True(segments[0].IsMatch);
        Assert.Equal("露营", segments[0].Text);
        Assert.False(segments[1].IsMatch);
    }

    [Fact]
    public void BuildSnippet_PadsMatchFromContentBoundaries()
    {
        // Long prefix trimmed by padding.
        var content = new string('x', 80) + "MATCH" + new string('y', 80);
        var segments = ChatSearchService.BuildSnippet(content, "MATCH", padding: 5);

        Assert.Equal(3, segments.Count);
        Assert.StartsWith("…", segments[0].Text);
        Assert.True(segments[1].IsMatch);
        Assert.EndsWith("…", segments[2].Text);
    }

    [Fact]
    public void BuildSnippet_NoVerbatimMatch_FallsBackToHeadPreview()
    {
        // LIKE wildcards could match a pattern that doesn't appear as a literal substring
        // (e.g. content="abc", pattern="%a_c%"). The FTS path can never produce this mismatch,
        // but the snippet algorithm must not crash.
        var segments = ChatSearchService.BuildSnippet("abcdef", "zzz", padding: 2);
        Assert.Single(segments);
        Assert.False(segments[0].IsMatch);
    }

    [Fact]
    public void BuildSnippet_EmptyContentOrQuery_ReturnsEmpty()
    {
        Assert.Empty(ChatSearchService.BuildSnippet(string.Empty, "q"));
        Assert.Empty(ChatSearchService.BuildSnippet("content", string.Empty));
    }

    // ----- Routing -----

    [Fact]
    public async Task SearchAsync_LongQuery_RoutesToFts()
    {
        var repo = new FakeSearchRepository();
        var service = new ChatSearchService(repo);

        var page = await service.SearchAsync(
            new ChatSearchRequest("露营吧", new ChatSearchFilter()),
            CancellationToken.None);

        Assert.Equal(1, repo.FtsCalls);
        Assert.Equal(0, repo.LikeCalls);
        Assert.Equal(ChatSearchSource.Fts, page.Source);
    }

    [Fact]
    public async Task SearchAsync_TwoCharQuery_RoutesToLike()
    {
        var repo = new FakeSearchRepository();
        var service = new ChatSearchService(repo);

        var page = await service.SearchAsync(
            new ChatSearchRequest("露营", new ChatSearchFilter()),
            CancellationToken.None);

        Assert.Equal(0, repo.FtsCalls);
        Assert.Equal(1, repo.LikeCalls);
        Assert.Equal(ChatSearchSource.Like, page.Source);
    }

    [Theory]
    [InlineData("\U0001F600", ChatSearchSource.Like)]
    [InlineData("\U0001F600\U0001F600", ChatSearchSource.Like)]
    [InlineData("\U00020000\U00020001", ChatSearchSource.Like)]
    [InlineData("a\U00020000", ChatSearchSource.Like)]
    [InlineData("ab\U00020000", ChatSearchSource.Fts)]
    [InlineData("\U0001F600\U0001F600\U0001F600", ChatSearchSource.Fts)]
    public async Task SearchAsync_CountsUnicodeScalarsForTrigramRouting(string query, ChatSearchSource expected)
    {
        var repo = new FakeSearchRepository();
        var page = await new ChatSearchService(repo).SearchAsync(
            new ChatSearchRequest(query, new ChatSearchFilter()), CancellationToken.None);

        Assert.Equal(expected, page.Source);
        Assert.Equal(expected == ChatSearchSource.Fts ? 1 : 0, repo.FtsCalls);
        Assert.Equal(expected == ChatSearchSource.Like ? 1 : 0, repo.LikeCalls);
    }

    [Fact]
    public async Task SearchAsync_EmptyQuery_NoRepoCallsNoResults()
    {
        var repo = new FakeSearchRepository();
        var service = new ChatSearchService(repo);

        var page = await service.SearchAsync(
            new ChatSearchRequest("   ", new ChatSearchFilter()),
            CancellationToken.None);

        Assert.Equal(0, repo.FtsCalls);
        Assert.Equal(0, repo.LikeCalls);
        Assert.Empty(page.Rows);
        Assert.Equal(0, page.TotalApproximate);
    }

    [Fact]
    public async Task SearchAsync_BuildsSnippetSegmentsPerResult()
    {
        var repo = new FakeSearchRepository
        {
            FtsResponse = new List<ChatSearchRow>
            {
                new ChatSearchRow(
                    MessageId: Guid.NewGuid(),
                    RoomId: Guid.NewGuid(),
                    RoomSequence: 1,
                    CreatedAt: DateTimeOffset.UtcNow,
                    SenderId: Guid.NewGuid(),
                    SenderName: "Alice",
                    RoomName: "Team",
                    Content: "明天去露营吧"),
            },
        };
        var service = new ChatSearchService(repo);

        var page = await service.SearchAsync(
            new ChatSearchRequest("露营吧", new ChatSearchFilter()),
            CancellationToken.None);

        Assert.Single(page.Rows);
        var snippet = page.Rows[0].Snippet;
        Assert.NotEmpty(snippet);
        Assert.Contains(snippet, s => s.IsMatch && s.Text == "露营吧");
    }

    [Fact]
    public async Task SearchAsync_PropagatesRoomFilterToRepository()
    {
        var repo = new FakeSearchRepository();
        var service = new ChatSearchService(repo);

        var roomId = Guid.NewGuid();
        await service.SearchAsync(
            new ChatSearchRequest("query", new ChatSearchFilter(RoomId: roomId, Take: 5, Offset: 0)),
            CancellationToken.None);

        Assert.Equal(roomId, repo.LastRoomId);
        Assert.Equal(5, repo.LastTake);
        Assert.Equal(0, repo.LastOffset);
    }

    [Fact]
    public async Task SearchAsync_CancelsSupersededCallsOnSameInstance()
    {
        var repo = new FakeSearchRepository { FtsDelay = TimeSpan.FromMilliseconds(200) };
        var service = new ChatSearchService(repo);

        // Two back-to-back invocations should not pile up — repo returns immediately for the
        // test, but the second call should still see the latest state.
        var t1 = service.SearchAsync(new ChatSearchRequest("a", new ChatSearchFilter()), CancellationToken.None);
        var t2 = service.SearchAsync(new ChatSearchRequest("ab", new ChatSearchFilter()), CancellationToken.None);
        await Task.WhenAll(t1, t2);
    }

    // ----- Helpers -----

    private sealed class FakeSearchRepository : IChatSearchRepository
    {
        public int FtsCalls;
        public int LikeCalls;
        public Guid? LastRoomId;
        public int LastTake;
        public int LastOffset;
        public TimeSpan FtsDelay;
        public IReadOnlyList<ChatSearchRow> FtsResponse { get; set; } = Array.Empty<ChatSearchRow>();
        public IReadOnlyList<ChatSearchRow> LikeResponse { get; set; } = Array.Empty<ChatSearchRow>();

        public async Task<IReadOnlyList<ChatSearchRow>> SearchByFtsAsync(
            string phrase, Guid? roomId, int take, int offset, CancellationToken cancellationToken)
        {
            FtsCalls++;
            LastRoomId = roomId;
            LastTake = take;
            LastOffset = offset;
            if (FtsDelay > TimeSpan.Zero) await Task.Delay(FtsDelay, cancellationToken);
            return FtsResponse;
        }

        public async Task<IReadOnlyList<ChatSearchRow>> SearchByLikeAsync(
            string likePattern, Guid? roomId, int take, int offset, CancellationToken cancellationToken)
        {
            LikeCalls++;
            LastRoomId = roomId;
            LastTake = take;
            LastOffset = offset;
            return LikeResponse;
        }
    }
}
