using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media.Imaging;
using SolarWin.Services;
using System.Collections.ObjectModel;

namespace SolarWin.ViewModels;

public sealed class OrderAttrOptionState
{
    public long AttributeId { get; init; }
    public string Name { get; init; } = "";
    public double ExtraPrice { get; init; }
    public bool CanSelected { get; init; }
    public string Label => ExtraPrice > 0 ? $"{Name} +¥{ExtraPrice:0.##}" : Name;
}

public sealed class OrderAttrGroupState
{
    public long AttributeId { get; init; }
    public string Name { get; init; } = "";
    public List<OrderAttrOptionState> Options { get; init; } = [];
    public OrderAttrOptionState? SelectedOption { get; set; }
}

/// <summary>
/// 规格选择对话框的会话状态。skuCode 即当前属性组合（实测：previewOrder/createOrder 只认 skuCode），
/// 每次 switchProduct 后必须用服务端响应整体替换，不能本地假设组合关系。
/// </summary>
public partial class OrderCustomizeState : ObservableObject
{
    public required int ProductId { get; init; }
    public required string Name { get; init; }
    public string? PictureUrl { get; init; }
    public string SkuCode { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PriceText))]
    public partial double Price { get; private set; }

    [ObservableProperty]
    public partial bool IsSwitching { get; set; }

    public ObservableCollection<OrderAttrGroupState> Groups { get; } = [];

    public string PriceText => Price > 0 ? $"¥{Price:0.00}" : "价格以预览为准";

    public string Summary
        => string.Join(" / ", Groups.Select(x => x.SelectedOption?.Name).Where(x => !string.IsNullOrEmpty(x)));

    public BitmapImage? PictureSource
        => Uri.TryCreate(PictureUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? new BitmapImage(uri)
            : null;

    public void Apply(LuckinProductSpec spec, string fallbackSkuCode, double fallbackPrice)
    {
        SkuCode = spec.SkuCode.Length > 0 ? spec.SkuCode : fallbackSkuCode;
        Price = spec.EstimatePrice > 0 ? spec.EstimatePrice : fallbackPrice;

        Groups.Clear();
        foreach (var group in spec.Groups)
        {
            var options = group.Options
                .Select(x => new OrderAttrOptionState
                {
                    AttributeId = x.AttributeId,
                    Name = x.Name,
                    ExtraPrice = x.ExtraPrice,
                    CanSelected = x.CanSelected
                })
                .ToList();
            var selected = -1;
            for (var i = 0; i < group.Options.Count; i++)
            {
                if (group.Options[i].IsSelected) { selected = i; break; }
            }
            Groups.Add(new OrderAttrGroupState
            {
                AttributeId = group.AttributeId,
                Name = group.Name,
                Options = options,
                SelectedOption = selected >= 0 ? options[selected] : options.FirstOrDefault(x => x.CanSelected)
            });
        }

        OnPropertyChanged(nameof(Summary));
    }
}
