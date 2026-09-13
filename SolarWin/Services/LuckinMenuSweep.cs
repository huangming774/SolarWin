namespace SolarWin.Services;

/// <summary>
/// 瑞幸咖啡菜单全量枚举的查询计划器（纯逻辑、无 UI 依赖，供单测直连）。
/// 实测契约：searchProductForMcp 只收 {deptId, query}（tools/list 的 inputSchema additionalProperties:false），
/// 无分页参数且每次最多返回 3 条推荐——全量菜单只能靠大量互补查询的并集去重逼近：
/// 种子词轮覆盖品类/风味/豆种主线，之后每轮从已发现商品名派生 2 字滑窗词根继续扩展，
/// 直到整轮无新增（收敛）或触达轮次/查询预算上限。
/// </summary>
internal static class LuckinMenuSweep
{
    /// <summary>最大扫描轮数：种子轮 + 3 轮派生扩展。</summary>
    internal const int MaxRounds = 4;

    /// <summary>全局查询预算。RunAsync 有 120s 应用层超时，4 并发下 300 次查询约 45-75s，留足余量。</summary>
    internal const int MaxQueries = 300;

    /// <summary>种子查询词。[0] 用作首轮单独查询（先出首屏），其余进入第 1 轮并行波。</summary>
    internal static readonly string[] SeedQueries =
    [
        "全部在售咖啡菜单",
        // 品类
        "拿铁", "美式", "澳瑞白", "馥芮白", "摩卡", "玛奇朵", "卡布奇诺", "康宝蓝", "Dirty", "冰吸",
        "冷萃", "手冲", "SOE", "意式", "黑咖", "浓缩", "果咖", "茶咖", "气泡美式", "瑞纳冰",
        "新品咖啡", "经典咖啡", "冰咖啡", "热咖啡", "无奶黑咖啡",
        // 奶基 / 产品线
        "生椰", "丝绒", "厚乳", "厚椰乳", "燕麦", "椰青", "黄油", "布列夫", "马斯卡彭", "生酪", "芝士", "奶油",
        // 风味
        "焦糖", "香草", "榛果", "抹茶", "黑巧", "白巧", "巧克力", "曲奇", "提拉米苏", "慕斯", "可可",
        // 水果
        "橙C", "香橙", "西柚", "柚C", "柠檬", "草莓", "蓝莓", "树莓", "莓果", "白桃", "葡萄", "芒果", "菠萝", "苹果", "西梅",
        // 花香 / 茶底
        "桂花", "茉莉", "山茶花", "龙井",
        // 豆种 / 产品系
        "埃塞", "耶加雪菲", "曼特宁", "云南", "拼配", "精品", "小黑杯", "大师", "特调", "挂耳",
        // 酒香
        "酒香", "威士忌", "白兰地", "桶香"
    ];

    /// <summary>构造第 round 轮要并行发出的查询波（入选查询同时记入 issued 占用预算）。
    /// round=1 返回尚未发出的种子词；round≥2 从已发现商品名派生 2 字滑窗词根。
    /// 返回空列表表示扫描应结束：超出轮次上限 / 预算耗尽 / 没有新查询可发。</summary>
    internal static IReadOnlyList<string> BuildWave(int round, IEnumerable<string> productNames, ISet<string> issued)
    {
        if (round < 1 || round > MaxRounds || issued.Count >= MaxQueries) return [];

        var wave = new List<string>();
        var candidates = round == 1 ? SeedQueries : DeriveQueries(productNames);
        foreach (var query in candidates)
        {
            if (issued.Count >= MaxQueries) break;
            if (issued.Add(query)) wave.Add(query);
        }
        return wave;
    }

    /// <summary>从商品名派生查询词根：去掉（首创）之类括注后取 2 字滑窗，
    /// 只保留纯 CJK 或纯 ASCII 字母的窗口（“C美”这类混排窗口没有匹配价值）。</summary>
    private static IEnumerable<string> DeriveQueries(IEnumerable<string> productNames)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in productNames)
        {
            var name = StripParenthetical(raw);
            for (var i = 0; i + 2 <= name.Length; i++)
            {
                var slice = name.Substring(i, 2);
                if (!IsQueryableSlice(slice)) continue;
                if (seen.Add(slice)) yield return slice;
            }
        }
    }

    private static string StripParenthetical(string name)
    {
        var cut = name.IndexOfAny('（', '(');
        return cut > 0 ? name[..cut] : name;
    }

    private static bool IsQueryableSlice(string slice)
        => slice.All(IsCjk) || slice.All(char.IsAsciiLetter);

    private static bool IsCjk(char c) => c >= '\u4E00' && c <= '\u9FFF';
}
