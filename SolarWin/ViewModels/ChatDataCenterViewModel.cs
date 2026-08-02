using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.UI.Dispatching;
using SkiaSharp;
using SolarWin.Collections;
using SolarWin.Data;
using SolarWin.Models.Analytics;
using SolarWin.Services;

namespace SolarWin.ViewModels;

public partial class ChatDataCenterViewModel : ObservableObject
{
    private static readonly SKColor[] ChartPalette =
    [
        new(0x36, 0x7B, 0xF5), new(0x16, 0xA3, 0x8A), new(0xF5, 0x9E, 0x0B),
        new(0xEF, 0x44, 0x44), new(0x8B, 0x5C, 0xF6), new(0xEC, 0x48, 0x99),
        new(0x06, 0xB6, 0xD4), new(0x84, 0xCC, 0x16), new(0xF9, 0x73, 0x16),
        new(0x63, 0x66, 0xF1), new(0x14, 0xB8, 0xA6),
    ];

    private readonly IChatAnalyticsService _analytics;
    private readonly IAuthService _auth;
    private readonly IAccountSessionService _sessions;
    private readonly IAccountDbContextFactory _accountDb;
    private readonly DispatcherQueue? _dispatcher;
    private CancellationTokenSource? _refreshCts;
    private CancellationTokenSource? _wordLayoutCts;
    private readonly CancellationTokenSource _lifetimeCts = new();
    private IReadOnlyList<WordFrequencyItem> _wordFrequencies = [];
    private int _refreshVersion;
    private int _initialized;
    private bool _disposed;

    public ChatDataCenterViewModel(
        IChatAnalyticsService analytics,
        IAuthService auth,
        IAccountSessionService sessions,
        IAccountDbContextFactory accountDb)
    {
        _analytics = analytics;
        _auth = auth;
        _sessions = sessions;
        _accountDb = accountDb;
        _dispatcher = DispatcherQueue.GetForCurrentThread() ?? App.DispatcherQueue;

        var today = DateTimeOffset.Now.Date;
        StartDate = new DateTimeOffset(today.AddDays(-29), TimeZoneInfo.Local.GetUtcOffset(today));
        EndDate = new DateTimeOffset(today, TimeZoneInfo.Local.GetUtcOffset(today));
        CurrentUserId = ResolveCurrentUserId();
        SelectedGranularity = TrendGranularity.Day;
    }

    public ObservableRangeCollection<ActivityHeatmapCell> HeatmapCells { get; } = [];
    public ObservableRangeCollection<WordCloudItem> WordCloudItems { get; } = [];
    public ObservableRangeCollection<ConversationOption> Conversations { get; } = [];

    public IReadOnlyList<TrendGranularity> Granularities { get; } =
        [TrendGranularity.Day, TrendGranularity.Week, TrendGranularity.Month];

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool IsWordCloudLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    [ObservableProperty]
    public partial bool HasData { get; set; }

    [ObservableProperty]
    public partial DateTimeOffset StartDate { get; set; }

    [ObservableProperty]
    public partial DateTimeOffset EndDate { get; set; }

    [ObservableProperty]
    public partial ConversationOption? SelectedConversation { get; set; }

    [ObservableProperty]
    public partial TrendGranularity SelectedGranularity { get; set; }

    [ObservableProperty]
    public partial bool OnlyCurrentUser { get; set; }

    [ObservableProperty]
    public partial string CurrentUserId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SummaryText { get; set; } = "等待加载本地统计";

    [ObservableProperty]
    public partial string PeakHourText { get; set; } = "—";

    [ObservableProperty]
    public partial string PeakPeriodText { get; set; } = "—";

    [ObservableProperty]
    public partial string HourlyAverageText { get; set; } = "0";

    [ObservableProperty]
    public partial string TotalMessagesText { get; set; } = "0";

    [ObservableProperty]
    public partial string DailyAverageText { get; set; } = "0";

    [ObservableProperty]
    public partial string MostActiveDayText { get; set; } = "—";

