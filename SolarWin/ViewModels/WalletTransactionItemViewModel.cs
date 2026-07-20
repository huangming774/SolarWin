using SolarWin.Models;

namespace SolarWin.ViewModels;

public sealed class WalletTransactionItemViewModel
{
    public WalletTransactionItemViewModel(SnWalletTransaction transaction, Guid walletId)
    {
        Transaction = transaction;
        var incoming = transaction.PayeeWalletId == walletId;
        var sign = incoming ? "+" : "-";
        var amount = Math.Abs(transaction.Amount);

        Title = string.IsNullOrWhiteSpace(transaction.Remarks)
            ? TypeText(transaction.Type)
            : transaction.Remarks!;
        AmountText = $"{sign}{amount:0.##} {transaction.Currency}";
        CurrencyText = transaction.Currency ?? string.Empty;
        TimeText = FormatTime(transaction.CreatedAt);
        StatusText = StatusLabel(transaction.Status);
        DirectionText = incoming ? "收入" : "支出";
    }

    public SnWalletTransaction Transaction { get; }

    public string Title { get; }

    public string AmountText { get; }

    public string CurrencyText { get; }

    public string TimeText { get; }

    public string StatusText { get; }

    public string DirectionText { get; }

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
