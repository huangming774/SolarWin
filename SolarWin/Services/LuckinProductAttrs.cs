using System.Globalization;
using System.Text.Json;

namespace SolarWin.Services;

public sealed record LuckinAttrOption(long AttributeId, string Name, double ExtraPrice, bool IsSelected, bool CanSelected);

public sealed record LuckinAttrGroup(long AttributeId, string Name, IReadOnlyList<LuckinAttrOption> Options);

/// <summary>某一属性组合下的商品规格快照：skuCode 唯一对应一组属性选择。</summary>
public sealed record LuckinProductSpec(string SkuCode, double EstimatePrice, double InitialPrice, IReadOnlyList<LuckinAttrGroup> Groups);

/// <summary>
/// 解析 queryProductDetailInfo / switchProduct 响应中的 productAttrs（杯型/温度/糖度等）。
/// 实测契约（2026-09-10）：下单接口不接受属性字段，属性组合通过 skuCode 区分；
/// switchProduct 返回新 skuCode + 更新后的 productAttrs + 最新价格，且可能重映射其它组的选项，
/// 因此调用方必须用解析结果整体替换本地状态。
/// </summary>
internal static class LuckinProductAttrs
{
    public static LuckinProductSpec Parse(JsonElement payload)
    {
        var groups = new List<LuckinAttrGroup>();
        if (LuckinJson.TryFindProperty(payload, out var attrs, "productAttrs") && attrs.ValueKind == JsonValueKind.Array)
        {
            foreach (var groupElement in attrs.EnumerateArray())
            {
                if (groupElement.ValueKind != JsonValueKind.Object) continue;

                var options = new List<LuckinAttrOption>();
                if (groupElement.TryGetProperty("productSubAttrs", out var subs) && subs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var sub in subs.EnumerateArray())
                    {
                        if (sub.ValueKind != JsonValueKind.Object) continue;
                        options.Add(new LuckinAttrOption(
                            DirectInt64(sub, "attributeId"),
                            DirectString(sub, "attributeName"),
                            DirectNumber(sub, "price"),
                            DirectBool(sub, "selected"),
                            DirectInt64(sub, "canSelected") != 0));
                    }
                }

                if (options.Count > 0)
                {
                    groups.Add(new LuckinAttrGroup(
                        DirectInt64(groupElement, "attributeId"),
                        DirectString(groupElement, "attributeName"),
                        options));
                }
            }
        }

        return new LuckinProductSpec(
            LuckinJson.String(payload, "skuCode"),
            LuckinJson.Number(payload, "estimatePrice"),
            LuckinJson.Number(payload, "initialPrice"),
            groups);
    }

    // 组/选项层级只读直接属性：attributeName/price/selected 等字段在组与选项层级重名，不能用 LuckinJson 的递归查找。

    private static string DirectString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static long DirectInt64(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)) return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)) return number;
        return long.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    private static double DirectNumber(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)) return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number)) return number;
        return double.TryParse(value.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    private static bool DirectBool(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}
