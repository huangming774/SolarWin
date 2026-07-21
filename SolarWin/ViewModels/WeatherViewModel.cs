using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using SolarWin.Models;
using SolarWin.Services;
using Windows.UI;

namespace SolarWin.ViewModels;

public partial class WeatherViewModel : ObservableObject
{
    private readonly IWeatherService _weather;

    private double _latitude = 31.2304;
    private double _longitude = 121.4737;
    private CancellationTokenSource? _suggestCts;

    public WeatherViewModel(IWeatherService weather)
    {
        _weather = weather;
    }

    public ObservableCollection<GeoResult> CitySuggestions { get; } = [];

    public ObservableCollection<HourlyForecastItem> HourlyForecast { get; } = [];

    public ObservableCollection<DailyForecastItem> DailyForecast { get; } = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LocationTitle { get; set; } = "定位中…";

    [ObservableProperty]
    public partial string LocationSubtitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TemperatureText { get; set; } = "--°";

    [ObservableProperty]
    public partial string WeatherEmoji { get; set; } = "🌡️";

    [ObservableProperty]
    public partial string WeatherDescription { get; set; } = "—";

    [ObservableProperty]
    public partial string FeelsLikeText { get; set; } = "体感 —";

    [ObservableProperty]
    public partial string HumidityText { get; set; } = "湿度 —";

    [ObservableProperty]
    public partial string WindText { get; set; } = "风速 —";

    [ObservableProperty]
    public partial string HighLowText { get; set; } = "最高 — · 最低 —";

    // —— Air quality ——
    [ObservableProperty]
    public partial string AirQualityValue { get; set; } = "—";

    [ObservableProperty]
    public partial string AirQualityLevel { get; set; } = "—";

    [ObservableProperty]
    public partial string AirQualityDetail { get; set; } = "暂无空气质量数据";

    // —— Precipitation ——
    [ObservableProperty]
    public partial string PrecipitationValue { get; set; } = "—";

    [ObservableProperty]
    public partial string PrecipitationLevel { get; set; } = "—";

    [ObservableProperty]
    public partial string PrecipitationDetail { get; set; } = "暂无降水数据";

    // —— UV ——
    [ObservableProperty]
    public partial string UvIndexValue { get; set; } = "—";

    [ObservableProperty]
    public partial string UvIndexLevel { get; set; } = "—";

    [ObservableProperty]
    public partial string UvIndexDetail { get; set; } = "暂无紫外线数据";

    // —— Sun ——
    [ObservableProperty]
    public partial string SunriseText { get; set; } = "—";

    [ObservableProperty]
    public partial string SunsetText { get; set; } = "—";

