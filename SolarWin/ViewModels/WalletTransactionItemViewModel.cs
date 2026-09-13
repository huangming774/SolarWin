using Microsoft.UI.Xaml.Media;
using SolarWin.Models;

namespace SolarWin.ViewModels;

public sealed class WalletTransactionItemViewModel
{
    private static readonly Brush IncomingForeground = new SolidColorBrush(
        Microsoft.UI.ColorHelper.FromArgb(255, 16, 137, 85));
    private static readonly Brush IncomingBackground = new SolidColorBrush(
        Microsoft.UI.ColorHelper.FromArgb(30, 16, 185, 129));
    private static readonly Brush OutgoingForeground = new SolidColorBrush(
        Microsoft.UI.ColorHelper.FromArgb(255, 220, 74, 74));
    private static readonly Brush OutgoingBackground = new SolidColorBrush(
        Microsoft.UI.ColorHelper.FromArgb(30, 239, 68, 68));

    public WalletTransactionItemViewModel(SnWalletTransaction transaction, Guid walletId)
    {
        Transaction = transaction;
        Id = transaction.Id;
        IsIncome = transaction.PayeeWalletId == walletId;

        var sign = IsIncome ? "+" : "-";
        var amount = Math.Abs(transaction.Amount);
        var counterparty = IsIncome
            ? DisplayWallet(transaction.PayerWallet, "系统")
            : DisplayWallet(transaction.PayeeWallet, "系统");

        DirectionText = IsIncome ? "收入" : "支出";
        IconGlyph = IsIncome ? "\uE896" : "\uE898";
        AmountForeground = IsIncome ? IncomingForeground : OutgoingForeground;
        IconForeground = AmountForeground;
        IconBackground = IsIncome ? IncomingBackground : OutgoingBackground;

        Title = counterparty;
        SubtitleText = string.IsNullOrWhiteSpace(transaction.Remarks)
            ? TypeText(transaction.Type)
            : transaction.Remarks!;
        AmountText = $"{sign}{amount:0.##} {transaction.Currency}";
        TimeText = FormatTime(transaction.CreatedAt);
        FullTimeText = FormatFullTime(transaction.CreatedAt);
        StatusText = StatusLabel(transaction.Status);
        TypeLabel = TypeText(transaction.Type);
        FromText = DisplayWallet(transaction.PayerWallet, "系统");
        ToText = DisplayWallet(transaction.PayeeWallet, "系统");
        RemarksText = string.IsNullOrWhiteSpace(transaction.Remarks) ? "无" : transaction.Remarks!;
        LifecycleText = BuildLifecycleText(transaction);
        CanRespond = IsIncome && transaction.Status is 0 or 1;
    }

    public SnWalletTransaction Transaction { get; }

    public Guid Id { get; }

    public bool IsIncome { get; }

    public bool CanRespond { get; }

    public string Title { get; }

    public string SubtitleText { get; }

    public string AmountText { get; }

    public string TimeText { get; }

    public string FullTimeText { get; }

    public string StatusText { get; }

    public string TypeLabel { get; }

    public string DirectionText { get; }

    public string FromText { get; }

    public string ToText { get; }

    public string RemarksText { get; }

    public string LifecycleText { get; }

    public string IconGlyph { get; }

    public Brush AmountForeground { get; }

    public Brush IconForeground { get; }

    public Brush IconBackground { get; }

    private static string DisplayWallet(SnWallet? wallet, string fallback)
    {
        var account = wallet?.Account;
        if (!string.IsNullOrWhiteSpace(account?.Nick))
        {
            return account.Nick!;
        }

        if (!string.IsNullOrWhiteSpace(account?.Name))
        {
            return $"@{account.Name}";
        }

        if (!string.IsNullOrWhiteSpace(wallet?.Name))
        {
            return wallet.Name!;
        }

        if (!string.IsNullOrWhiteSpace(wallet?.PublicId))
        {
            return wallet.PublicId!;
        }

        return fallback;
    }

    private static string TypeText(int type)
        => type switch
        {
            0 => "转账",
            1 => "支付",
            _ => $"交易 #{type}",
        };

    private static string StatusLabel(int status)
        => status switch
        {
            0 => "待确认",
            1 => "冻结中",
            2 => "已确认",
            3 => "已退款",
            4 => "已取消",
            _ => "未知状态",
        };

    private static string FormatTime(DateTimeOffset? time)
    {
        if (time is null)
        {
            return string.Empty;
        }

        var local = time.Value.ToLocalTime();
        var now = DateTimeOffset.Now;
        if (local.Date == now.Date)
        {
            return local.ToString("HH:mm");
        }

        return local.Year == now.Year
            ? local.ToString("MM-dd HH:mm")
            : local.ToString("yyyy-MM-dd");
    }

    private static string FormatFullTime(DateTimeOffset? time)
        => time?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "未知";

    private static string BuildLifecycleText(SnWalletTransaction transaction)
    {
        var parts = new List<string>();
        if (transaction.IsFrozen)
        {
            parts.Add("资金冻结");
        }

        if (transaction.RequireConfirmation)
        {
            parts.Add("需要收款方确认");
        }

        if (transaction.ExpiresAt is { } expiresAt)
        {
            parts.Add($"{expiresAt.ToLocalTime():MM-dd HH:mm} 到期");
        }

        if (transaction.ConfirmedAt is { } confirmedAt)
        {
            parts.Add($"{confirmedAt.ToLocalTime():MM-dd HH:mm} 确认");
        }

        return parts.Count == 0 ? "标准交易" : string.Join(" · ", parts);
    }
}
