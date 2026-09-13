using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using SolarWin.Models;
using SolarWin.ViewModels;

namespace SolarWin.Views;

public sealed partial class WalletPage : Page
{
    public WalletViewModel ViewModel { get; }

    public WalletPage()
    {
        ViewModel = App.Services.GetRequiredService<WalletViewModel>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (ViewModel.LoadCommand.CanExecute(null))
        {
            ViewModel.LoadCommand.Execute(null);
        }
    }

    private async void WalletSelector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { SelectedItem: SnWallet wallet })
        {
            await ViewModel.SelectWalletAsync(wallet);
        }
    }

    private async void PocketSelector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { SelectedItem: SnWalletPocket pocket })
        {
            await ViewModel.SelectPocketAsync(pocket);
        }
    }

    private async void TransactionFilter_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton button || !int.TryParse(button.Tag?.ToString(), out var index))
        {
            return;
        }

        AllFilterButton.IsChecked = index == 0;
        IncomeFilterButton.IsChecked = index == 1;
        OutgoingFilterButton.IsChecked = index == 2;
        await ViewModel.SetTransactionFilterAsync(index);
    }

    private async void CreateWallet_OnClick(object sender, RoutedEventArgs e)
    {
        var nameBox = new TextBox
        {
            Header = "钱包名称（可选）",
            PlaceholderText = "例如：日常钱包",
            MaxLength = 64,
        };
        var dialog = new ContentDialog
        {
            Title = "创建钱包",
            Content = nameBox,
            PrimaryButtonText = "创建",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await ViewModel.CreateWalletAsync(nameBox.Text);
        }
    }

    private async void Transfer_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedWallet is null || ViewModel.SelectedPocket is null)
        {
            return;
        }

        var amountBox = new NumberBox
        {
            Header = "金额",
            Minimum = 0,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Hidden,
            PlaceholderText = "0.00",
        };
        var currencyBox = new ComboBox
        {
            Header = "币种",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            DisplayMemberPath = nameof(SnWalletPocket.DisplayName),
            ItemsSource = ViewModel.Pockets,
            SelectedItem = ViewModel.SelectedPocket,
        };
        var recipientKindBox = new ComboBox
        {
            Header = "收款方类型",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = new[] { "Solar Network 账号名", "钱包公开 ID" },
            SelectedIndex = 0,
        };
        var recipientBox = new TextBox
        {
            Header = "收款方",
            PlaceholderText = "账号名或公开 ID",
            MaxLength = 128,
        };
        recipientKindBox.SelectionChanged += (_, _) =>
        {
            recipientBox.PlaceholderText = recipientKindBox.SelectedIndex == 1
                ? "例如：ABCD1234"
                : "例如：little_sheep";
        };
        var remarkBox = new TextBox
        {
            Header = "备注（可选）",
            PlaceholderText = "这笔转账的用途",
            MaxLength = 200,
        };
        var pinBox = new PasswordBox
        {
            Header = "钱包 PIN",
            PlaceholderText = "输入 6 位数字 PIN",
            MaxLength = 6,
        };
        var freezeBox = new CheckBox
        {
            Content = "冻结 24 小时后结算",
        };
        var confirmationBox = new CheckBox
        {
            Content = "需要收款方确认",
        };
        var errorText = new TextBlock
        {
            Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 196, 43, 28)),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
        };
        var panel = new StackPanel { Spacing = 12, MinWidth = 360 };
        panel.Children.Add(amountBox);
        panel.Children.Add(currencyBox);
        panel.Children.Add(recipientKindBox);
        panel.Children.Add(recipientBox);
        panel.Children.Add(remarkBox);
        panel.Children.Add(pinBox);
        panel.Children.Add(freezeBox);
        panel.Children.Add(confirmationBox);
        panel.Children.Add(errorText);

        var dialog = new ContentDialog
        {
            Title = $"从“{ViewModel.TitleText}”转账",
            Content = new ScrollViewer
            {
                MaxHeight = 560,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = panel,
            },
            PrimaryButtonText = "确认转账",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        dialog.PrimaryButtonClick += (_, args) =>
        {
            decimal ignoredAmount;
            SnWalletPocket? ignoredPocket;
            errorText.Text = ValidateTransfer(
                amountBox,
                currencyBox,
                recipientBox,
                pinBox,
                out ignoredAmount,
                out ignoredPocket);
            args.Cancel = !string.IsNullOrEmpty(errorText.Text);
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        _ = ValidateTransfer(
            amountBox,
            currencyBox,
            recipientBox,
            pinBox,
            out var amount,
            out var pocket);
        await ViewModel.TransferAsync(
            amount,
            pocket!.Currency!,
            recipientBox.Text,
            recipientKindBox.SelectedIndex == 1,
            pinBox.Password,
            remarkBox.Text,
            freezeBox.IsChecked == true,
            confirmationBox.IsChecked == true);
    }

    private async void Transactions_OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not WalletTransactionItemViewModel item)
        {
            return;
        }

        var amount = new TextBlock
        {
            Text = item.AmountText,
            FontSize = 28,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = item.AmountForeground,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        var status = new TextBlock
        {
            Text = $"{item.StatusText} · {item.LifecycleText}",
            FontSize = 12,
            Opacity = 0.65,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        var technicalId = new TextBox
        {
            Header = "交易 ID",
            Text = item.Id.ToString("D"),
            IsReadOnly = true,
            FontFamily = new FontFamily("Consolas"),
        };
        var panel = new StackPanel { Spacing = 12, MinWidth = 380 };
        panel.Children.Add(amount);
        panel.Children.Add(status);
        panel.Children.Add(CreateDetailRow("时间", item.FullTimeText));
        panel.Children.Add(CreateDetailRow("类型", item.TypeLabel));
        panel.Children.Add(CreateDetailRow("付款方", item.FromText));
        panel.Children.Add(CreateDetailRow("收款方", item.ToText));
        panel.Children.Add(CreateDetailRow("备注", item.RemarksText));
        panel.Children.Add(technicalId);

        var dialog = new ContentDialog
        {
            Title = "交易详情",
            Content = new ScrollViewer
            {
                MaxHeight = 520,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = panel,
            },
            CloseButtonText = "关闭",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (item.CanRespond)
        {
            dialog.PrimaryButtonText = "确认收款";
            dialog.SecondaryButtonText = "拒绝并退款";
            dialog.DefaultButton = ContentDialogButton.Primary;
        }

        var result = await dialog.ShowAsync();
        if (!item.CanRespond)
        {
            return;
        }

        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.RespondToTransactionAsync(item.Id, accept: true);
        }
        else if (result == ContentDialogResult.Secondary)
        {
            await ViewModel.RespondToTransactionAsync(item.Id, accept: false);
        }
    }

    private static string ValidateTransfer(
        NumberBox amountBox,
        ComboBox currencyBox,
        TextBox recipientBox,
        PasswordBox pinBox,
        out decimal amount,
        out SnWalletPocket? pocket)
    {
        amount = 0;
        pocket = currencyBox.SelectedItem as SnWalletPocket;
        if (double.IsNaN(amountBox.Value) || double.IsInfinity(amountBox.Value) || amountBox.Value <= 0)
        {
            return "请输入大于 0 的转账金额。";
        }

        amount = Convert.ToDecimal(amountBox.Value);
        if (decimal.Round(amount, 2) != amount)
        {
            return "转账金额最多保留两位小数。";
        }

        if (pocket is null || string.IsNullOrWhiteSpace(pocket.Currency))
        {
            return "请选择转账币种。";
        }

        if (amount > pocket.AvailableAmount)
        {
            return $"可用余额不足，当前可用 {pocket.AvailableAmount:0.##} {pocket.Currency}。";
        }

        if (string.IsNullOrWhiteSpace(recipientBox.Text))
        {
            return "请输入收款方。";
        }

        if (pinBox.Password.Length != 6 || pinBox.Password.Any(character => !char.IsDigit(character)))
        {
            return "钱包 PIN 必须是 6 位数字。";
        }

        return string.Empty;
    }

    private static Grid CreateDetailRow(string label, string value)
    {
        var grid = new Grid { ColumnSpacing = 16 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var labelBlock = new TextBlock
        {
            Text = label,
            Opacity = 0.58,
            FontSize = 12,
        };
        var valueBlock = new TextBlock
        {
            Text = value,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        Grid.SetColumn(valueBlock, 1);
        grid.Children.Add(labelBlock);
        grid.Children.Add(valueBlock);
        return grid;
    }
}
