using System.Text.RegularExpressions;

namespace SolarWin.Services;

/// <summary>
/// Dependency-free local tokenizer. English words are normalized directly; Chinese runs
/// are split around stopwords and long runs use deterministic two-character shingles.
/// The ITextTokenizer boundary allows a full Jieba dictionary engine to replace this later.
/// </summary>
public sealed partial class LocalTextTokenizer : ITextTokenizer
{
    private readonly HashSet<string> _stopwords;
    private readonly string[] _orderedStopwords;

    public LocalTextTokenizer()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "stopwords-zh.txt");
        _stopwords = File.Exists(path)
            ? File.ReadLines(path)
                .Select(static x => x.Trim())
                .Where(static x => x.Length > 0 && !x.StartsWith('#'))
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : DefaultStopwords.ToHashSet(StringComparer.OrdinalIgnoreCase);
        _orderedStopwords = _stopwords.OrderByDescending(static x => x.Length).ToArray();
    }

    public IEnumerable<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;
        var cleaned = EmailRegex().Replace(UrlRegex().Replace(text, " "), " ");
        foreach (Match match in TokenRegex().Matches(cleaned))
        {
            var token = match.Value.Trim().ToLowerInvariant();
            if (token.Length == 0 || NumberRegex().IsMatch(token)) continue;
            if (IsAllCjk(token))
            {
                foreach (var chinese in TokenizeChineseRun(token)) yield return chinese;
                continue;
            }

            if (token.Length >= 2 && !_stopwords.Contains(token)) yield return token;
        }
    }

    private IEnumerable<string> TokenizeChineseRun(string run)
    {
        var parts = new List<string> { run };
        foreach (var stopword in _orderedStopwords)
        {
            if (stopword.Length == 0) continue;
            for (var i = parts.Count - 1; i >= 0; i--)
            {
                if (!parts[i].Contains(stopword, StringComparison.Ordinal)) continue;
                var split = parts[i].Split(stopword, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                parts.RemoveAt(i);
                parts.InsertRange(i, split);
            }
        }

        foreach (var part in parts)
        {
            if (part.Length is >= 2 and <= 6)
            {
                yield return part;
            }
            else if (part.Length > 6)
            {
                for (var i = 0; i < part.Length - 1; i += 2)
                {
                    yield return part.Substring(i, Math.Min(2, part.Length - i));
                }
            }
        }
    }

    private static bool IsAllCjk(string value)
        => value.All(static c => c is >= '\u3400' and <= '\u9fff');

    private static readonly string[] DefaultStopwords =
        ["的", "了", "是", "我", "你", "他", "她", "它", "在", "有", "和", "就", "都", "也", "还", "吗", "呢", "啊", "吧", "哦", "嗯"];

    [GeneratedRegex(@"https?://\S+|www\.\S+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlRegex();

    [GeneratedRegex(@"\b[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}\b", RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"[A-Za-z][A-Za-z0-9_'-]*|[\u3400-\u9fff]+|\p{N}+", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"^\p{N}+$", RegexOptions.CultureInvariant)]
    private static partial Regex NumberRegex();
}
