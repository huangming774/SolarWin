using SolarWin.Models;

namespace SolarWin.Services;

public interface IWeatherService
{
    /// <summary>Open-Meteo geocoding search.</summary>
    Task<IReadOnlyList<GeoResult>> SearchCitiesAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// 面向中国城市定位的增强搜索：除原始关键词外并发补查“××市”，合并去重并按
    /// “名称精确匹配（含 ××市 变体）&gt; 人口 &gt; 行政区划级别”排序，仅返回中国条目。
    /// Open-Meteo 中文搜索常漏掉主要城市（实测搜“珠海”只返回山东青岛的同名村庄），
    /// 直接取第一条会定位到错误城市。
    /// </summary>
    Task<IReadOnlyList<GeoResult>> SearchChinaCitiesAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Open-Meteo 10-day forecast + current + hourly.</summary>
    Task<ForecastResponse> GetForecastAsync(double latitude, double longitude, CancellationToken cancellationToken = default);

    /// <summary>Open-Meteo air quality (best-effort; null on failure).</summary>
    Task<AirQualityResponse?> GetAirQualityAsync(double latitude, double longitude, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve approximate location from public IP (ipwho.is, no key).
    /// Falls back to Shanghai when lookup fails.
    /// </summary>
    Task<GeoResult> ResolveLocationFromIpAsync(CancellationToken cancellationToken = default);
}
