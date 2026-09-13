using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using SolarWin.Helpers;
using SolarWin.Services;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace SolarWin.ViewModels;

public partial class OrderProduct : ObservableObject
{
    public int ProductId { get; init; }
    public string SkuCode { get; init; } = "";
    public string Name { get; init; } = "";
    public string? PictureUrl { get; init; }

    /// <summary>已选属性摘要（如“超大杯 / 热 / 少甜”）；空串表示直接加入的默认规格。</summary>
    public string AttrSummary { get; init; } = "";
    public bool HasAttrSummary => AttrSummary.Length > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PriceText))]
    public partial double Price { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AmountText))]
    public partial int Amount { get; set; } = 1;

    public string PriceText => Price > 0 ? $"¥{Price:0.00}" : "价格以预览为准";
    public string AmountText => $"数量 {Amount}";

    public BitmapImage? PictureSource
        => Uri.TryCreate(PictureUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? new BitmapImage(uri)
            : null;
}

public sealed class OrderShop
{
    public int DeptId { get; init; }
    public string Name { get; init; } = "";
    public string Address { get; init; } = "";
    public double Longitude { get; init; }
    public double Latitude { get; init; }
    public string WorkStatus { get; init; } = "";
    public bool IsOpen => !string.Equals(WorkStatus, "打烊中", StringComparison.Ordinal);
    public string DisplayText => IsOpen ? $"{Name}  {Address}" : $"{Name}（打烊中）  {Address}";
    /// <summary>门店名 + 地址（地址缺失时只显示门店名），用于确认弹窗/支付摘要里点明自提地点。</summary>
    public string NameWithAddress => Address.Length > 0 ? $"{Name}（{Address}）" : Name;
}

public partial class OrderViewModel : ObservableObject
{
    private readonly ILuckinMcpService _mcp;
    private readonly IWeatherService _weather;
    private JsonElement? _previewCouponCodeList;
    private string? _previewSignature;
    private double _previewInitialPrice;
    private double _previewPrivilegeMoney;
    private double _previewDiscountPrice;
    private long _previewAboutTime;
    private CancellationTokenSource? _opCts;
    private int _shopGeneration;
    private CancellationTokenSource? _menuCts;
    // 会话内 (deptId, productId) → 详情解析快照缓存，避免 FillPricesAsync 与 LoadCustomizeAsync
    // 对同参 queryProductDetailInfo 重复请求。缓存 Task（而非值）以便 ProductImage_OnClick
    // 绕过 RunAsync/IsBusy 门点开规格时合并并发的同参请求为一个网络往返。
    private readonly System.Collections.Concurrent.ConcurrentDictionary<(int deptId, int productId), Task<LuckinProductSpec?>> _specCache = new();

    public OrderViewModel(ILuckinMcpService mcp, IWeatherService weather)
    {
        _mcp = mcp;
        _weather = weather;
    }

    public ObservableCollection<OrderShop> Shops { get; } = [];
    public ObservableCollection<OrderProduct> Products { get; } = [];
    public ObservableCollection<OrderProduct> Cart { get; } = [];

