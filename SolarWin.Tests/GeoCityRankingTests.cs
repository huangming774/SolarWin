using SolarWin.Models;
using SolarWin.Services;

namespace SolarWin.Tests;

/// <summary>
/// GeoCityRanking 合并排序的回归测试。
/// 数据取自 2026-09-12 对 Open-Meteo geocoding（language=zh）的实测响应：
/// 搜“珠海”只返回山东青岛的同名村庄，广东珠海市必须靠补查“珠海市”才能召回。
/// </summary>
public sealed class GeoCityRankingTests
{
    private static GeoResult City(
        long id,
        string name,
        string admin1,
        double lat,
        double lon,
        long? population = null,
        string? featureCode = null,
        string admin2 = "")
        => new()
        {
            Id = id,
            Name = name,
            Country = "中国",
            Admin1 = admin1,
            Admin2 = admin2,
            Latitude = lat,
            Longitude = lon,
            Population = population,
            FeatureCode = featureCode,
        };

    [Fact]
    public void Zhuhai_MajorCityFromSecondaryQueryOutranksSameNamedVillage()
    {
        // 主查询“珠海”的真实响应：仅山东青岛珠海村（PPL、无人口）。
        var primary = new List<GeoResult>
        {
            City(8708290, "珠海", "山东", 35.87124, 119.99638, featureCode: "PPL", admin2: "青岛市"),
        };
        // 补查“珠海市”的真实响应：广东珠海市（PPLA2、220 万人口）。
        var secondary = new List<GeoResult>
        {
            City(1790437, "珠海市", "广东", 22.27694, 113.56778, 2207090, "PPLA2", "珠海市"),
        };

        var ranked = GeoCityRanking.MergeAndRankChinaCities("珠海", primary, secondary);

        var winner = Assert.Single(ranked, r => r.Name == "珠海市");
        Assert.Equal(22.27694, winner.Latitude, 0.000001);
        Assert.Equal("广东", winner.Admin1);
        // 青岛珠海村仍在列表里，只是排到后面（用户可感知候选）。
        Assert.Equal(8708290, ranked[^1].Id);
    }

    [Fact]
    public void Zhongshan_MajorCityFromPrimaryOutranksSmallerSameNames()
    {
        // 搜“中山”的真实响应（节选）：中山市居首，重庆中山为 PPLA4 小政区。
        // 注意：真实 API 把臺灣条目的 country 也标为“中国”，国别过滤挡不住，
        // 与旧实现一致地保留，靠人口排序排到后面。
        var primary = new List<GeoResult>
        {
            City(1790438, "中山", "广东", 22.52306, 113.37912, 3841873, "PPLA2", "中山市"),
            City(10001, "中山", "重庆市", 28.85541, 106.33602, 20509, "PPLA4", "重庆市"),
            City(10002, "中山", "福建省", 25.02737, 116.03578, null, "PPLA4", "龙岩市"),
            City(10003, "中山", "臺灣省 or 台灣省", 24.33806, 120.65694, featureCode: "PPL"),
        };

        var ranked = GeoCityRanking.MergeAndRankChinaCities("中山", primary, []);

        Assert.Equal("广东", ranked[0].Admin1);
        Assert.Equal(113.37912, ranked[0].Longitude, 0.000001);
        // 臺灣条目保留在候选里（真实 API 标注 country=中国），但绝不排第一。
        var taiwanIndex = ranked.FindIndex(r => r.Admin1 is not null && r.Admin1.Contains("臺灣"));
        Assert.True(taiwanIndex > 0, "臺灣同名条目应保留但不排第一");
    }

    [Fact]
    public void Ranking_FiltersNonChinaAndDedupesById()
    {
        var primary = new List<GeoResult>
        {
            // 日本同名地，Country 非“中国”，应被过滤。
            new() { Id = 20001, Name = "广州", Country = "日本", Latitude = 35.0, Longitude = 139.0 },
            City(20002, "广州", "广东", 23.11667, 113.25, 16096724, "PPLA"),
        };
        var secondary = new List<GeoResult>
        {
            City(20002, "广州", "广东", 23.11667, 113.25, 16096724, "PPLA"), // 同 Id 重复
            City(20003, "广州", "广东", 23.11667, 113.25, 100, "PPL"),
        };

        var ranked = GeoCityRanking.MergeAndRankChinaCities("广州", primary, secondary);

        var guangzhou = Assert.Single(ranked, r => r.Id == 20002);
        Assert.Equal(2, ranked.Count);
        Assert.Equal(16096724, guangzhou.Population);
    }

    [Fact]
    public void Ranking_ExactNameMatchBeatsBiggerUnrelatedCity()
    {
        var primary = new List<GeoResult>
        {
            City(30001, "北京", "北京市", 39.9, 116.4, 21800000, "PPLA"),
            City(30002, "香洲", "广东", 22.07683, 113.48202, featureCode: "ISL"),
        };

        var ranked = GeoCityRanking.MergeAndRankChinaCities("香洲", primary, []);

        // “香洲”岛与查询精确同名（tier 0），北京虽人口更多但名称不匹配（tier 2）。
        Assert.Equal(30002, ranked[0].Id);
    }

    [Fact]
    public void Ranking_EmptyInputsYieldEmptyList()
    {
        Assert.Empty(GeoCityRanking.MergeAndRankChinaCities("不存在的地方", [], []));
    }
}
