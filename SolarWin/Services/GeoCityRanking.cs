using SolarWin.Models;

namespace SolarWin.Services;

/// <summary>
/// 中国城市候选合并与排序（纯逻辑，便于单测）。
/// Open-Meteo geocoding 的中文搜索常漏掉主要城市——实测（2026-09-12）搜“珠海”只返回
/// 山东青岛的同名村庄、搜“朝阳”只返回乡镇级小地名，直接取第一条会把门店定位到错误城市；
/// 因此除原始关键词外需并发补查“××市”，把两路结果交给本类合并排序。
/// </summary>
public static class GeoCityRanking
{
    /// <summary>
    /// 合并多路 geocoding 结果：仅保留中国条目并按 Id 去重，按
    /// “名称精确匹配（含 ××市 变体）&gt; 人口 &gt; 行政区划驻地 &gt; 原始顺序”排序，
    /// 让主要城市排在同名村庄/岛屿之前（如 广东珠海市 先于 山东青岛珠海村）。
    /// </summary>
    public static List<GeoResult> MergeAndRankChinaCities(
        string query,
        IReadOnlyList<GeoResult> primary,
        IReadOnlyList<GeoResult> secondary)
    {
        var queryWithShi = query + "市";
        var seen = new HashSet<long>();
        var candidates = new List<GeoResult>();

        foreach (var result in primary.Concat(secondary))
        {
            if (result.Country is null ||
                (!string.Equals(result.Country, "中国", StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(result.Country, "China", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }
            if (!seen.Add(result.Id))
            {
                continue;
            }
            candidates.Add(result);
        }

        return candidates
            .Select((result, index) => (result, index))
            .OrderBy(entry => NameMatchTier(entry.result, query, queryWithShi))
            .ThenByDescending(entry => entry.result.Population ?? 0)
            .ThenByDescending(entry => IsAdminSeat(entry.result))
            .ThenBy(entry => entry.index)
            .Select(entry => entry.result)
            .ToList();
    }

    private static int NameMatchTier(GeoResult result, string query, string queryWithShi)
    {
        if (string.Equals(result.Name, query, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(result.Name, queryWithShi, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }
        return result.Name?.StartsWith(query, StringComparison.OrdinalIgnoreCase) == true ? 1 : 2;
    }

    private static bool IsAdminSeat(GeoResult result)
        => result.FeatureCode is not null &&
           (string.Equals(result.FeatureCode, "PPLC", StringComparison.Ordinal) ||
            result.FeatureCode.StartsWith("PPLA", StringComparison.Ordinal));
}