    [ObservableProperty] public partial string Token { get; set; } = "";
    [ObservableProperty] public partial string SearchText { get; set; } = "";
    [ObservableProperty] public partial string LocationText { get; set; } = "";
    [ObservableProperty] public partial string ShopKeyword { get; set; } = "";
    [ObservableProperty] public partial OrderShop? SelectedShop { get; set; }
    [ObservableProperty] public partial string StatusText { get; set; } = "请输入密钥后开始点餐";
    [ObservableProperty] public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PayVisibility))]
    [NotifyPropertyChangedFor(nameof(PaySource))]
    [NotifyPropertyChangedFor(nameof(PayImageSource))]
    [NotifyPropertyChangedFor(nameof(CanOpenPayInBrowser))]
    public partial string? PayUrl { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PayVisibility))]
    [NotifyPropertyChangedFor(nameof(PayImageSource))]
    public partial BitmapImage? PayQrImage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOrderId))]
    public partial string OrderId { get; set; } = "";
    public bool HasOrderId => OrderId.Trim().Length > 0;
    [ObservableProperty] public partial string PaymentSummary { get; set; } = "";

    public Visibility PayVisibility => PayImageSource is null ? Visibility.Collapsed : Visibility.Visible;
    public BitmapImage? PaySource
        => Uri.TryCreate(PayUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? new BitmapImage(uri)
            : null;
    public BitmapImage? PayImageSource => PaySource ?? PayQrImage;
    public bool CanOpenPayInBrowser => PaySource is not null;
    public double CartTotal => Cart.Sum(x => x.Price * x.Amount);
    public string CartTotalText => $"商品估价 ¥{CartTotal:0.00}";
    public bool HasValidPreview => _previewSignature == BuildOrderSignature();
    public string ConfirmationText =>
        $"自提门店：{SelectedShop?.NameWithAddress}\n商品：{Cart.Count} 项\n原价：¥{_previewInitialPrice:0.00}\n优惠：¥{_previewPrivilegeMoney:0.00}\n应付：¥{_previewDiscountPrice:0.00}\n确认后将创建真实订单（到店自取，无配送），并生成支付二维码。";

    partial void OnSelectedShopChanged(OrderShop? value)
    {
        CancelMenuLoad();
        _specCache.Clear();
        Products.Clear();
        Cart.Clear();
        OnCartChanged();
    }

    /// <summary>登出 / 跨账号切换时调用：清空全部会话状态。
    /// 因为 OrderViewModel 是 Singleton，会话数据跨导航保留，必须显式清理避免下一账号看到上一会话的购物车/未支付订单入口。
    /// </summary>
    public void Reset()
    {
        try
        {
            // 先清 SelectedShop 触发 OnSelectedShopChanged（CancelMenuLoad + 清 Products/Cart + 失效预览签名）
            SelectedShop = null;
            Shops.Clear();
            // SelectedShop 已是 null 时 OnSelectedShopChanged 不会再清 Shops（值未变不触发 setter），
            // 显式清掉防止残留；状态属性一并重置为初始引导文案。
            Token = "";
            SearchText = "";
            LocationText = "";
            ShopKeyword = "";
            StatusText = "请输入密钥后开始点餐";
        }
        catch
        {
            // best-effort：登出清理失败不影响登出流程
        }
    }

    /// <summary>中止进行中的菜单加载（切店 / 重新搜索门店）。代际计数 + 菜单专用 CTS，
    /// 仿 ChatDetailViewModel.CancelMessageLoad（ViewModels/ChatDetailViewModel.cs:490）。
    /// </summary>
    private void CancelMenuLoad()
    {
        _shopGeneration++;
        if (_menuCts is null) return;
        try { _menuCts.Cancel(); }
        catch { }
        _menuCts.Dispose();
        _menuCts = null;
    }

    private bool IsStaleMenuLoad(int generation)
        => generation != _shopGeneration;

    [RelayCommand]
    private async Task SaveTokenAsync()
    {
        try
        {
            await _mcp.SaveTokenAsync(Token);
            StatusText = "密钥已保存，可以查找门店";
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
    }

    [RelayCommand]
    private async Task SearchShopsAsync()
    {
        await RunAsync(async ct =>
        {
            var cityName = LocationText.Trim();
            var location = cityName.Length == 0
                ? await _weather.ResolveLocationFromIpAsync(ct)
                : (await _weather.SearchChinaCitiesAsync(cityName, ct)).FirstOrDefault();
            if (location is null)
            {
                Shops.Clear();
                SelectedShop = null;
                StatusText = $"没有找到城市“{cityName}”，请换用完整城市名";
                return;
            }

            var args = new Dictionary<string, object?>
            {
                ["longitude"] = location.Longitude,
                ["latitude"] = location.Latitude
            };
            var keyword = ShopKeyword.Trim();
            if (keyword.Length > 0) args["deptName"] = keyword;
            var response = await _mcp.CallAsync("queryShopList", args, ct);
            var shops = LuckinJson.FindEntities(response, "deptId", "deptID");

            Shops.Clear();
            foreach (var item in shops)
            {
                var id = LuckinJson.Int32(item, "deptId", "deptID", "id");
                if (id <= 0) continue;
                Shops.Add(new OrderShop
                {
                    DeptId = id,
                    Name = LuckinJson.String(item, "deptName", "name", "shopName"),
                    Address = LuckinJson.String(item, "address", "deptAddress", "shopAddress"),
                    Longitude = LuckinJson.Number(item, "longitude", "lng"),
                    Latitude = LuckinJson.Number(item, "latitude", "lat"),
                    WorkStatus = LuckinJson.String(item, "workStatus")
                });
            }

            SelectedShop = Shops.FirstOrDefault(x => x.IsOpen) ?? Shops.FirstOrDefault();
            // 点名省份（如“珠海市（广东）”），避免同名小地名再次造成静默错位。
            var resolvedName = string.IsNullOrWhiteSpace(location.Name)
                ? cityName
                : !string.IsNullOrWhiteSpace(location.Admin1) &&
                  !string.Equals(location.Admin1.TrimEnd('市'), location.Name.TrimEnd('市'), StringComparison.Ordinal)
                    ? $"{location.Name}（{location.Admin1}）"
                    : location.Name;
            var closedCount = Shops.Count(x => !x.IsOpen);
            StatusText = closedCount == 0
                ? $"{resolvedName}附近找到 {Shops.Count} 家门店，请确认自提门店"
                : $"{resolvedName}附近找到 {Shops.Count} 家门店（{closedCount} 家打烊中），请确认自提门店";
        });
    }

    [RelayCommand]
    private async Task SearchProductsAsync()
    {
        if (SelectedShop is not { } shop)
        {
            StatusText = "请先选择门店";
            return;
        }

        // 全量菜单扫描合法耗时可达数分钟（最多 300 次搜索 + N 次详情补价，4 并发），
        // 必须放宽应用层超时上限，否则正常扫描会被半路打断丢数据。
        await RunAsync(async ct =>
        {
            // 起菜单专用 CTS 并捕获代际计数：切店时 OnSelectedShopChanged 已 Cancel 旧 token。
            // 必须与 RunAsync 传入的 ct 链接，否则 RunAsync 的 120s 应用层超时对菜单加载完全失效
            // （服务端挂起时 IsBusy 永久锁死）。全量扫描合法耗时可能超 120s，调用方用更长超时。
            CancelMenuLoad();
            _menuCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var menuCt = _menuCts.Token;
            var generation = _shopGeneration;

            Products.Clear();
            var found = new Dictionary<string, OrderProduct>(StringComparer.OrdinalIgnoreCase);
            var search = SearchText.Trim();
            if (search.Length > 0)
            {
                var batch = await SearchProductBatchAsync(shop.DeptId, search, suppressErrors: false, menuCt);
                if (IsStaleMenuLoad(generation)) return;
                AppendProducts(batch.Products, found);
                StatusText = Products.Count == 0
                    ? $"没有找到“{search}”，请换个商品关键词"
                    : $"找到 {Products.Count} 款与“{search}”相关的商品，正在读取价格…";
                try { await FillPricesAsync(shop.DeptId, batch.Products, menuCt); }
                catch (OperationCanceledException) { return; }
                if (IsStaleMenuLoad(generation)) return;
                if (Products.Count > 0)
                {
                    StatusText = $"找到 {Products.Count} 款与“{search}”相关的商品";
                }
                return;
            }

            StatusText = "正在读取当前门店的咖啡菜单…";
            // searchProductForMcp 无分页、每次最多 3 条（tools/list 实测 additionalProperties:false），
            // 全量菜单靠“种子词 + 已发现商品名派生词根”多轮并集逼近，计划逻辑见 LuckinMenuSweep。
            var issued = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { LuckinMenuSweep.SeedQueries[0] };
            var primary = await SearchProductBatchAsync(shop.DeptId, LuckinMenuSweep.SeedQueries[0], suppressErrors: true, menuCt);
            if (IsStaleMenuLoad(generation)) return;
            AppendProducts(primary.Products, found);
            StatusText = $"已先显示 {Products.Count} 款，正在按分类并行补全咖啡菜单…";

            var failed = primary.Error is null ? 0 : 1;
            using var throttle = new SemaphoreSlim(4, 4);
            for (var round = 1; ; round++)
            {
                var wave = LuckinMenuSweep.BuildWave(round, found.Values.Select(x => x.Name), issued);
                if (wave.Count == 0) break;
                var pending = wave
                    .Select(query => SearchProductBatchThrottledAsync(shop.DeptId, query, throttle, menuCt))
                    .ToList();
                var completed = 0;
                var added = 0;
                Exception? cancelled = null;
                while (pending.Count > 0)
                {
                    var finished = await Task.WhenAny(pending);
                    pending.Remove(finished);
                    ProductSearchBatch batch;
                    try { batch = await finished; }
                    catch (OperationCanceledException ex) { cancelled = ex; break; }
                    if (IsStaleMenuLoad(generation)) break;
                    completed++;
                    if (batch.Error is not null) failed++;
                    var before = Products.Count;
                    AppendProducts(batch.Products, found);
                    added += Products.Count - before;
                    StatusText = $"正在补全咖啡菜单：第 {round} 轮 {completed}/{wave.Count}，累计 {issued.Count} 次查询，已找到 {Products.Count} 款";
                }

                if (IsStaleMenuLoad(generation))
                {
                    // 切店后旧菜单的残余请求已 Cancel；using throttle 需等 pending 全收尾才能 Dispose，
                    // 否则 finally 里的 Release 会抛 ObjectDisposedException（未观察异常）。
                    try { await Task.WhenAll(pending); } catch { }
                    return;
                }
                if (cancelled is not null)
                {
                    // 非切店的取消/超时（RunAsync 120s）同样先收尾再向上抛，由 RunAsync 统一提示可重试
                    try { await Task.WhenAll(pending); } catch { }
                    throw cancelled;
                }

                if (added == 0) break; // 本轮查询无任何新增：枚举收敛
            }

            StatusText = failed == 0
                ? $"已合并当前门店可搜索到的 {Products.Count} 款咖啡（{issued.Count} 次查询），正在读取价格…"
                : $"已显示 {Products.Count} 款咖啡；{failed} 个查询暂时加载失败，可重试补全；正在读取价格…";

            try { await FillPricesAsync(shop.DeptId, Products.ToArray(), menuCt); }
            catch (OperationCanceledException) { return; }
            if (IsStaleMenuLoad(generation)) return;

            StatusText = failed == 0
                ? $"已合并当前门店可搜索到的 {Products.Count} 款咖啡（{issued.Count} 次查询）"
                : $"已显示 {Products.Count} 款咖啡；{failed} 个查询暂时加载失败，可重试补全";
        }, TimeSpan.FromSeconds(240));
    }

    [RelayCommand]
    private void AddToCart(OrderProduct? product)
    {
        if (product is null) return;
        var existing = Cart.FirstOrDefault(x =>
            (!string.IsNullOrWhiteSpace(product.SkuCode) && string.Equals(x.SkuCode, product.SkuCode, StringComparison.OrdinalIgnoreCase)) ||
            (string.IsNullOrWhiteSpace(product.SkuCode) && x.ProductId == product.ProductId));
        if (existing is null)
        {
            Cart.Add(new OrderProduct
            {
                ProductId = product.ProductId,
                SkuCode = product.SkuCode,
                Name = product.Name,
                Price = product.Price,
                PictureUrl = product.PictureUrl
            });
        }
        else
        {
            existing.Amount++;
        }

        OnCartChanged();
    }

    [RelayCommand]
    private void IncreaseCartItem(OrderProduct? item)
    {
        if (item is null || !Cart.Contains(item)) return;
        item.Amount++;
        OnCartChanged();
    }

    [RelayCommand]
    private void DecreaseCartItem(OrderProduct? item)
    {
        if (item is null || !Cart.Contains(item)) return;
        if (item.Amount > 1)
        {
            item.Amount--;
        }
        else
        {
            Cart.Remove(item);
        }
        OnCartChanged();
    }

    [RelayCommand]
    private void RemoveFromCart(OrderProduct? item)
    {
        if (item is null || !Cart.Remove(item)) return;
        OnCartChanged();
    }

    private void OnCartChanged()
    {
        InvalidatePreviewAndPayment();
        OnPropertyChanged(nameof(CartTotal));
        OnPropertyChanged(nameof(CartTotalText));
    }

    [RelayCommand]
    private async Task PreviewAsync()
    {
        if (SelectedShop is not { } shop || Cart.Count == 0)
        {
            StatusText = "请选择门店并加入商品";
            return;
        }

        await RunAsync(async ct =>
        {
            InvalidatePreviewAndPayment();
            var response = await _mcp.CallAsync("previewOrder", new Dictionary<string, object?>
            {
                ["deptId"] = shop.DeptId,
                ["productList"] = BuildProductList()
            }, ct);

            if (!LuckinJson.TryNumber(response, out _previewDiscountPrice, "discountPrice"))
            {
                throw new InvalidOperationException("订单预览未返回明确的应付金额，已停止创建订单");
            }

            _previewInitialPrice = LuckinJson.TryNumber(response, out var initial, "totalInitialPrice") ? initial : CartTotal;
            _previewPrivilegeMoney = LuckinJson.TryNumber(response, out var privilege, "privilegeMoney") ? privilege : Math.Max(0, _previewInitialPrice - _previewDiscountPrice);
            _previewAboutTime = LuckinJson.TryNumber(response, out var aboutTime, "aboutTime") ? (long)aboutTime : 0;
            _previewCouponCodeList = LuckinJson.TryFindProperty(response, out var coupons, "couponCodeList") && coupons.ValueKind == JsonValueKind.Array
                ? coupons.Clone()
                : null;
            _previewSignature = BuildOrderSignature();
            PaymentSummary = BuildPaymentSummary();
            StatusText = $"订单预览完成，应付 ¥{_previewDiscountPrice:0.00}";
            OnPropertyChanged(nameof(HasValidPreview));
            OnPropertyChanged(nameof(ConfirmationText));
        });
    }

    [RelayCommand]
    private async Task CreateOrderAsync()
    {
        if (SelectedShop is not { } shop || Cart.Count == 0)
        {
            StatusText = "请选择门店并加入商品";
            return;
        }
        if (!HasValidPreview)
        {
            StatusText = "订单内容已变化，请重新预览后再创建";
            return;
        }

        await RunAsync(async ct =>
        {
            var args = new Dictionary<string, object?>
            {
                ["deptId"] = shop.DeptId,
                ["productList"] = BuildProductList(),
                ["longitude"] = shop.Longitude,
                ["latitude"] = shop.Latitude
            };
            if (_previewCouponCodeList is { ValueKind: JsonValueKind.Array } coupons && coupons.GetArrayLength() > 0)
            {
                args["couponCodeList"] = coupons;
            }

            var response = await _mcp.CallAsync("createOrder", args, ct);
            // createOrder 成功响应格式此前未实测过（2026-09-08 店铺打烊），落盘留现场便于排查
            LogOrderResponse(response);

            // 先存订单号：二维码异常时订单已在瑞幸后台创建，必须保住查询/取消入口
            OrderId = LuckinJson.String(response, "orderIdStr", "orderId");
            if (LuckinJson.TryNumber(response, out var createdDiscountPrice, "discountPrice"))
            {
                _previewDiscountPrice = createdDiscountPrice;
            }
            PaymentSummary = BuildPaymentSummary();

            var paymentPayload = LuckinJson.String(response, PaymentPayloadFields);
            var paymentShown = await TryShowPaymentAsync(paymentPayload, ct);
            StatusText = (paymentShown, OrderId.Length > 0) switch
            {
                (true, true) => $"真实订单 {OrderId} 已创建，请使用下方二维码完成支付",
                (true, false) => "真实订单已创建，请使用下方二维码完成支付",
                (false, true) => $"真实订单 {OrderId} 已创建，但瑞幸未返回支付二维码；点「查询支付状态」重试，或到瑞幸 App 完成支付",
                _ => "订单已创建但未返回订单号和支付二维码；请到瑞幸 App 查看待支付订单，请勿重复下单"
            };

            // 下单成功后清空购物车并使预览签名失效，避免重复点击创建多笔订单
            Cart.Clear();
            _previewSignature = null;
            _previewCouponCodeList = null;
            OnPropertyChanged(nameof(CartTotal));
            OnPropertyChanged(nameof(CartTotalText));
            OnPropertyChanged(nameof(HasValidPreview));
            OnPropertyChanged(nameof(ConfirmationText));
        });
    }

    private async Task<ProductSearchBatch> SearchProductBatchThrottledAsync(int deptId, string query, SemaphoreSlim throttle, CancellationToken cancellationToken)
    {
        await throttle.WaitAsync(cancellationToken);
        try
        {
            return await SearchProductBatchAsync(deptId, query, suppressErrors: true, cancellationToken);
        }
        finally
        {
            throttle.Release();
        }
    }

    private async Task<ProductSearchBatch> SearchProductBatchAsync(int deptId, string query, bool suppressErrors, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _mcp.CallAsync("searchProductForMcp", new Dictionary<string, object?>
            {
                ["deptId"] = deptId,
                ["query"] = query
            }, cancellationToken);
            var products = LuckinJson.FindEntities(response, "productId", "skuCode")
                .Select(ToProduct)
                .Where(product => product is not null)
                .Cast<OrderProduct>()
                .ToArray();
            return new ProductSearchBatch(products, null);
        }
        catch (Exception ex) when (suppressErrors && ex is not OperationCanceledException)
        {
            // 单个分类查询失败不阻断菜单补全，但取消必须向上传播（让 RunAsync 区分用户取消与失败）
            return new ProductSearchBatch([], ex.Message);
        }
    }

    private void AppendProducts(IEnumerable<OrderProduct> products, IDictionary<string, OrderProduct> found)
    {
        foreach (var product in products)
        {
            // 菜单展示按 productId 去重：不同查询可能对同一商品带回不同推荐 sku，商品卡片只应出现一次
            var key = product.ProductId > 0 ? $"id:{product.ProductId}" : $"sku:{product.SkuCode}";
            if (found.TryAdd(key, product)) Products.Add(product);
        }
    }

    /// <summary>searchProductForMcp 不返回价格，用 queryProductDetailInfo 节流补全，并同步购物车中同 sku 项。
/// 详情响应本身（含 productAttrs/skuCode）一并解析为 LuckinProductSpec 写入 _specCache，供 LoadCustomizeAsync 复用。</summary>
    private async Task FillPricesAsync(int deptId, IReadOnlyCollection<OrderProduct> targets, CancellationToken cancellationToken)
    {
        // 先按会话缓存命中取价：缓存 key 含 deptId，OnSelectedShopChanged 已 Clear 缓存
        var pending = new List<OrderProduct>();
        foreach (var product in targets)
        {
            if (product.Price > 0 || product.ProductId <= 0) continue;
            var cachedTask = TryGetCachedSpec(deptId, product.ProductId);
            if (cachedTask is { } && cachedTask.IsCompletedSuccessfully && cachedTask.Result is { } cached)
            {
                ApplyPrice(product, ResolvePrice(cached.EstimatePrice, cached.InitialPrice));
                SyncCartPrice(product);
            }
            else
            {
                pending.Add(product);
            }
        }

        if (pending.Count == 0)
        {
            OnPropertyChanged(nameof(CartTotal));
            OnPropertyChanged(nameof(CartTotalText));
            return;
        }

        using var throttle = new SemaphoreSlim(4, 4);
        var tasks = pending.Select(async product =>
        {
            await throttle.WaitAsync(cancellationToken);
            try
            {
                var spec = await GetOrFetchSpecAsync(deptId, product.ProductId, cancellationToken);
                if (spec is null) return;
                var price = ResolvePrice(spec.EstimatePrice, spec.InitialPrice);
                if (price <= 0) return;

                product.Price = price;
                SyncCartPrice(product);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // 单个商品价格读取失败不阻断菜单，价格保留“以预览为准”
            }
            finally
            {
                throttle.Release();
            }
        }).ToArray();

        await Task.WhenAll(tasks);
        OnPropertyChanged(nameof(CartTotal));
        OnPropertyChanged(nameof(CartTotalText));
    }

    /// <summary>同步购物车中同 sku / 同 productId 商品的价格。</summary>
    private void SyncCartPrice(OrderProduct product)
    {
        foreach (var cartItem in Cart.Where(x =>
            (!string.IsNullOrWhiteSpace(product.SkuCode) && string.Equals(x.SkuCode, product.SkuCode, StringComparison.OrdinalIgnoreCase)) ||
            (string.IsNullOrWhiteSpace(product.SkuCode) && x.ProductId == product.ProductId)))
        {
            cartItem.Price = product.Price;
        }
    }

    /// <summary>从会话缓存取同参 (deptId, productId) 的已解析规格（仍处于 in-flight 的返回其 Task）。
    /// 返回 null 表示未缓存或缓存项已失败。</summary>
    private Task<LuckinProductSpec?>? TryGetCachedSpec(int deptId, int productId)
        => _specCache.TryGetValue((deptId, productId), out var task) ? task : null;

    /// <summary>合并同参并发请求为一个网络往返：调用方 await 返回的 Task，
    /// 内部走 LuckinProductAttrs.Parse 同时给规格对话框与价格补全消费。</summary>
    private Task<LuckinProductSpec?> GetOrFetchSpecAsync(int deptId, int productId, CancellationToken cancellationToken)
    {
        var key = (deptId, productId);
        return _specCache.GetOrAdd(key, _ => FetchAndCacheSpecAsync(deptId, productId, cancellationToken));
    }

    private async Task<LuckinProductSpec?> FetchAndCacheSpecAsync(int deptId, int productId, CancellationToken cancellationToken)
    {
        try
        {
            var detail = await _mcp.CallAsync("queryProductDetailInfo", new Dictionary<string, object?>
            {
                ["deptId"] = deptId,
                ["productId"] = productId
            }, cancellationToken);
            var spec = LuckinProductAttrs.Parse(detail);
            // 失败/空响应不缓存：避免"价格以预览为准"被永久钉死
            return spec.EstimatePrice > 0 || spec.InitialPrice > 0 || spec.SkuCode.Length > 0 ? spec : null;
        }
        catch
        {
            _specCache.TryRemove((deptId, productId), out _);
            return null;
        }
    }

    private static double ResolvePrice(double estimate, double initial)
        => estimate > 0 ? estimate : initial > 0 ? initial : 0;

    private static void ApplyPrice(OrderProduct product, double price)
    {
        if (price > 0) product.Price = price;
    }

    private static OrderProduct? ToProduct(JsonElement item)
    {
        var productId = LuckinJson.Int32(item, "productId", "id");
        var skuCode = LuckinJson.String(item, "skuCode", "sku");
        if (productId <= 0 && skuCode.Length == 0) return null;
        var name = LuckinJson.String(item, "productName", "name");
        var picture = LuckinJson.String(item, "pictureUrl", "bigPicUrl", "breviaryPicUrl");
        return new OrderProduct
        {
            ProductId = productId,
            SkuCode = skuCode,
            Name = name.Length > 0 ? name : "咖啡",
            PictureUrl = picture.Length > 0 ? picture : null,
            Price = LuckinJson.Number(item, "estimatePrice", "price")
        };
    }

    /// <summary>createOrder/queryOrderDetailInfo 里可能出现的支付载体字段（候选名，2026-09-10 前未实测，按宽收）。</summary>
    private static readonly string[] PaymentPayloadFields =
        ["payOrderQrCodeUrl", "payQrCodeUrl", "payQrcodeUrl", "payUrl", "qrCodeUrl", "paymentUrl", "cashierUrl", "mwebUrl"];

    /// <summary>把支付载体显示出来：http(s) 直接按图片展示（浏览器打开兜底），其它字符串（如 weixin:// 支付串）本地生成二维码。返回是否成功显示。</summary>
    private async Task<bool> TryShowPaymentAsync(string paymentPayload, CancellationToken cancellationToken)
    {
        if (Uri.TryCreate(paymentPayload, UriKind.Absolute, out var paymentUri) && paymentUri.Scheme is ("https" or "http"))
        {
            PayQrImage = null;
            PayUrl = paymentUri.AbsoluteUri;
            return true;
        }
        if (string.IsNullOrWhiteSpace(paymentPayload)) return false;

        try
        {
            PayUrl = null;
            PayQrImage = await QrCodeImageHelper.CreateBitmapAsync(paymentPayload, cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 支付串生成二维码失败不丢订单：保留 OrderId，用户可查询/取消
            return false;
        }
    }

    private static void LogOrderResponse(JsonElement response)
    {
        try
        {
            var dir = Path.Combine(AppPaths.RootDirectory, "logs");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "luckin-orders.log"),
                $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] createOrder => {response.GetRawText()}{Environment.NewLine}");
        }
        catch
        {
            // 日志失败不影响下单
        }
    }

    private object[] BuildProductList()
        => Cart.Select(x => (object)new { amount = x.Amount, productId = x.ProductId, skuCode = x.SkuCode }).ToArray();

    private string? BuildOrderSignature()
    {
        if (SelectedShop is not { } shop || Cart.Count == 0) return null;
        return $"{shop.DeptId}|{string.Join(';', Cart.OrderBy(x => x.ProductId).ThenBy(x => x.SkuCode).Select(x => $"{x.ProductId}:{x.SkuCode}:{x.Amount}"))}";
    }

    [RelayCommand]
    private async Task QueryOrderStatusAsync()
    {
        if (string.IsNullOrWhiteSpace(OrderId))
        {
            StatusText = "请先创建订单";
            return;
        }

        await RunAsync(async ct =>
        {
            var response = await _mcp.CallAsync("queryOrderDetailInfo", new Dictionary<string, object?>
            {
                ["orderId"] = OrderId
            }, ct);

            var payStatus = LuckinJson.String(response, "payStatusDesc", "payStatusText", "payStatus");
            var orderStatus = LuckinJson.String(response, "orderStatusDesc", "statusDesc", "orderStatus", "status");
            var pickCode = LuckinJson.String(response, "pickUpCode", "pickupCode", "takeCode", "code");
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(payStatus)) parts.Add($"支付状态：{payStatus}");
            if (!string.IsNullOrWhiteSpace(orderStatus)) parts.Add($"订单状态：{orderStatus}");
            if (!string.IsNullOrWhiteSpace(pickCode)) parts.Add($"取餐码：{pickCode}");
            // 找回路径：创建时没显示出二维码的订单，详情里若带回支付载体则补显示
            if (PayImageSource is null
                && await TryShowPaymentAsync(LuckinJson.String(response, PaymentPayloadFields), ct))
            {
                parts.Add("已找回支付二维码");
            }
            StatusText = parts.Count > 0
                ? string.Join("；", parts)
                : $"订单 {OrderId} 详情：{response.GetRawText()[..Math.Min(300, response.GetRawText().Length)]}";
        });
    }

    [RelayCommand]
    private async Task CancelOrderAsync()
    {
        if (string.IsNullOrWhiteSpace(OrderId))
        {
            StatusText = "没有可取消的订单";
            return;
        }

        await RunAsync(async ct =>
        {
            await _mcp.CallAsync("cancelOrder", new Dictionary<string, object?>
            {
                ["orderId"] = OrderId
            }, ct);
            StatusText = $"订单 {OrderId} 已取消";
            PayUrl = null;
            PayQrImage = null;
            OrderId = "";
            PaymentSummary = "";
        });
    }

    private string BuildPaymentSummary()
    {
        var summary = $"原价 ¥{_previewInitialPrice:0.00} · 优惠 ¥{_previewPrivilegeMoney:0.00} · 应付 ¥{_previewDiscountPrice:0.00}";
        if (_previewAboutTime > 0)
        {
            var pickup = DateTimeOffset.FromUnixTimeMilliseconds(_previewAboutTime).ToLocalTime();
            summary += $" · 预计 {pickup:HH:mm} 可取";
        }
        // MCP 只支持到店自提，摘要里始终点明订单下到哪家门店、去哪取
        if (SelectedShop is { } shop)
        {
            summary += $"\n到店自取：{shop.NameWithAddress}";
        }
        return summary;
    }

    private void InvalidatePreviewAndPayment()
    {
        _previewSignature = null;
        _previewCouponCodeList = null;
        _previewAboutTime = 0;
        PayUrl = null;
        PayQrImage = null;
        OrderId = "";
        PaymentSummary = "";
        OnPropertyChanged(nameof(HasValidPreview));
        OnPropertyChanged(nameof(ConfirmationText));
    }

    private async Task RunAsync(Func<CancellationToken, Task> action)
        => await RunAsync(action, TimeSpan.FromSeconds(120));

    /// <summary>
    /// 每个 RunAsync 命令自带应用层超时，防止服务端挂起时用户被锁死。默认 120s；少数长操作（如全量菜单扫描）
    /// 可显式传入更长预算，但需要其内部的子 CTS 主动通过 CreateLinkedTokenSource 链接进来才能生效——否则就是空跑。
    /// </summary>
    private async Task RunAsync(Func<CancellationToken, Task> action, TimeSpan timeout)
    {
        if (IsBusy)
        {
            StatusText = "上一个操作仍在进行，请稍候重试";
            return;
        }

        _opCts?.Cancel();
        _opCts?.Dispose();
        var cts = new CancellationTokenSource(timeout);
        _opCts = cts;
        try
        {
            IsBusy = true;
            await action(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // 统一视为“操作被打断”：无论是本 CTS 超时、还是菜单 CTS（与本 CTS 通过 CreateLinkedTokenSource
            // 或裸关联取消）触发的下游 OCE，都向用户提示可重试，避免 TaskCanceledException 原文落进 StatusText。
            StatusText = "操作已取消或超时，可重试";
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
        finally
        {
            IsBusy = false;
            if (ReferenceEquals(_opCts, cts))
            {
                _opCts = null;
            }
            cts.Dispose();
        }
    }

    private sealed record ProductSearchBatch(IReadOnlyList<OrderProduct> Products, string? Error);
}

