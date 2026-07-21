using System.Net.Http.Json;
using System.Text.Json;
using SolarWin.Helpers;
using SolarWin.Models;

namespace SolarWin.Services;

/// <summary>
/// Open-Meteo (forecast + air quality + geocoding) and ipwho.is (IP city) — no API keys.
/// </summary>
public sealed class WeatherService : IWeatherService
{
    public const string HttpClientName = "Weather";

    private static readonly GeoResult ShanghaiFallback = new()
    {
        Name = "上海",
        Latitude = 31.2304,
        Longitude = 121.4737,
        Country = "中国",
        Admin1 = "上海市",
    };

    private readonly IHttpClientFactory _httpClientFactory;

    public WeatherService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IReadOnlyList<GeoResult>> SearchCitiesAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return [];
        }

        var client = _httpClientFactory.CreateClient(HttpClientName);
        var url =
            $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(name.Trim())}&count=8&language=zh&format=json";

        try
        {
            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var payload = await response.Content
                .ReadFromJsonAsync<GeoSearchResponse>(JsonDefaults.Options, cancellationToken)
                .ConfigureAwait(false);
            return payload?.Results ?? [];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            throw new WeatherServiceException("城市搜索失败，请稍后重试。", ex);
        }
    }

    public async Task<ForecastResponse> GetForecastAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var lat = latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var lon = longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var url =
            "https://api.open-meteo.com/v1/forecast" +
            $"?latitude={lat}&longitude={lon}" +
            "&current=temperature_2m,relative_humidity_2m,apparent_temperature,weather_code,wind_speed_10m,precipitation" +
            "&hourly=temperature_2m,weather_code,precipitation_probability,uv_index,precipitation" +
            "&daily=weather_code,temperature_2m_max,temperature_2m_min,sunrise,sunset,uv_index_max,precipitation_sum,precipitation_probability_max" +
            "&timezone=auto&forecast_days=10";

        try
        {
            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var payload = await response.Content
                .ReadFromJsonAsync<ForecastResponse>(JsonDefaults.Options, cancellationToken)
                .ConfigureAwait(false);
            if (payload is null)
            {
                throw new WeatherServiceException("天气数据为空。");
            }

            return payload;
        }
        catch (WeatherServiceException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            throw new WeatherServiceException("获取天气预报失败，请检查网络后重试。", ex);
        }
    }

    public async Task<AirQualityResponse?> GetAirQualityAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var lat = latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var lon = longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var url =
            "https://air-quality-api.open-meteo.com/v1/air-quality" +
            $"?latitude={lat}&longitude={lon}" +
            "&current=us_aqi,european_aqi,pm2_5,pm10" +
            "&timezone=auto";

        try
        {
            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content
                .ReadFromJsonAsync<AirQualityResponse>(JsonDefaults.Options, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    public async Task<GeoResult> ResolveLocationFromIpAsync(CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        try
        {
            using var response = await client
                .GetAsync("https://ipwho.is/", cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return ShanghaiFallback;
            }

            var payload = await response.Content
                .ReadFromJsonAsync<IpWhoResponse>(JsonDefaults.Options, cancellationToken)
                .ConfigureAwait(false);

            if (payload is not { Success: true } ||
                (payload.Latitude == 0 && payload.Longitude == 0) ||
                string.IsNullOrWhiteSpace(payload.City))
            {
                return ShanghaiFallback;
            }

            return new GeoResult
            {
                Name = payload.City,
                Latitude = payload.Latitude,
                Longitude = payload.Longitude,
                Country = payload.Country,
                Admin1 = payload.Region,
            };
        }
        catch
        {
            return ShanghaiFallback;
        }
    }
}

public sealed class WeatherServiceException : Exception
{
    public WeatherServiceException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}
