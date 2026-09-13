using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SolarWin.ViewModels;

namespace SolarWin.Views;

public sealed partial class OrderPage : Page
{
    public OrderViewModel ViewModel { get; }

    public OrderPage()
    {
        ViewModel = App.Services.GetRequiredService<OrderViewModel>();
        InitializeComponent();
    }

    private void TokenBox_OnPasswordChanged(object sender, RoutedEventArgs e)
        => ViewModel.Token = ((PasswordBox)sender).Password;

    private async void ProductImage_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            // Tag="{x:Bind}" 即模板数据项本身，不依赖 DataContext 语义（x:Bind 模板内 DataContext 不可靠）
            if (sender is not FrameworkElement { Tag: OrderProduct product })
            {
                ViewModel.StatusText = "规格打开失败：未取到商品数据";
                return;
            }
            if (ViewModel.SelectedShop is null)
            {
                ViewModel.StatusText = "请先选择门店";
                return;
            }
            ViewModel.StatusText = $"正在读取「{product.Name}」的可选规格…";
            await OpenCustomizeDialogAsync(product);
        }
        catch (Exception ex)
        {
            ViewModel.StatusText = $"打开规格对话框失败：{ex.Message}";
        }
    }

    private async Task OpenCustomizeDialogAsync(OrderProduct product)
    {
        var panel = new StackPanel { Spacing = 12, MinWidth = 380 };
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = $"{product.Name} · 选择规格",
            Content = new ScrollViewer { Content = panel, MaxHeight = 560, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
            PrimaryButtonText = "加入购物车",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
        };

        var closed = false;
        var rendering = false;
        OrderCustomizeState? state = null;
        var radios = new List<RadioButton>();

        void RenderLoading()
        {
            panel.Children.Clear();
            panel.Children.Add(new ProgressRing { IsActive = true, Width = 32, Height = 32, HorizontalAlignment = HorizontalAlignment.Center });
            panel.Children.Add(new TextBlock { Text = "正在读取可选规格…", HorizontalAlignment = HorizontalAlignment.Center, Opacity = 0.7 });
        }

        void RenderState()
        {
            if (state is null) return;
            rendering = true;
            try
            {
                panel.Children.Clear();
                radios.Clear();
                if (state.PictureSource is { } picture)
                {
                    panel.Children.Add(new Image { Source = picture, Height = 140, Stretch = Stretch.Uniform });
                }
                if (state.Groups.Count == 0)
                {
                    panel.Children.Add(new TextBlock { Text = "该商品没有可选规格，将按默认配方下单", Opacity = 0.7, TextWrapping = TextWrapping.Wrap });
                }
                foreach (var group in state.Groups)
                {
                    panel.Children.Add(new TextBlock { Text = group.Name, FontWeight = FontWeights.SemiBold });
                    var groupPanel = new StackPanel { Spacing = 4 };
                    foreach (var option in group.Options)
                    {
                        var radio = new RadioButton
                        {
                            Content = option.Label,
                            IsChecked = ReferenceEquals(option, group.SelectedOption),
                            GroupName = $"attr{group.AttributeId}",
                            Tag = (group, option),
                            IsEnabled = !state.IsSwitching && option.CanSelected,
                        };
                        radio.Checked += RadioOption_OnChecked;
                        radios.Add(radio);
                        groupPanel.Children.Add(radio);
                    }
                    panel.Children.Add(groupPanel);
                }
                panel.Children.Add(new TextBlock { Text = state.PriceText, FontSize = 18, FontWeight = FontWeights.SemiBold });
                panel.Children.Add(new TextBlock { Text = state.Summary, Opacity = 0.7, TextWrapping = TextWrapping.Wrap });
            }
            finally
            {
                rendering = false;
            }
        }

        async void RadioOption_OnChecked(object sender, RoutedEventArgs args)
        {
            if (rendering || closed || state is null) return;
            if (sender is not RadioButton { Tag: (OrderAttrGroupState group, OrderAttrOptionState option) }) return;
            if (ReferenceEquals(option, group.SelectedOption)) return;

            try
            {
                dialog.IsPrimaryButtonEnabled = false;
                foreach (var item in radios) item.IsEnabled = false;
                // 成功与失败都以 state 为准整体重渲染（服务端可能重映射其它组，失败时状态未变）
                await ViewModel.SwitchCustomizeOptionAsync(state, group, option);
            }
            finally
            {
                dialog.IsPrimaryButtonEnabled = true;
                if (!closed) RenderState();
            }
        }

        RenderLoading();
        var showTask = dialog.ShowAsync().AsTask();
        state = await ViewModel.LoadCustomizeAsync(product);
        if (state is null)
        {
            dialog.Hide();
            await showTask;
            return;
        }

        RenderState();
        var result = await showTask;
        closed = true;
        if (result == ContentDialogResult.Primary) ViewModel.AddCustomizedToCart(state);
    }

    private async void CreateOrder_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Cart.Count == 0)
        {
            ViewModel.StatusText = "请先加入商品";
            return;
        }
        if (!ViewModel.HasValidPreview)
        {
            ViewModel.StatusText = "请先预览最终价格；购物车变化后需要重新预览";
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "确认创建真实订单？",
            Content = ViewModel.ConfirmationText,
            PrimaryButtonText = "创建并生成支付二维码",
            CloseButtonText = "返回检查",
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary && ViewModel.CreateOrderCommand.CanExecute(null))
        {
            await ViewModel.CreateOrderCommand.ExecuteAsync(null);
        }
    }

    private async void OpenPayment_OnClick(object sender, RoutedEventArgs e)
    {
        if (Uri.TryCreate(ViewModel.PayUrl, UriKind.Absolute, out var paymentUri) && paymentUri.Scheme is "https" or "http")
        {
            var launched = await Windows.System.Launcher.LaunchUriAsync(paymentUri);
            if (!launched) ViewModel.StatusText = "无法打开支付二维码，请直接使用页面中的二维码扫码支付";
        }
    }

    private async void CancelOrder_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.OrderId))
        {
            ViewModel.StatusText = "没有可取消的订单";
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "取消这笔订单？",
            Content = $"订单 {ViewModel.OrderId}\n仅未支付订单可取消；已支付订单请到瑞幸 App 处理。",
            PrimaryButtonText = "取消订单",
            CloseButtonText = "保留订单",
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary && ViewModel.CancelOrderCommand.CanExecute(null))
        {
            await ViewModel.CancelOrderCommand.ExecuteAsync(null);
        }
    }
}