    [ObservableProperty]
    public partial string SelectedInsightText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ISeries[] MessageTrendSeries { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MessageTrendXAxesSource))]
    public partial Axis[] MessageTrendXAxes { get; set; } = [];

    public IEnumerable<ICartesianAxis> MessageTrendXAxesSource => MessageTrendXAxes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MessageTrendYAxesSource))]
    public partial Axis[] MessageTrendYAxes { get; set; } = [];

    public IEnumerable<ICartesianAxis> MessageTrendYAxesSource => MessageTrendYAxes;

    [ObservableProperty]
    public partial ISeries[] HourlyActivitySeries { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HourlyAngleAxesSource))]
    public partial PolarAxis[] HourlyAngleAxes { get; set; } = [];

    public IEnumerable<IPolarAxis> HourlyAngleAxesSource => HourlyAngleAxes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HourlyRadiusAxesSource))]
    public partial PolarAxis[] HourlyRadiusAxes { get; set; } = [];

    public IEnumerable<IPolarAxis> HourlyRadiusAxesSource => HourlyRadiusAxes;

    [ObservableProperty]
    public partial bool HasHeatmapData { get; set; }

    [ObservableProperty]
    public partial bool HasTrendData { get; set; }

    [ObservableProperty]
    public partial bool HasHourlyData { get; set; }

    [ObservableProperty]
    public partial bool HasWordCloudData { get; set; }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (Interlocked.Exchange(ref _initialized, 1) != 0) return;
        var token = _lifetimeCts.Token;
        CurrentUserId = ResolveCurrentUserId();

        try
        {
            var conversations = await Task.Run(
                () => _analytics.GetConversationsAsync(token), token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            await RunOnUiAsync(() =>
            {
                Conversations.ReplaceWith(
                    new[] { new ConversationOption(null, "全部会话", -1, IsAll: true) }.Concat(conversations));
                SelectedConversation = Conversations[0];
            }, token).ConfigureAwait(false);
            await RefreshCoreAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Navigation disposed this page while initialization was still running.
        }
        catch (Exception ex)
        {
            if (!_disposed)
            {
                await RunOnUiAsync(() => ErrorMessage = FriendlyError(ex), token).ConfigureAwait(false);
            }
        }
    }

    [RelayCommand]
    private Task RefreshAsync() => RefreshCoreAsync();

    [RelayCommand]
    private void HeatmapCellClick(ActivityHeatmapCell? cell)
    {
        if (cell is null) return;
        SelectedInsightText = cell.TooltipText;
    }

    [RelayCommand]
    private void WordClick(WordCloudItem? item)
    {
        if (item is null) return;
        SelectedInsightText = $"关键词“{item.Word}”出现 {item.Count} 次，可用于后续聊天搜索。";
    }

    [RelayCommand]
    private void ResetFilter()
    {
        var today = DateTimeOffset.Now.Date;
        StartDate = new DateTimeOffset(today.AddDays(-29), TimeZoneInfo.Local.GetUtcOffset(today));
        EndDate = new DateTimeOffset(today, TimeZoneInfo.Local.GetUtcOffset(today));
        SelectedConversation = Conversations.FirstOrDefault();
        SelectedGranularity = TrendGranularity.Day;
        OnlyCurrentUser = false;
        SelectedInsightText = string.Empty;
    }

    public async Task RelayoutWordCloudAsync(double width, double height)
    {
        if (_wordFrequencies.Count == 0 || _disposed) return;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var next = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token, timeout.Token);
        var previous = Interlocked.Exchange(ref _wordLayoutCts, next);
        previous?.Cancel();
        try
        {
            var items = await _analytics.LayoutWordCloudAsync(_wordFrequencies, width, height, next.Token).ConfigureAwait(false);
            next.Token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(Volatile.Read(ref _wordLayoutCts), next) || _disposed) return;
            await RunOnUiAsync(() => WordCloudItems.ReplaceWith(items), next.Token).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.CompareExchange(ref _wordLayoutCts, null, next);
            next.Dispose();
        }
    }

    public void Close()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetimeCts.Cancel();
        _refreshCts?.Cancel();
        _wordLayoutCts?.Cancel();
    }

    private async Task RefreshCoreAsync()
    {
        if (_disposed) return;
        var next = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
        var previous = Interlocked.Exchange(ref _refreshCts, next);
        previous?.Cancel();
        var token = next.Token;
        var version = Interlocked.Increment(ref _refreshVersion);
        AnalyticsFilter? filter = null;

        try
        {
            await RunOnUiAsync(() =>
            {
                IsLoading = true;
                IsWordCloudLoading = true;
                ErrorMessage = null;
                CurrentUserId = ResolveCurrentUserId();
                filter = BuildFilter();
            }, token).ConfigureAwait(false);

            // Microsoft.Data.Sqlite executes its async APIs synchronously. Keep the whole
            // query/tokenization batch off the UI thread, and keep only one ownership task.
            var result = await Task.Run(async () =>
            {
                var activeFilter = filter!;
                var heatTask = CaptureAsync(() => _analytics.GetActivityHeatmapAsync(activeFilter, token));
                var trendTask = CaptureAsync(() => _analytics.GetMessageTrendAsync(activeFilter, token));
                var hourlyTask = CaptureAsync(() => _analytics.GetHourlyActivityAsync(activeFilter, token));
                var wordsTask = CaptureAsync(() => _analytics.GetWordFrequenciesAsync(activeFilter with { TopN = 50 }, token));

                await Task.WhenAll(heatTask, trendTask, hourlyTask, wordsTask).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                var heat = await heatTask.ConfigureAwait(false);
                var trend = await trendTask.ConfigureAwait(false);
                var hourly = await hourlyTask.ConfigureAwait(false);
                var words = await wordsTask.ConfigureAwait(false);
                var summary = _analytics.CalculateSummary(activeFilter, heat.Value ?? [], hourly.Value ?? []);
                var cloud = await _analytics.LayoutWordCloudAsync(words.Value ?? [], 720, 330, token).ConfigureAwait(false);
                return new RefreshResult(activeFilter, heat, trend, hourly, words, summary, cloud);
            }, token).ConfigureAwait(false);

            token.ThrowIfCancellationRequested();
            if (version != Volatile.Read(ref _refreshVersion) || _disposed) return;
            await RunOnUiAsync(() =>
            {
                ApplyHeatmap(result.Heat.Value ?? []);
                ApplyTrend(result.Trend.Value ?? []);
                ApplyHourly(result.Hourly.Value ?? []);
                _wordFrequencies = result.Words.Value ?? [];
                WordCloudItems.ReplaceWith(result.Cloud);
                HasWordCloudData = WordCloudItems.Count > 0;
                ApplySummary(result.Summary, result.Filter);
                var moduleErrors = new[] { result.Heat.Error, result.Trend.Error, result.Hourly.Error, result.Words.Error }
                    .Where(static x => !string.IsNullOrWhiteSpace(x)).ToArray();
                ErrorMessage = moduleErrors.Length == 0 ? null : string.Join("；", moduleErrors.Distinct());
                HasData = HasHeatmapData || HasTrendData || HasHourlyData || HasWordCloudData;
            }, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // A newer refresh or navigation cleanup superseded this request.
        }
        catch (Exception ex)
        {
            if (version == Volatile.Read(ref _refreshVersion) && !_disposed)
            {
                await RunOnUiAsync(() => ErrorMessage = FriendlyError(ex), _lifetimeCts.Token).ConfigureAwait(false);
            }
        }
        finally
        {
            var isCurrent = ReferenceEquals(Interlocked.CompareExchange(ref _refreshCts, null, next), next);
            next.Dispose();
            if (isCurrent && !_disposed)
            {
                await RunOnUiAsync(() =>
                {
                    IsLoading = false;
                    IsWordCloudLoading = false;
                }, _lifetimeCts.Token).ConfigureAwait(false);
            }
        }
    }

    private AnalyticsFilter BuildFilter()
    {
        var startLocal = DateTime.SpecifyKind(StartDate.Date, DateTimeKind.Local);
        var endLocalExclusive = DateTime.SpecifyKind(EndDate.Date.AddDays(1), DateTimeKind.Local);
        if (endLocalExclusive <= startLocal) throw new InvalidOperationException("结束日期不能早于起始日期。");
        return new AnalyticsFilter
        {
            StartTime = new DateTimeOffset(startLocal).ToUniversalTime(),
            EndTime = new DateTimeOffset(endLocalExclusive).ToUniversalTime(),
            ConversationId = SelectedConversation is { IsAll: false } ? SelectedConversation.Id : null,
            CurrentUserId = CurrentUserId,
            OnlyCurrentUser = OnlyCurrentUser,
            TopN = 50,
            TrendGranularity = SelectedGranularity,
        };
    }

    private void ApplyHeatmap(IReadOnlyList<ActivityHeatmapCell> cells)
    {
        HeatmapCells.ReplaceWith(cells);
        HasHeatmapData = cells.Any(static x => x.MessageCount > 0);
    }

    private void ApplyTrend(IReadOnlyList<MessageTrendPoint> points)
    {
        var values = points.Select(static x => (double)x.MessageCount).ToArray();
        var color = ChartPalette[0];
        MessageTrendSeries =
        [
            new LineSeries<double>
            {
                Name = "消息数",
                Values = values,
                GeometrySize = points.Count > 90 ? 0 : 6,
                LineSmoothness = 0.35,
                Stroke = new SolidColorPaint(color, 2),
                Fill = new SolidColorPaint(new SKColor(color.Red, color.Green, color.Blue, 45)),
                XToolTipLabelFormatter = point => points[Math.Clamp(point.Index, 0, points.Count - 1)].Label,
                YToolTipLabelFormatter = point => $"{point.Model:N0} 条消息",
            },
        ];
        MessageTrendXAxes =
        [
            new Axis
            {
                Labels = points.Select(static x => x.Label).ToArray(),
                MinStep = 1,
                ForceStepToMin = true,
                LabelsRotation = points.Count > 60 ? 45 : points.Count > 30 ? 25 : 0,
            },
        ];
        MessageTrendYAxes =
        [
            new Axis { MinLimit = 0, MinStep = 1, Labeler = static value => Math.Max(0, value).ToString("0") },
        ];
        HasTrendData = points.Any(static x => x.MessageCount > 0);
    }

    private void ApplyHourly(IReadOnlyList<HourlyActivityPoint> points)
    {
        var color = ChartPalette[4];
        // LiveCharts2 2.0.5 has no RadarSeries. A closed PolarLineSeries is the supported approximation.
        HourlyActivitySeries =
        [
            new PolarLineSeries<double>
            {
                Name = "小时消息数",
                Values = points.Select(static x => (double)x.MessageCount).ToArray(),
                IsClosed = true,
                GeometrySize = 5,
                LineSmoothness = 0.15,
                Stroke = new SolidColorPaint(color, 2),
                Fill = new SolidColorPaint(new SKColor(color.Red, color.Green, color.Blue, 45)),
                RadiusToolTipLabelFormatter = point => $"{Math.Clamp(point.Index, 0, 23):00}:00–{Math.Clamp(point.Index, 0, 23):00}:59，共 {point.Model:N0} 条消息",
            },
        ];
        HourlyAngleAxes =
        [
            new PolarAxis
            {
                Labels = Enumerable.Range(0, 24).Select(static h => h % 3 == 0 ? $"{h:00}" : string.Empty).ToArray(),
                MinLimit = 0,
                MaxLimit = 24,
                MinStep = 1,
                ForceStepToMin = true,
            },
        ];
        HourlyRadiusAxes = [new PolarAxis { MinLimit = 0, MinStep = 1 }];
        HasHourlyData = points.Any(static x => x.MessageCount > 0);
    }

    private void ApplySummary(ChatAnalyticsSummary summary, AnalyticsFilter filter)
    {
        TotalMessagesText = summary.TotalMessages.ToString("N0");
        DailyAverageText = summary.AveragePerDay.ToString("N1");
        MostActiveDayText = summary.MostActiveDay;
        PeakHourText = summary.PeakHour;
        PeakPeriodText = summary.PeakPeriod;
        HourlyAverageText = summary.AveragePerHour.ToString("N1");
        SummaryText = $"{filter.StartTime.ToLocalTime():yyyy-MM-dd} 至 {filter.EndTime.ToLocalTime().AddTicks(-1):yyyy-MM-dd} · 所有统计均在本地完成";
    }

    private string ResolveCurrentUserId()
        => (_auth.CurrentAccount?.Id ?? _sessions.ActiveAccountId ?? _accountDb.BoundAccountId)?.ToString("D") ?? string.Empty;

    private static int StablePaletteIndex(string value)
    {
        uint hash = 2166136261;
        foreach (var c in value)
        {
            hash ^= c;
            hash *= 16777619;
        }

        return (int)(hash % (uint)ChartPalette.Length);
    }

    private static async Task<ModuleResult<T>> CaptureAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return ModuleResult<T>.Success(await action().ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ModuleResult<T>.Failure(FriendlyError(ex));
        }
    }

    private Task RunOnUiAsync(Action action, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_dispatcher is null || _dispatcher.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_dispatcher.TryEnqueue(() =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    action();
                    tcs.TrySetResult();
                }
                catch (OperationCanceledException) { tcs.TrySetCanceled(cancellationToken); }
                catch (Exception ex) { tcs.TrySetException(ex); }
            }))
        {
            tcs.TrySetCanceled();
        }

        return tcs.Task;
    }

    private static string FriendlyError(Exception ex)
        => ex switch
        {
            FileNotFoundException => ex.Message,
            InvalidOperationException => ex.Message,
            _ => "加载本地聊天统计时发生错误，请稍后重试。",
        };

    private sealed record ModuleResult<T>(T? Value, string? Error)
    {
        public static ModuleResult<T> Success(T value) => new(value, null);
        public static ModuleResult<T> Failure(string error) => new(default, error);
    }

    private sealed record RefreshResult(
        AnalyticsFilter Filter,
        ModuleResult<IReadOnlyList<ActivityHeatmapCell>> Heat,
        ModuleResult<IReadOnlyList<MessageTrendPoint>> Trend,
        ModuleResult<IReadOnlyList<HourlyActivityPoint>> Hourly,
        ModuleResult<IReadOnlyList<WordFrequencyItem>> Words,
        ChatAnalyticsSummary Summary,
        IReadOnlyList<WordCloudItem> Cloud);
}
