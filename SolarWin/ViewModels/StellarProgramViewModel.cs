using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using SolarWin.Models;
using SolarWin.Services;

namespace SolarWin.ViewModels;

public partial class StellarProgramViewModel : ObservableObject
{
    private readonly ISolarApiClient _api;
    private string? _currentIdentifier;

    public StellarProgramViewModel(ISolarApiClient api)
    {
        _api = api;
    }

    public ObservableCollection<StellarPlanItemViewModel> Plans { get; } = [];

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActionMessageVisibility))]
    public partial string? ActionMessage { get; set; }

    [ObservableProperty]
    public partial string MembershipStatusText { get; set; } = "正在获取会员状态…";

    [ObservableProperty]
    public partial string CurrentPlanName { get; set; } = "尚未加入";

    [ObservableProperty]
    public partial string CurrentPlanDetail { get; set; } = "选择适合你的方案，支持 Solar Network 持续发展。";

    [ObservableProperty]
    public partial string QueueSummaryText { get; set; } = "暂无待生效方案";

    [ObservableProperty]
    public partial string LastUpdatedText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial Visibility CancelVisibility { get; set; } = Visibility.Collapsed;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public Visibility ActionMessageVisibility => string.IsNullOrWhiteSpace(ActionMessage)
        ? Visibility.Collapsed
        : Visibility.Visible;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            ActionMessage = null;

            var group = await _api.GetStellarSubscriptionGroupAsync().ConfigureAwait(true);
            ApplyGroup(group);
            LastUpdatedText = $"更新于 {DateTimeOffset.Now:HH:mm}";
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.Message;
            MembershipStatusText = "会员状态暂不可用";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CancelCurrentAsync()
    {
        if (IsBusy || string.IsNullOrWhiteSpace(_currentIdentifier))
        {
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            ActionMessage = null;

            await _api.CancelStellarSubscriptionAsync(_currentIdentifier).ConfigureAwait(true);
            ActionMessage = "恒星计划已取消，正在刷新会员状态。";

            var group = await _api.GetStellarSubscriptionGroupAsync().ConfigureAwait(true);
            ApplyGroup(group);
            LastUpdatedText = $"更新于 {DateTimeOffset.Now:HH:mm}";
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyGroup(SnStellarSubscriptionGroup group)
    {
        Plans.Clear();

        var current = group.Current;
        _currentIdentifier = current?.Subscription.Identifier;
        var hasCurrent = !string.IsNullOrWhiteSpace(_currentIdentifier);

        MembershipStatusText = hasCurrent ? "恒星计划成员" : "当前未订阅";
        CurrentPlanName = hasCurrent
            ? current?.Definition?.DisplayName
              ?? current?.Subscription.DisplayName
              ?? FriendlyTierName(_currentIdentifier!)
            : "尚未加入";
        CurrentPlanDetail = hasCurrent
            ? BuildCurrentPlanDetail(current!.Subscription)
            : "选择适合你的方案，解锁个性化权益并支持 Solar Network 持续发展。";

        CancelVisibility = hasCurrent
                           && current!.Subscription.RenewalAt is not null
                           && string.Equals(
                               current.Subscription.PaymentMethod,
                               "solian.wallet",
                               StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;

        var queued = group.Subscriptions
            .Where(item => item.Subscription.IsPendingActivation
                           || item.Subscription.BegunAt is { } begun && begun > DateTimeOffset.Now)
            .ToList();
        QueueSummaryText = queued.Count switch
        {
            0 => "暂无待生效方案",
            1 => $"1 个方案等待生效：{queued[0].Definition?.DisplayName ?? FriendlyTierName(queued[0].Subscription.Identifier)}",
            _ => $"{queued.Count} 个方案正在等待生效",
        };

        foreach (var plan in group.Catalog.Items
                     .OrderBy(item => item.PerkLevel)
                     .ThenBy(item => item.BasePrice))
        {
            Plans.Add(new StellarPlanItemViewModel(
                plan,
                string.Equals(plan.Identifier, _currentIdentifier, StringComparison.OrdinalIgnoreCase)));
        }
    }

    private static string BuildCurrentPlanDetail(SnStellarSubscription subscription)
    {
        var parts = new List<string>();
        if (subscription.EndedAt is { } ended)
        {
            parts.Add($"有效至 {ended.ToLocalTime():yyyy-MM-dd HH:mm}");
        }
        else
        {
            parts.Add("当前方案没有固定到期时间");
        }

        if (subscription.IsFreeTrial)
        {
            parts.Add("试用方案");
        }

        if (subscription.RenewalAt is { } renewal)
        {
            parts.Add($"预计 {renewal.ToLocalTime():yyyy-MM-dd} 续订");
        }

        return string.Join(" · ", parts);
    }

    internal static string FriendlyTierName(string identifier)
        => identifier.ToLowerInvariant() switch
        {
            "solian.stellar.primary" => "恒星 Stellar",
            "solian.stellar.nova" => "新星 Nova",
            "solian.stellar.supernova" or "solian.stellar.tertiary" => "超新星 Supernova",
            _ => identifier.Split('.').LastOrDefault() ?? "恒星计划",
        };
}

public sealed class StellarPlanItemViewModel
{
    public StellarPlanItemViewModel(SnSubscriptionCatalogItem plan, bool isCurrent)
    {
        Identifier = plan.Identifier;
        DisplayName = string.IsNullOrWhiteSpace(plan.DisplayName)
            ? StellarProgramViewModel.FriendlyTierName(plan.Identifier)
            : plan.DisplayName;
        PriceText = $"{plan.BasePrice:0.##} {FormatCurrency(plan.Currency)} / 30 天";
        EligibilityText = plan.MinimumAccountLevel is > 0
            ? $"需要账户等级 Lv.{plan.MinimumAccountLevel}"
            : "无账户等级限制";
        BonusText = BuildBonusText(plan);
        PaymentMethodsText = BuildPaymentMethods(plan.AllowedPaymentMethods);
        BenefitsText = BuildBenefitsText(plan.Identifier);
        IsCurrent = isCurrent;
        CurrentBadgeVisibility = isCurrent ? Visibility.Visible : Visibility.Collapsed;
        CanOpenCheckout = !isCurrent;
        ActionText = isCurrent ? "当前方案" : "查看订阅方式";

        var color = ParseColor(plan.DisplayConfig?.Color, plan.Identifier);
        AccentBrush = new SolidColorBrush(color);
        AccentBackgroundBrush = new SolidColorBrush(
            Windows.UI.Color.FromArgb(0x24, color.R, color.G, color.B));
        CardBorderBrush = new SolidColorBrush(
            isCurrent
                ? color
                : Windows.UI.Color.FromArgb(0x38, color.R, color.G, color.B));
        CardBorderThickness = isCurrent ? new Thickness(2) : new Thickness(1);
    }

    public string Identifier { get; }
    public string DisplayName { get; }
    public string PriceText { get; }
    public string EligibilityText { get; }
    public string BonusText { get; }
    public string PaymentMethodsText { get; }
    public string BenefitsText { get; }
    public bool IsCurrent { get; }
    public bool CanOpenCheckout { get; }
    public string ActionText { get; }
    public Visibility CurrentBadgeVisibility { get; }
    public Brush AccentBrush { get; }
    public Brush AccentBackgroundBrush { get; }
    public Brush CardBorderBrush { get; }
    public Thickness CardBorderThickness { get; }

    private static string BuildBonusText(SnSubscriptionCatalogItem plan)
    {
        var details = new List<string>();
        if (plan.ExperienceMultiplier is > 1)
        {
            details.Add($"{plan.ExperienceMultiplier:0.##}× 经验加成");
        }

        if (plan.GoldenPointReward is > 0)
        {
            details.Add($"每周期 +{plan.GoldenPointReward} 金点");
        }

        return details.Count == 0 ? "基础会员权益" : string.Join(" · ", details);
    }

    private static string BuildPaymentMethods(IEnumerable<string> methods)
    {
        var names = methods.Select(method => method.ToLowerInvariant() switch
            {
                "solian.wallet" => "Solar 钱包",
                "apple_store" => "Apple 内购",
                "afdian" => "爱发电",
                "paddle" => "Paddle",
                _ => method,
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return names.Count == 0 ? "结账方式以官网为准" : "支持：" + string.Join("、", names);
    }

    private static string BuildBenefitsText(string identifier)
    {
        var benefits = identifier.ToLowerInvariant() switch
        {
            "solian.stellar.primary" => new[]
            {
                "1.5× 账户升级加成",
                "有限的用户名颜色",
                "翻译服务",
                "可申请账户认证",
            },
            "solian.stellar.nova" => new[]
            {
                "2× 账户升级加成",
                "无限用户名颜色",
                "自定义标签",
                "更多领域与机器人配额",
                "翻译服务与认证资格",
            },
            "solian.stellar.supernova" or "solian.stellar.tertiary" => new[]
            {
                "2.5× 账户升级加成",
                "渐变用户名颜色",
                "包含全部 Nova 权益",
                "优先支持",
                "专属徽章",
            },
            _ => new[] { "完整权益请前往官方定价页面查看" },
        };

        return string.Join(Environment.NewLine, benefits.Select(benefit => $"✓  {benefit}"));
    }

    private static string FormatCurrency(string currency)
        => currency.ToLowerInvariant() switch
        {
            "golds" => "NSP",
            _ => currency.ToUpperInvariant(),
        };

    private static Windows.UI.Color ParseColor(string? value, string identifier)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            var raw = value.Trim().TrimStart('#');
            if (raw.Length == 6 && uint.TryParse(raw, System.Globalization.NumberStyles.HexNumber, null, out var rgb))
            {
                return Windows.UI.Color.FromArgb(
                    0xFF,
                    (byte)(rgb >> 16),
                    (byte)(rgb >> 8),
                    (byte)rgb);
            }
        }

        return identifier.ToLowerInvariant() switch
        {
            "solian.stellar.nova" => Windows.UI.Color.FromArgb(0xFF, 0x39, 0xC5, 0xBB),
            "solian.stellar.supernova" or "solian.stellar.tertiary" => Windows.UI.Color.FromArgb(0xFF, 0xF2, 0xA9, 0x00),
            _ => Windows.UI.Color.FromArgb(0xFF, 0x55, 0x86, 0xE8),
        };
    }
}
