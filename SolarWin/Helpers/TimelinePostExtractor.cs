using System.Text.Json;
using SolarWin.Models;

namespace SolarWin.Helpers;

/// <summary>
/// Pull <see cref="SnPost"/> instances out of timeline events whose <c>data</c>
/// field is untyped in OpenAPI (may be a bare post or a wrapper).
/// </summary>
public static class TimelinePostExtractor
{
    public static List<SnPost> ExtractPosts(SnTimelinePage? page)
    {
        if (page?.Items is null || page.Items.Count == 0)
        {
            return [];
        }

        var posts = new List<SnPost>();
        var seen = new HashSet<Guid>();

        foreach (var evt in page.Items)
        {
            foreach (var post in ExtractFromEvent(evt))
            {
                if (post.Id != Guid.Empty && seen.Add(post.Id))
                {
                    posts.Add(post);
                }
            }
        }

        return posts;
    }

    public static IEnumerable<SnPost> ExtractFromEvent(SnTimelineEvent evt)
    {
        if (evt.Data.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            yield break;
        }

        if (evt.Data.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in evt.Data.EnumerateArray())
            {
                var nested = TryDeserializePost(el);
                if (nested is not null)
                {
                    yield return nested;
                }
            }

            yield break;
        }

        if (evt.Data.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        // Common wrappers: { post: {...} }, { data: {...} }, bare SnPost
        // Live gateway posts.new events put the full SnPost in "data".
        foreach (var key in new[] { "post", "data", "resource", "content", "payload" })
        {
            if (evt.Data.TryGetProperty(key, out var wrapped) && wrapped.ValueKind == JsonValueKind.Object)
            {
                var fromWrap = TryDeserializePost(wrapped);
                if (fromWrap is not null)
                {
                    yield return fromWrap;
                    yield break;
                }
            }
        }

        // Event itself may be the post when type is posts.*
        var direct = TryDeserializePost(evt.Data);
        if (direct is not null)
        {
            yield return direct;
        }
    }

    private static SnPost? TryDeserializePost(JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        // Heuristic: must look like a post (id + content-ish / publisher).
        // Timeline events use type "posts.new" with nested post in data — caller unwraps first.
        var hasId = el.TryGetProperty("id", out var idEl)
                    && idEl.ValueKind is JsonValueKind.String or JsonValueKind.Number;
        if (!hasId)
        {
            return null;
        }

        var looksLikePost =
            el.TryGetProperty("content", out _) ||
            el.TryGetProperty("publisher", out _) ||
            el.TryGetProperty("publisher_id", out _) ||
            el.TryGetProperty("replies_count", out _) ||
            el.TryGetProperty("visibility", out _) ||
            el.TryGetProperty("published_at", out _) ||
            el.TryGetProperty("attachments", out _) ||
            el.TryGetProperty("body", out _);

        // Still try deserialize if id is uuid-shaped even without extra fields
        if (!looksLikePost)
        {
            var idStr = idEl.ValueKind == JsonValueKind.String ? idEl.GetString() : null;
            if (idStr is null || !Guid.TryParse(idStr, out _))
            {
                return null;
            }
        }

        try
        {
            return el.Deserialize<SnPost>(JsonDefaults.Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
