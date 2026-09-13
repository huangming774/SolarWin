using SolarWin.Services;

namespace SolarWin.ViewModels;

/// <summary>
/// 规格选择（冷热/糖度等）。实测契约：previewOrder/createOrder 的 productList 只认 {amount,productId,skuCode}，
/// 属性组合通过 skuCode 区分——详情返回的 skuCode 对应其 selected 组合，switchProduct 链式返回新组合的 skuCode。
/// </summary>
public partial class OrderViewModel
{
    /// <summary>打开规格对话框时调用：拉取详情属性组；返回 null 表示失败（StatusText 已写明原因）。</summary>
    public async Task<OrderCustomizeState?> LoadCustomizeAsync(OrderProduct product, CancellationToken cancellationToken = default)
    {
        if (SelectedShop is not { } shop)
        {
            StatusText = "请先选择门店";
            return null;
        }
        if (product.ProductId <= 0)
        {
            StatusText = "该商品缺少 productId，无法读取规格";
            return null;
        }

        try
        {
            var spec = await GetOrFetchSpecAsync(shop.DeptId, product.ProductId, cancellationToken);
            if (spec is null)
            {
                StatusText = "读取商品规格失败：服务端未返回规格数据";
                return null;
            }

            var state = new OrderCustomizeState
            {
                ProductId = product.ProductId,
                Name = product.Name,
                PictureUrl = product.PictureUrl
            };
            // detail.skuCode 精确对应 detail 的 selected 组合（所见即所得基准）；缺失时退回搜索结果 sku
            state.Apply(spec, product.SkuCode, product.Price);
            return state;
        }
        catch (Exception ex)
        {
            StatusText = $"读取商品规格失败：{ex.Message}";
            return null;
        }
    }

    /// <summary>切换某一组的选中项；成功后 state 已被服务端响应整体替换（含新 skuCode/价格/可能重映射的其它组）。</summary>
    public async Task<bool> SwitchCustomizeOptionAsync(OrderCustomizeState state, OrderAttrGroupState group, OrderAttrOptionState option, CancellationToken cancellationToken = default)
    {
        if (SelectedShop is not { } shop || state.IsSwitching) return false;

        try
        {
            state.IsSwitching = true;
            var response = await _mcp.CallAsync("switchProduct", new Dictionary<string, object?>
            {
                ["deptId"] = shop.DeptId,
                ["productId"] = state.ProductId,
                ["skuCode"] = state.SkuCode,
                ["attrOperationParam"] = new
                {
                    attributeId = group.AttributeId,
                    subAttr = new { attributeId = option.AttributeId, operation = 1 }
                },
                ["amount"] = 1
            }, cancellationToken);

            state.Apply(LuckinProductAttrs.Parse(response), state.SkuCode, state.Price);
            return true;
        }
        catch (Exception ex)
        {
            StatusText = $"切换{group.Name}失败：{ex.Message}";
            return false;
        }
        finally
        {
            state.IsSwitching = false;
        }
    }

    /// <summary>把规格对话框里的当前组合加入购物车；同一 sku（同一属性组合）合并数量。</summary>
    public void AddCustomizedToCart(OrderCustomizeState state)
    {
        var existing = Cart.FirstOrDefault(x => string.Equals(x.SkuCode, state.SkuCode, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            Cart.Add(new OrderProduct
            {
                ProductId = state.ProductId,
                SkuCode = state.SkuCode,
                Name = state.Name,
                Price = state.Price,
                PictureUrl = state.PictureUrl,
                AttrSummary = state.Summary
            });
        }
        else
        {
            existing.Amount++;
        }

        OnCartChanged();
        StatusText = $"已加入：{state.Name}（{state.Summary}）";
    }
}
