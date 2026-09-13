using System.Text.Json;
using SolarWin.Services;

namespace SolarWin.Tests;

public sealed class LuckinJsonTests
{
    [Fact]
    public void FindEntities_TraversesNestedAndEmbeddedJson()
    {
        using var document = JsonDocument.Parse("""
        {
          "result": {
            "content": [
              { "text": "{\"data\":{\"products\":[{\"productId\":1,\"skuCode\":\"A\"},{\"productId\":2,\"skuCode\":\"B\"}]}}" }
            ]
          }
        }
        """);

        var products = LuckinJson.FindEntities(document.RootElement, "productId", "skuCode");

        Assert.Equal(2, products.Count);
        Assert.Equal(1, LuckinJson.Int32(products[0], "productId"));
        Assert.Equal("B", LuckinJson.String(products[1], "skuCode"));
    }

    [Fact]
    public void TryFindProperty_FindsPaymentQrUrlAndCouponsInWrappers()
    {
        using var document = JsonDocument.Parse("""
        {
          "structuredContent": {
            "data": {
              "payOrderQrCodeUrl": "https://pay.example/qr",
              "couponCodeList": ["C1", "C2"]
            }
          }
        }
        """);

        Assert.Equal("https://pay.example/qr", LuckinJson.String(document.RootElement, "payOrderQrCodeUrl"));
        Assert.True(LuckinJson.TryFindProperty(document.RootElement, out var coupons, "couponCodeList"));
        Assert.Equal(2, coupons.GetArrayLength());
    }

    [Fact]
    public void Number_ParsesInvariantStringValues()
    {
        using var document = JsonDocument.Parse("{\"discountPrice\":\"9.90\"}");

        Assert.Equal(9.9, LuckinJson.Number(document.RootElement, "discountPrice"), 3);
    }

    [Fact]
    public void ThrowIfBusinessError_ThrowsServerMessageOnNonZeroCode()
    {
        using var document = JsonDocument.Parse("{\"code\":1000,\"msg\":\"店铺已打烊ZzZ，07:00-21:00 营业时间再来吧\",\"data\":null,\"success\":false}");

        var ex = Assert.Throws<InvalidOperationException>(() => LuckinJson.ThrowIfBusinessError(document.RootElement));

        Assert.Contains("店铺已打烊", ex.Message);
    }

    [Fact]
    public void ThrowIfBusinessError_ThrowsWhenSuccessFalseWithNumericStringCode()
    {
        using var document = JsonDocument.Parse("{\"code\":\"1001\",\"msg\":\"库存不足\",\"success\":false}");

        var ex = Assert.Throws<InvalidOperationException>(() => LuckinJson.ThrowIfBusinessError(document.RootElement));

        Assert.Equal("库存不足", ex.Message);
    }

    [Fact]
    public void ThrowIfBusinessError_IgnoresSuccessfulPayloads()
    {
        using var document = JsonDocument.Parse("{\"code\":0,\"msg\":\"success\",\"data\":{\"discountPrice\":13.9},\"success\":true}");

        LuckinJson.ThrowIfBusinessError(document.RootElement);
    }

    [Fact]
    public void ThrowIfBusinessError_IgnoresNestedCodeInSuccessPayload()
    {
        using var document = JsonDocument.Parse("{\"code\":0,\"data\":{\"inner\":{\"code\":1000,\"msg\":\"not top level\"}},\"success\":true}");

        LuckinJson.ThrowIfBusinessError(document.RootElement);
    }
}