    [ObservableProperty]
    public partial string SunDetail { get; set; } = "暂无日出日落数据";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContentVisibility))]
    [NotifyPropertyChangedFor(nameof(EmptyVisibility))]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ErrorVisibility))]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContentVisibility))]
    [NotifyPropertyChangedFor(nameof(EmptyVisibility))]
    public partial bool HasData { get; set; }

    [ObservableProperty]
    public partial Color GradientTop { get; set; } = Color.FromArgb(255, 12, 24, 56);

    [ObservableProperty]
    public partial Color GradientBottom { get; set; } = Color.FromArgb(255, 30, 72, 140);

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public Visibility ErrorVisibility => HasError ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ContentVisibility => HasData && !IsLoading ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyVisibility => !HasData && !IsLoading && !HasError ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Load by public IP (fallback Shanghai) then forecast.</summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;
            NotifyVisibilities();

            var location = await _weather.ResolveLocationFromIpAsync().ConfigureAwait(true);
            ApplyLocation(location);
            await LoadForecastInternalAsync().ConfigureAwait(true);
        }
        catch (WeatherServiceException ex)
        {
            ErrorMessage = ex.Message;
            HasData = false;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载失败：{ex.Message}";
            HasData = false;
        }
        finally
        {
            IsLoading = false;
            NotifyVisibilities();
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;
            await LoadForecastInternalAsync().ConfigureAwait(true);
        }
        catch (WeatherServiceException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"刷新失败：{ex.Message}";
        }
        finally
        {
            IsLoading = false;
            NotifyVisibilities();
        }
    }

    [RelayCommand]
    private async Task SuggestCitiesAsync(string? query)
    {
        var q = (query ?? SearchText)?.Trim() ?? string.Empty;
        if (q.Length < 1)
        {
            CitySuggestions.Clear();
            return;
        }

        _suggestCts?.Cancel();
        _suggestCts = new CancellationTokenSource();
        var token = _suggestCts.Token;

        try
        {
            await Task.Delay(280, token).ConfigureAwait(true);
            var results = await _weather.SearchCitiesAsync(q, token).ConfigureAwait(true);
            if (token.IsCancellationRequested)
            {
                return;
            }

            CitySuggestions.Clear();
            foreach (var r in results)
            {
                CitySuggestions.Add(r);
            }
        }
        catch (OperationCanceledException)
        {
            // debounce / superseded
        }
        catch (WeatherServiceException)
        {
            CitySuggestions.Clear();
        }
    }

    [RelayCommand]
    private async Task SelectCityAsync(GeoResult? city)
    {
        if (city is null)
        {
            return;
        }

        try
        {
            IsLoading = true;
            ErrorMessage = null;
            ApplyLocation(city);
            SearchText = city.DisplayName;
            await LoadForecastInternalAsync().ConfigureAwait(true);
        }
        catch (WeatherServiceException ex)
        {
            ErrorMessage = ex.Message;
            HasData = false;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载失败：{ex.Message}";
            HasData = false;
        }
        finally
        {
            IsLoading = false;
            NotifyVisibilities();
        }
    }

    private void NotifyVisibilities()
    {
        OnPropertyChanged(nameof(ErrorVisibility));
        OnPropertyChanged(nameof(ContentVisibility));
        OnPropertyChanged(nameof(EmptyVisibility));
        OnPropertyChanged(nameof(HasError));
    }

    private void ApplyLocation(GeoResult location)
    {
        _latitude = location.Latitude;
        _longitude = location.Longitude;
        LocationTitle = location.Name ?? "未知";
        var sub = new List<string>();
        if (!string.IsNullOrWhiteSpace(location.Admin1))
        {
            sub.Add(location.Admin1!);
        }

        if (!string.IsNullOrWhiteSpace(location.Country))
        {
            sub.Add(location.Country!);
        }

        LocationSubtitle = sub.Count > 0 ? string.Join(" · ", sub) : $"{_latitude:F2}, {_longitude:F2}";
    }

    private async Task LoadForecastInternalAsync()
    {
        var forecast = await _weather.GetForecastAsync(_latitude, _longitude).ConfigureAwait(true);
        ApplyForecast(forecast);

        // Air quality is best-effort; never fail the whole dashboard.
        try
        {
            var aqi = await _weather.GetAirQualityAsync(_latitude, _longitude).ConfigureAwait(true);
            ApplyAirQuality(aqi);
        }
        catch
        {
            ResetAirQuality();
        }

        HasData = true;
    }

    private void ApplyForecast(ForecastResponse forecast)
    {
        var current = forecast.Current;
        if (current is not null)
        {
            var code = current.WeatherCode;
            TemperatureText = $"{current.Temperature2m:0.#}°";
            WeatherEmoji = WeatherCodeMapper.Emoji(code);
            WeatherDescription = WeatherCodeMapper.Describe(code);
            FeelsLikeText = $"体感 {current.ApparentTemperature:0.#}°";
            HumidityText = $"湿度 {current.RelativeHumidity2m}%";
            WindText = $"风速 {current.WindSpeed10m:0.#} km/h";
            ApplyGradient(WeatherCodeMapper.Mood(code));
        }

        // Daily first day → high/low, sun, precip, UV
        var daily = forecast.Daily;
        if (daily?.Time is { Count: > 0 })
        {
            if (daily.Temperature2mMax is { Count: > 0 } && daily.Temperature2mMin is { Count: > 0 })
            {
                HighLowText = $"最高 {daily.Temperature2mMax[0]:0}°  ·  最低 {daily.Temperature2mMin[0]:0}°";
            }

            ApplySun(daily);
            ApplyPrecipitation(forecast);
            ApplyUv(forecast);
            ApplyDailyList(daily);
        }
        else
        {
            HighLowText = "最高 — · 最低 —";
            SunriseText = "—";
            SunsetText = "—";
            SunDetail = "暂无日出日落数据";
            PrecipitationValue = "—";
            PrecipitationLevel = "—";
            PrecipitationDetail = "暂无降水数据";
            UvIndexValue = "—";
            UvIndexLevel = "—";
            UvIndexDetail = "暂无紫外线数据";
            DailyForecast.Clear();
        }

        ApplyHourly(forecast);
    }

    private void ApplyHourly(ForecastResponse forecast)
    {
        HourlyForecast.Clear();
        var hourly = forecast.Hourly;
        if (hourly?.Time is not { Count: > 0 } times ||
            hourly.Temperature2m is not { } temps ||
            hourly.WeatherCode is not { } codes)
        {
            return;
        }

        var now = DateTimeOffset.Now;
        var count = Math.Min(times.Count, Math.Min(temps.Count, codes.Count));
        var pops = hourly.PrecipitationProbability;
        var added = 0;
        for (var i = 0; i < count && added < 24; i++)
        {
            if (!DateTimeOffset.TryParse(times[i], out var t))
            {
                continue;
            }

            if (t < now.AddMinutes(-30))
            {
                continue;
            }

            var pop = pops is not null && i < pops.Count ? pops[i] : (int?)null;
            HourlyForecast.Add(new HourlyForecastItem
            {
                TimeLabel = added == 0 ? "现在" : t.ToLocalTime().ToString("HH"),
                Emoji = WeatherCodeMapper.Emoji(codes[i]),
                TemperatureText = $"{temps[i]:0}°",
                PrecipitationChanceText = pop is null ? string.Empty : (pop > 0 ? $"{pop}%" : string.Empty),
            });
            added++;
        }
    }

    private void ApplyDailyList(DailyWeatherBlock daily)
    {
        DailyForecast.Clear();
        if (daily.Time is not { Count: > 0 } days ||
            daily.WeatherCode is not { } dCodes ||
            daily.Temperature2mMax is not { } maxs ||
            daily.Temperature2mMin is not { } mins)
        {
            return;
        }

        var n = Math.Min(10, Math.Min(days.Count, Math.Min(dCodes.Count, Math.Min(maxs.Count, mins.Count))));
        var pops = daily.PrecipitationProbabilityMax;
        for (var i = 0; i < n; i++)
        {
            var dateLabel = days[i];
            if (DateTime.TryParse(days[i], out var d))
            {
                dateLabel = i switch
                {
                    0 => "今天",
                    1 => "明天",
                    _ => d.ToString("M月d日 ddd"),
                };
            }

            var popText = pops is not null && i < pops.Count && pops[i] > 0
                ? $"{pops[i]}%"
                : string.Empty;

            DailyForecast.Add(new DailyForecastItem
            {
                DateLabel = dateLabel,
                Emoji = WeatherCodeMapper.Emoji(dCodes[i]),
                Description = WeatherCodeMapper.Describe(dCodes[i]),
                HighText = $"{maxs[i]:0}°",
                LowText = $"{mins[i]:0}°",
                HighLowText = $"{maxs[i]:0}° / {mins[i]:0}°",
                PrecipitationChanceText = popText,
            });
        }
    }

    private void ApplySun(DailyWeatherBlock daily)
    {
        string? sunriseRaw = daily.Sunrise is { Count: > 0 } ? daily.Sunrise[0] : null;
        string? sunsetRaw = daily.Sunset is { Count: > 0 } ? daily.Sunset[0] : null;

        DateTimeOffset? rise = null;
        DateTimeOffset? set = null;
        if (DateTimeOffset.TryParse(sunriseRaw, out var r))
        {
            rise = r.ToLocalTime();
            SunriseText = rise.Value.ToString("HH:mm");
        }
        else
        {
            SunriseText = "—";
        }

        if (DateTimeOffset.TryParse(sunsetRaw, out var s))
        {
            set = s.ToLocalTime();
            SunsetText = set.Value.ToString("HH:mm");
        }
        else
        {
            SunsetText = "—";
        }

        if (rise is not null && set is not null)
        {
            var span = set.Value - rise.Value;
            if (span.TotalMinutes > 0)
            {
                SunDetail = $"日照约 {span.Hours} 小时 {span.Minutes} 分钟";
            }
            else
            {
                SunDetail = "今日日出日落";
            }
        }
        else
        {
            SunDetail = "暂无日出日落数据";
        }
    }

    private void ApplyPrecipitation(ForecastResponse forecast)
    {
        var daily = forecast.Daily;
        double sum = 0;
        int? popMax = null;
        if (daily?.PrecipitationSum is { Count: > 0 })
        {
            sum = daily.PrecipitationSum[0];
        }

        if (daily?.PrecipitationProbabilityMax is { Count: > 0 })
        {
            popMax = daily.PrecipitationProbabilityMax[0];
        }

        // Prefer next-hour pop if available for "chance"
        int? nextPop = null;
        var hourly = forecast.Hourly;
        if (hourly?.Time is { Count: > 0 } && hourly.PrecipitationProbability is { } pops)
        {
            var now = DateTimeOffset.Now;
            for (var i = 0; i < Math.Min(hourly.Time.Count, pops.Count); i++)
            {
                if (DateTimeOffset.TryParse(hourly.Time[i], out var t) && t >= now.AddMinutes(-30))
                {
                    nextPop = pops[i];
                    break;
                }
            }
        }

        var chance = nextPop ?? popMax;
        PrecipitationValue = chance is null ? $"{sum:0.#} mm" : $"{chance}%";
        PrecipitationLevel = chance switch
        {
            null when sum <= 0 => "干燥",
            null => "有降水",
            <= 20 => "可能性低",
            <= 50 => "可能有雨",
            <= 80 => "较可能",
            _ => "很大可能",
        };

        var parts = new List<string>();
        if (chance is not null)
        {
            parts.Add($"降水概率 {chance}%");
        }

        parts.Add($"今日累计 {sum:0.#} mm");
        if (forecast.Current is not null && forecast.Current.Precipitation > 0)
        {
            parts.Add($"当前 {forecast.Current.Precipitation:0.#} mm");
        }

        PrecipitationDetail = string.Join(" · ", parts);
    }

    private void ApplyUv(ForecastResponse forecast)
    {
        double? uv = null;
        var daily = forecast.Daily;
        if (daily?.UvIndexMax is { Count: > 0 })
        {
            uv = daily.UvIndexMax[0];
        }

        // Prefer current-hour UV when present
        var hourly = forecast.Hourly;
        if (hourly?.Time is { Count: > 0 } && hourly.UvIndex is { } uvs)
        {
            var now = DateTimeOffset.Now;
            for (var i = 0; i < Math.Min(hourly.Time.Count, uvs.Count); i++)
            {
                if (DateTimeOffset.TryParse(hourly.Time[i], out var t) && t >= now.AddMinutes(-30))
                {
                    uv = uvs[i];
                    break;
                }
            }
        }

        if (uv is null)
        {
            UvIndexValue = "—";
            UvIndexLevel = "—";
            UvIndexDetail = "暂无紫外线数据";
            return;
        }

        var v = uv.Value;
        UvIndexValue = v.ToString("0.#");
        UvIndexLevel = WeatherCodeMapper.UvLevel(v);
        UvIndexDetail = $"今日峰值约 {daily?.UvIndexMax?.FirstOrDefault():0.#} · {WeatherCodeMapper.UvAdvice(v)}";
    }

    private void ApplyAirQuality(AirQualityResponse? aqi)
    {
        var cur = aqi?.Current;
        if (cur is null)
        {
            ResetAirQuality();
            return;
        }

        var index = cur.UsAqi ?? cur.EuropeanAqi;
        if (index is null)
        {
            ResetAirQuality();
            return;
        }

        var value = index.Value;
        AirQualityValue = value.ToString();
        AirQualityLevel = WeatherCodeMapper.AqiLevel(value);
        var parts = new List<string> { WeatherCodeMapper.AqiAdvice(value) };
        if (cur.Pm25 is double pm25)
        {
            parts.Add($"PM2.5 {pm25:0.#}");
        }

        if (cur.Pm10 is double pm10)
        {
            parts.Add($"PM10 {pm10:0.#}");
        }

        AirQualityDetail = string.Join(" · ", parts);
    }

    private void ResetAirQuality()
    {
        AirQualityValue = "—";
        AirQualityLevel = "—";
        AirQualityDetail = "暂无空气质量数据";
    }

    private void ApplyGradient(WeatherMood mood)
    {
        // Premium navy→blue base, softly tinted by weather mood.
        (GradientTop, GradientBottom) = mood switch
        {
            WeatherMood.Clear => (
                Color.FromArgb(255, 10, 28, 72),
                Color.FromArgb(255, 48, 110, 210)),
            WeatherMood.Cloudy => (
                Color.FromArgb(255, 28, 40, 68),
                Color.FromArgb(255, 70, 95, 130)),
            WeatherMood.Rain => (
                Color.FromArgb(255, 16, 28, 52),
                Color.FromArgb(255, 40, 70, 110)),
            WeatherMood.Snow => (
                Color.FromArgb(255, 36, 52, 80),
                Color.FromArgb(255, 110, 150, 200)),
            WeatherMood.Storm => (
                Color.FromArgb(255, 12, 14, 36),
                Color.FromArgb(255, 36, 40, 70)),
            WeatherMood.Fog => (
                Color.FromArgb(255, 40, 48, 64),
                Color.FromArgb(255, 90, 105, 120)),
            _ => (
                Color.FromArgb(255, 12, 24, 56),
                Color.FromArgb(255, 30, 72, 140)),
        };
    }
}

public sealed class HourlyForecastItem
{
    public string TimeLabel { get; init; } = string.Empty;

    public string Emoji { get; init; } = "🌡️";

    public string TemperatureText { get; init; } = "--°";

    public string PrecipitationChanceText { get; init; } = string.Empty;
}

public sealed class DailyForecastItem
{
    public string DateLabel { get; init; } = string.Empty;

    public string Emoji { get; init; } = "🌡️";

    public string Description { get; init; } = string.Empty;

    public string HighLowText { get; init; } = "-- / --";

    public string HighText { get; init; } = "--°";

    public string LowText { get; init; } = "--°";

    public string PrecipitationChanceText { get; init; } = string.Empty;
}
