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
    private readonly ISolarApiClient _api;

    private Guid? _walletId;
    private int _offset;
    private bool _hasMore = true;

    public WalletViewModel(ISolarApiClient api)
    {
        _api = api;
    }

    public ObservableCollection<SnWallet> Wallets { get; } = [];
    public ObservableCollection<WalletTransactionItemViewModel> Transactions { get; } = [];

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsLoadingMore { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TitleText { get; set; } = "钱包";

    [ObservableProperty]
    public partial string BalanceText { get; set; } = "-";

    [ObservableProperty]
    public partial string WalletMetaText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PocketSummaryText { get; set; } = string.Empty;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsEmpty => !IsBusy && !HasError && Transactions.Count == 0;
    public Visibility EmptyVisibility => IsEmpty ? Visibility.Visible : Visibility.Collapsed;
    public bool HasMore => _hasMore;
    public Visibility LoadMoreVisibility => _hasMore ? Visibility.Visible : Visibility.Collapsed;

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            Wallets.Clear();
            Transactions.Clear();
            _offset = 0;
            _hasMore = true;

            var wallets = await _api.GetWalletsAsync().ConfigureAwait(true);
            foreach (var wallet in wallets)
            {
                Wallets.Add(wallet);
            }

            var selected = Wallets.FirstOrDefault(w => w.IsPrimary) ?? Wallets.FirstOrDefault();
            if (selected is null)
            {
                TitleText = "钱包";
                BalanceText = "暂无钱包";
                WalletMetaText = string.Empty;
                PocketSummaryText = string.Empty;
                StatusText = "未创建钱包";
                return;
            }

            await SelectWalletAsync(selected).ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.Message;
            StatusText = "加载失败";
        }
        finally
        {
            IsBusy = false;
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
            var page = await _api.GetTransactionsAsync(_walletId.Value, _offset, PageSize).ConfigureAwait(true);
            foreach (var tx in page)
            {
                Transactions.Add(new WalletTransactionItemViewModel(tx, _walletId.Value));
            }

            _offset += page.Count;
            _hasMore = page.Count >= PageSize;
            UpdateStatus();
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(LoadMoreVisibility));
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    public async Task SelectWalletAsync(SnWallet wallet)
    {
        _walletId = wallet.Id;
        TitleText = wallet.Name ?? "钱包";
        WalletMetaText = string.IsNullOrWhiteSpace(wallet.PublicId)
            ? wallet.Id.ToString("N")[..8]
            : wallet.PublicId;
        PocketSummaryText = BuildPocketSummary(wallet);
        BalanceText = BuildBalance(wallet);

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            Transactions.Clear();
            _offset = 0;
            _hasMore = true;

            var page = await _api.GetTransactionsAsync(wallet.Id, 0, PageSize).ConfigureAwait(true);
            foreach (var tx in page)
            {
                Transactions.Add(new WalletTransactionItemViewModel(tx, wallet.Id));
            }

            _offset = page.Count;
            _hasMore = page.Count >= PageSize;
            UpdateStatus();
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(EmptyVisibility));
            OnPropertyChanged(nameof(LoadMoreVisibility));
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.Message;
            StatusText = "加载失败";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string BuildBalance(SnWallet wallet)
    {
        if (wallet.Pockets.Count == 0)
        {
            return "0";
        }

        var pocket = wallet.Pockets[0];
        return $"{pocket.AvailableAmount:0.##} {pocket.Currency}";
    }

    private static string BuildPocketSummary(SnWallet wallet)
    {
        if (wallet.Pockets.Count == 0)
        {
            return "暂无资产";
        }

        return string.Join(" · ", wallet.Pockets.Select(p => $"{p.AvailableAmount:0.##} {p.Currency}"));
    }

    private void UpdateStatus()
    {
        StatusText = $"共 {Transactions.Count} 笔";
    }
}
