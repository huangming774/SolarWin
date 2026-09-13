using System.Text.Json;
using SolarWin.Services;

namespace SolarWin.Tests;

public sealed class LuckinProductAttrsTests
{
    // 2026-09-10 对官方 MCP 实测抓取的生椰拿铁详情（精简保留了结构与关键字段）
    private const string DetailPayload = """
    {"code":0,"msg":"success","data":{"productId":1262,"productName":"生椰拿铁（首创）","skuCode":"SP2077-01134",
    "productAttrs":[
      {"attributeId":64,"attributeName":"杯型","productSubAttrs":[
        {"attributeId":365,"attributeName":"大杯","selected":true,"price":0.0,"canSelected":1},
        {"attributeId":594,"attributeName":"超大杯","selected":false,"price":3.0,"canSelected":1}]},
      {"attributeId":17,"attributeName":"温度","productSubAttrs":[
        {"attributeId":57,"attributeName":"冰","selected":true,"price":0.0,"canSelected":1},
        {"attributeId":56,"attributeName":"热","selected":false,"price":0.0,"canSelected":1}]},
      {"attributeId":18,"attributeName":"糖度","productSubAttrs":[
        {"attributeId":60,"attributeName":"标准甜","selected":false,"price":0.0,"canSelected":1},
        {"attributeId":69,"attributeName":"不另外加糖","selected":true,"price":0.0,"canSelected":1}]}
    ],"initialPrice":20.0,"estimatePrice":16.6},"success":true}
    """;

    // 切到超大杯后的 switchProduct 响应：sku 变化、selected 迁移、价格更新
    private const string SwitchPayload = """
    {"code":0,"msg":"success","data":{"productId":1262,"productName":"生椰拿铁（首创）","skuCode":"SP2077-01013",
    "productAttrs":[
      {"attributeId":64,"attributeName":"杯型","productSubAttrs":[
        {"attributeId":365,"attributeName":"大杯","selected":false,"price":0.0,"canSelected":1},
        {"attributeId":594,"attributeName":"超大杯","selected":true,"price":3.0,"canSelected":1}]},
      {"attributeId":17,"attributeName":"温度","productSubAttrs":[
        {"attributeId":57,"attributeName":"冰","selected":true,"price":0.0,"canSelected":1},
        {"attributeId":56,"attributeName":"热","selected":false,"price":0.0,"canSelected":1}]}
    ],"initialPrice":23.0,"estimatePrice":19.6},"success":true}
    """;

    [Fact]
    public void Parse_DetailPayload_ReturnsSkuPricesAndSelectedOptions()
    {
        using var document = JsonDocument.Parse(DetailPayload);

        var spec = LuckinProductAttrs.Parse(document.RootElement);

        Assert.Equal("SP2077-01134", spec.SkuCode);
        Assert.Equal(16.6, spec.EstimatePrice, 3);
        Assert.Equal(20.0, spec.InitialPrice, 3);
        Assert.Equal(3, spec.Groups.Count);

        var cup = spec.Groups[0];
        Assert.Equal(64, cup.AttributeId);
        Assert.Equal("杯型", cup.Name);
        Assert.Equal(2, cup.Options.Count);
        Assert.True(cup.Options[0].IsSelected);
        Assert.Equal("大杯", cup.Options[0].Name);
        Assert.Equal(3.0, cup.Options[1].ExtraPrice, 3);
        Assert.False(cup.Options[1].IsSelected);
        Assert.True(cup.Options[1].CanSelected);

        var sugar = spec.Groups[2];
        Assert.Equal("糖度", sugar.Name);
        Assert.True(sugar.Options.Single(x => x.Name == "不另外加糖").IsSelected);
    }

    [Fact]
    public void Parse_SwitchPayload_ReflectsNewSkuSelectionAndPrice()
    {
        using var document = JsonDocument.Parse(SwitchPayload);

        var spec = LuckinProductAttrs.Parse(document.RootElement);

        Assert.Equal("SP2077-01013", spec.SkuCode);
        Assert.Equal(19.6, spec.EstimatePrice, 3);
        Assert.Equal(23.0, spec.InitialPrice, 3);
        var cup = Assert.Single(spec.Groups, x => x.AttributeId == 64);
        Assert.False(cup.Options[0].IsSelected);
        Assert.True(cup.Options[1].IsSelected);
    }

    [Fact]
    public void Parse_PreviewEchoShape_NullSelectedIsFalse()
    {
        // previewOrder 的 productInfoList[].productAttrs 回显里 selected/canSelected 为 null
        using var document = JsonDocument.Parse("""
        {"code":0,"data":{"productAttrs":[
          {"attributeId":17,"attributeName":"温度","productSubAttrs":[
            {"attributeId":56,"attributeName":"热","selected":null,"price":0.0,"canSelected":null}]}
        ]},"success":true}
        """);

        var spec = LuckinProductAttrs.Parse(document.RootElement);

        var group = Assert.Single(spec.Groups);
        var option = Assert.Single(group.Options);
        Assert.False(option.IsSelected);
        Assert.False(option.CanSelected);
    }

    [Fact]
    public void Parse_PayloadWithoutAttrs_ReturnsEmptyGroups()
    {
        using var document = JsonDocument.Parse("""{"code":0,"data":{"productId":1,"initialPrice":9.9},"success":true}""");

        var spec = LuckinProductAttrs.Parse(document.RootElement);

        Assert.Empty(spec.Groups);
        Assert.Equal("", spec.SkuCode);
        Assert.Equal(9.9, spec.InitialPrice, 3);
    }
}
