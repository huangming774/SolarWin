using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using SolarWin.Models;
using SolarWin.Services;

namespace SolarWin.ViewModels;

public partial class WalletViewModel : ObservableObject
{
    private const int PageSize = 20;
    private const int MaxTransactions = 150;
    private const int StatsPeriodDays = 30;
    private readonly ISolarApiClient _api;

    private Guid? _walletId;
    private int _offset;
    private bool _hasMore;
    private int _loadVersion;
    private string? _direction;

    public WalletViewModel(ISolarApiClient api)
    {
        _api = api;
    }

    public ObservableCollection<SnWallet> Wallets { get; } = [];

    public ObservableCollection<SnWalletPocket> Pockets { get; } = [];

    public ObservableCollection<WalletTransactionItemViewModel> Transactions { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(EmptyVisibility))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsLoadingMore { get; set; }

    [ObservableProperty]
    public partial bool IsMutating { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(EmptyVisibility))]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActionMessage))]
    public partial string? ActionMessage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWallet))]
    [NotifyPropertyChangedFor(nameof(WalletContentVisibility))]
    [NotifyPropertyChangedFor(nameof(NoWalletVisibility))]
    [NotifyPropertyChangedFor(nameof(SetDefaultVisibility))]
    public partial SnWallet? SelectedWallet { get; set; }

    [ObservableProperty]
    public partial SnWalletPocket? SelectedPocket { get; set; }

    [ObservableProperty]
    public partial int TransactionFilterIndex { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TitleText { get; set; } = "钱包";

    [ObservableProperty]
    public partial string WalletKindText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BalanceText { get; set; } = "0";

    [ObservableProperty]
    public partial string CurrencyText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TotalBalanceText { get; set; } = "0";

    [ObservableProperty]
    public partial string HeldBalanceText { get; set; } = "0";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WalletMetaVisibility))]
    public partial string WalletMetaText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PublicIdButtonText { get; set; } = "启用公开 ID";

    [ObservableProperty]
    public partial string IncomeText { get; set; } = "—";

    [ObservableProperty]
    public partial string OutgoingText { get; set; } = "—";

    [ObservableProperty]
    public partial string StatsCountText { get; set; } = "—";

    [ObservableProperty]
    public partial string StatsPeriodText { get; set; } = $"近 {StatsPeriodDays} 天";

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasActionMessage => !string.IsNullOrWhiteSpace(ActionMessage);

    public bool HasWallet => SelectedWallet is not null;

    public bool IsEmpty => HasWallet && !IsBusy && !HasError && Transactions.Count == 0;

    public Visibility EmptyVisibility => IsEmpty ? Visibility.Visible : Visibility.Collapsed;

    public Visibility WalletContentVisibility => HasWallet ? Visibility.Visible : Visibility.Collapsed;

    public Visibility NoWalletVisibility => HasWallet || IsBusy ? Visibility.Collapsed : Visibility.Visible;

    public Visibility WalletMetaVisibility => string.IsNullOrWhiteSpace(WalletMetaText)
        ? Visibility.Collapsed
        : Visibility.Visible;

    public Visibility SetDefaultVisibility => SelectedWallet is { IsPrimary: false }
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility LoadMoreVisibility => _hasMore && HasWallet
        ? Visibility.Visible
        : Visibility.Collapsed;

    [RelayCommand]
    private async Task LoadAsync()
    {
        var version = ++_loadVersion;
        var preferredWalletId = SelectedWallet?.Id ?? _walletId;

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            ActionMessage = null;

            var wallets = await _api.GetWalletsAsync();
            if (version != _loadVersion)
            {
                return;
            }

            Wallets.Clear();
            foreach (var wallet in wallets.DistinctBy(static wallet => wallet.Id))
            {
                Wallets.Add(wallet);
            }

            var selected = preferredWalletId is { } walletId
                ? Wallets.FirstOrDefault(wallet => wallet.Id == walletId)
                : null;
            selected ??= Wallets.FirstOrDefault(static wallet => wallet.IsPrimary) ?? Wallets.FirstOrDefault();

            if (selected is null)
            {
                ResetWalletState();
                return;
            }

            ApplyWallet(selected);
            await LoadSelectedWalletDataAsync(version);
        }
        catch (Exception ex)
        {
            SetError(ex, "无法加载钱包，请稍后重试。");
            StatusText = "加载失败";
        }
        finally
        {
            if (version == _loadVersion)
            {
                IsBusy = false;
                RaiseListStateChanged();
            }
        }
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (_walletId is null || IsLoadingMore || IsBusy || !_hasMore)
        {
            return;
        }

        try
        {
            IsLoadingMore = true;
            ErrorMessage = null;
            var page = await _api.GetTransactionsAsync(
                _walletId.Value,
                _offset,
                PageSize,
                _direction);
            AddTransactions(page, _walletId.Value);

            _offset += page.Count;
            _hasMore = page.Count >= PageSize && Transactions.Count < MaxTransactions;
            UpdateStatus();
        }
        catch (Exception ex)
        {
            SetError(ex, "无法加载更多交易记录。");
        }
        finally
        {
            IsLoadingMore = false;
            RaiseListStateChanged();
        }
    }

    public async Task SelectWalletAsync(SnWallet wallet)
    {
        ArgumentNullException.ThrowIfNull(wallet);
        if (_walletId == wallet.Id && SelectedWallet?.Id == wallet.Id)
        {
            return;
        }

        var version = ++_loadVersion;
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            ApplyWallet(wallet);
            await LoadSelectedWalletDataAsync(version);
        }
        catch (Exception ex)
        {
            SetError(ex, "无法切换钱包。");
            StatusText = "加载失败";
        }
        finally
        {
            if (version == _loadVersion)
            {
                IsBusy = false;
                RaiseListStateChanged();
            }
        }
    }

    public async Task SelectPocketAsync(SnWalletPocket pocket)
    {
        ArgumentNullException.ThrowIfNull(pocket);
        if (string.Equals(SelectedPocket?.Currency, pocket.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SelectedPocket = pocket;
        ApplyPocket(pocket);
        if (_walletId is null)
        {
            return;
        }

        try
        {
            ErrorMessage = null;
            var stats = await _api.GetWalletStatsAsync(_walletId.Value, pocket.Currency ?? string.Empty, StatsPeriodDays);
            ApplyStats(stats, pocket.Currency);
        }
        catch
        {
            ResetStats("统计暂不可用");
        }
    }

    public async Task SetTransactionFilterAsync(int filterIndex)
    {
        filterIndex = Math.Clamp(filterIndex, 0, 2);
        if (TransactionFilterIndex == filterIndex && Transactions.Count > 0)
        {
            return;
        }

        TransactionFilterIndex = filterIndex;
        _direction = filterIndex switch
        {
            1 => "income",
            2 => "outcome",
            _ => null,
        };

        if (_walletId is null)
        {
            return;
        }

        var version = ++_loadVersion;
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            await LoadTransactionsAsync(_walletId.Value, version);
        }
        catch (Exception ex)
        {
            SetError(ex, "无法筛选交易记录。");
        }
        finally
        {
            if (version == _loadVersion)
            {
                IsBusy = false;
                RaiseListStateChanged();
            }
        }
    }

    public async Task<bool> CreateWalletAsync(string? name)
    {
        try
        {
            IsMutating = true;
            ErrorMessage = null;
            var created = await _api.CreateWalletAsync(new CreateWalletRequest
            {
                Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
            });
            SelectedWallet = created;
            _walletId = created.Id;
            ActionMessage = "钱包已创建。";
            await LoadAsync();
            ActionMessage = "钱包已创建。";
            return true;
        }
        catch (Exception ex)
        {
            SetError(ex, "创建钱包失败。");
            return false;
        }
        finally
        {
            IsMutating = false;
        }
    }

    [RelayCommand]
    private async Task SetDefaultAsync()
    {
        if (_walletId is null || SelectedWallet?.IsPrimary == true)
        {
            return;
        }

        try
        {
            IsMutating = true;
            ErrorMessage = null;
            await _api.SetDefaultWalletAsync(_walletId.Value);
            await LoadAsync();
            ActionMessage = "已设为主钱包。";
        }
        catch (Exception ex)
        {
            SetError(ex, "设置主钱包失败。");
        }
        finally
        {
            IsMutating = false;
        }
    }

    [RelayCommand]
    private async Task TogglePublicIdAsync()
    {
        if (_walletId is null || SelectedWallet is null)
        {
            return;
        }

        var enable = string.IsNullOrWhiteSpace(SelectedWallet.PublicId);
        try
        {
            IsMutating = true;
            ErrorMessage = null;
            var updated = await _api.SetWalletPublicIdEnabledAsync(_walletId.Value, enable);
            ReplaceWallet(updated);
            ApplyWallet(updated, keepPocketCurrency: true);
            ActionMessage = enable ? "公开 ID 已启用，可用于收款。" : "公开 ID 已关闭。";
        }
        catch (Exception ex)
        {
            SetError(ex, enable ? "启用公开 ID 失败。" : "关闭公开 ID 失败。");
        }
        finally
        {
            IsMutating = false;
        }
    }

    public async Task<bool> TransferAsync(
        decimal amount,
        string currency,
        string recipient,
        bool usePublicId,
        string pinCode,
        string? remark,
        bool freeze,
        bool requireConfirmation)
    {
        if (_walletId is null)
        {
            ErrorMessage = "请先选择付款钱包。";
            return false;
        }

        try
        {
            IsMutating = true;
            ErrorMessage = null;

            Guid? payeeAccountId = null;
            string? payeePublicId = null;
            if (usePublicId)
            {
                payeePublicId = recipient.Trim().ToUpperInvariant();
            }
            else
            {
                var accountName = recipient.Trim().TrimStart('@');
                var account = await _api.GetAccountByNameAsync(accountName);
                payeeAccountId = account.Id;
            }

            await _api.TransferWalletAsync(new WalletTransferRequest
            {
                Amount = amount,
                Currency = currency,
                PinCode = pinCode,
                PayerWalletId = _walletId,
                PayeeAccountId = payeeAccountId,
                PayeePublicId = payeePublicId,
                Remark = string.IsNullOrWhiteSpace(remark) ? null : remark.Trim(),
                Freeze = freeze,
                RequireConfirmation = requireConfirmation,
            });

            await LoadAsync();
            ActionMessage = requireConfirmation
                ? "转账已创建，正在等待收款方确认。"
                : freeze
                    ? "转账已创建，资金将在冻结期结束后结算。"
                    : "转账成功。";
            return true;
        }
        catch (Exception ex)
        {
            SetError(ex, "转账失败，请检查收款方、余额与钱包 PIN。");
            return false;
        }
        finally
        {
            IsMutating = false;
        }
    }

    public async Task<bool> RespondToTransactionAsync(Guid transactionId, bool accept)
    {
        try
        {
            IsMutating = true;
            ErrorMessage = null;
            if (accept)
            {
                await _api.ConfirmWalletTransactionAsync(transactionId);
            }
            else
            {
                await _api.RejectWalletTransactionAsync(transactionId);
            }

            await LoadAsync();
            ActionMessage = accept ? "交易已确认。" : "交易已拒绝，资金将退回付款方。";
            return true;
        }
        catch (Exception ex)
        {
            SetError(ex, accept ? "确认交易失败。" : "拒绝交易失败。");
            return false;
        }
        finally
        {
            IsMutating = false;
        }
    }

    private async Task LoadSelectedWalletDataAsync(int version)
    {
        if (_walletId is null)
        {
            return;
        }

        await LoadTransactionsAsync(_walletId.Value, version);
        if (version != _loadVersion || SelectedPocket?.Currency is not { Length: > 0 } currency)
        {
            ResetStats();
            return;
        }

        try
        {
            var stats = await _api.GetWalletStatsAsync(_walletId.Value, currency, StatsPeriodDays);
            if (version == _loadVersion)
            {
                ApplyStats(stats, currency);
            }
        }
        catch
        {
            if (version == _loadVersion)
            {
                ResetStats("统计暂不可用");
            }
        }
    }

    private async Task LoadTransactionsAsync(Guid walletId, int version)
    {
        Transactions.Clear();
        _offset = 0;
        _hasMore = true;

        var page = await _api.GetTransactionsAsync(walletId, 0, PageSize, _direction);
        if (version != _loadVersion)
        {
            return;
        }

        AddTransactions(page, walletId);
        _offset = page.Count;
        _hasMore = page.Count >= PageSize && Transactions.Count < MaxTransactions;
        UpdateStatus();
    }

    private void ApplyWallet(SnWallet wallet, bool keepPocketCurrency = false)
    {
        var previousCurrency = keepPocketCurrency ? SelectedPocket?.Currency : null;
        _walletId = wallet.Id;
        SelectedWallet = wallet;
        TitleText = string.IsNullOrWhiteSpace(wallet.Name) ? "未命名钱包" : wallet.Name!;
        WalletKindText = wallet.IsPrimary ? "主钱包" : "个人钱包";
        WalletMetaText = string.IsNullOrWhiteSpace(wallet.PublicId)
            ? $"钱包 ID · {wallet.Id.ToString("N")[..8]}"
            : $"公开 ID · {wallet.PublicId}";
        PublicIdButtonText = string.IsNullOrWhiteSpace(wallet.PublicId) ? "启用公开 ID" : "关闭公开 ID";

        Pockets.Clear();
        foreach (var pocket in wallet.Pockets.OrderBy(static pocket => pocket.Currency))
        {
            Pockets.Add(pocket);
        }

        var pocketToSelect = previousCurrency is null
            ? Pockets.FirstOrDefault()
            : Pockets.FirstOrDefault(pocket => string.Equals(pocket.Currency, previousCurrency, StringComparison.OrdinalIgnoreCase))
              ?? Pockets.FirstOrDefault();
        SelectedPocket = pocketToSelect;
        ApplyPocket(pocketToSelect);
        OnPropertyChanged(nameof(SetDefaultVisibility));
    }

    private void ApplyPocket(SnWalletPocket? pocket)
    {
        if (pocket is null)
        {
            BalanceText = "0";
            CurrencyText = "暂无资产";
            TotalBalanceText = "0";
            HeldBalanceText = "0";
            return;
        }

        BalanceText = pocket.AvailableAmount.ToString("0.##");
        CurrencyText = pocket.Currency ?? string.Empty;
        TotalBalanceText = $"{pocket.Amount:0.##} {pocket.Currency}";
        HeldBalanceText = $"{pocket.HeldAmount:0.##} {pocket.Currency}";
    }

    private void ApplyStats(SnWalletStats stats, string? currency)
    {
        IncomeText = $"+{stats.TotalIncome:0.##} {currency}";
        OutgoingText = $"-{Math.Abs(stats.TotalOutgoing):0.##} {currency}";
        StatsCountText = $"{stats.TotalTransactions:N0} 笔交易";
        StatsPeriodText = stats.PeriodBegin is { } begin && stats.PeriodEnd is { } end
            ? $"{begin.ToLocalTime():MM-dd} 至 {end.ToLocalTime():MM-dd}"
            : $"近 {StatsPeriodDays} 天";
    }

    private void ResetStats(string? periodText = null)
    {
        IncomeText = "—";
        OutgoingText = "—";
        StatsCountText = "—";
        StatsPeriodText = periodText ?? $"近 {StatsPeriodDays} 天";
    }

    private void ResetWalletState()
    {
        _walletId = null;
        SelectedWallet = null;
        SelectedPocket = null;
        Wallets.Clear();
        Pockets.Clear();
        Transactions.Clear();
        TitleText = "钱包";
        WalletKindText = string.Empty;
        WalletMetaText = string.Empty;
        BalanceText = "0";
        CurrencyText = string.Empty;
        TotalBalanceText = "0";
        HeldBalanceText = "0";
        StatusText = "尚未创建钱包";
        _hasMore = false;
        ResetStats();
        RaiseListStateChanged();
    }

    private void ReplaceWallet(SnWallet wallet)
    {
        var index = Wallets.ToList().FindIndex(existing => existing.Id == wallet.Id);
        if (index >= 0)
        {
            Wallets[index] = wallet;
        }
    }

    private void AddTransactions(IEnumerable<SnWalletTransaction> transactions, Guid walletId)
    {
        var remaining = MaxTransactions - Transactions.Count;
        foreach (var transaction in transactions.Take(Math.Max(0, remaining)))
        {
            Transactions.Add(new WalletTransactionItemViewModel(transaction, walletId));
        }
    }

    private void UpdateStatus()
    {
        var pendingCount = Transactions.Count(static transaction => transaction.CanRespond);
        var loaded = Transactions.Count >= MaxTransactions
            ? $"最近 {MaxTransactions} 笔 · 已达显示上限"
            : $"已加载 {Transactions.Count} 笔";
        StatusText = pendingCount > 0 ? $"{loaded} · {pendingCount} 笔待确认" : loaded;
    }

    private void SetError(Exception exception, string fallback)
    {
        ErrorMessage = exception is SolarApiException apiException && !string.IsNullOrWhiteSpace(apiException.Message)
            ? apiException.Message
            : fallback;
    }

    private void RaiseListStateChanged()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyVisibility));
        OnPropertyChanged(nameof(LoadMoreVisibility));
        OnPropertyChanged(nameof(WalletContentVisibility));
        OnPropertyChanged(nameof(NoWalletVisibility));
    }
}
