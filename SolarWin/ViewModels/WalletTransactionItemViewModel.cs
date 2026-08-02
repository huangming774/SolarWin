using Microsoft.UI.Xaml.Media;
using SolarWin.Models;

namespace SolarWin.ViewModels;

public sealed class WalletTransactionItemViewModel
{
    public WalletTransactionItemViewModel(SnWalletTransaction transaction, Guid walletId)
    {
        Id = transaction.Id;
        var incoming = transaction.PayeeWalletId == walletId;
        var sign = incoming ? "+" : "-";
        var amount = Math.Abs(transaction.Amount);

        DirectionText = incoming ? "收入" : "支出";
        IconGlyph = incoming ? "\uE896" : "\uE898";
        AmountForeground = incoming ? IncomingForeground : OutgoingForeground;
        IconForeground = AmountForeground;
        IconBackground = incoming ? IncomingBackground : OutgoingBackground;

        Title = string.IsNullOrWhiteSpace(transaction.Remarks)
            ? TypeText(transaction.Type)
            : transaction.Remarks!;
        AmountText = $"{sign}{amount:0.##} {transaction.Currency}";
        TimeText = FormatTime(transaction.CreatedAt);
        StatusText = StatusLabel(transaction.Status);
    }

    public Guid Id { get; }

    public string Title { get; }

    public string AmountText { get; }

    public string TimeText { get; }

    public string StatusText { get; }

    public string DirectionText { get; }

    public string IconGlyph { get; }

    public Brush AmountForeground { get; }

    public Brush IconForeground { get; }

    public Brush IconBackground { get; }

    private static readonly Brush IncomingForeground = new SolidColorBrush(
        Microsoft.UI.ColorHelper.FromArgb(255, 16, 137, 85));
    private static readonly Brush IncomingBackground = new SolidColorBrush(
        Microsoft.UI.ColorHelper.FromArgb(30, 16, 185, 129));
    private static readonly Brush OutgoingForeground = new SolidColorBrush(
        Microsoft.UI.ColorHelper.FromArgb(255, 220, 74, 74));
    private static readonly Brush OutgoingBackground = new SolidColorBrush(
        Microsoft.UI.ColorHelper.FromArgb(30, 239, 68, 68));

    private static string TypeText(int type)
        => type switch
        {
            0 => "转账",
            1 => "充值",
            2 => "消费",
            3 => "退款",
            4 => "奖励",
            _ => $"交易 #{type}",
        };

    private static string StatusLabel(int status)
        => status switch
        {
            0 => "待处理",
            1 => "冻结中",
            2 => "已确认",
            3 => "已退款",
            4 => "已取消",
            _ => string.Empty,
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

        if (local.Year == now.Year)
        {
            return local.ToString("MM-dd HH:mm");
        }

        return local.ToString("yyyy-MM-dd");
    }
}
