using System.Text.Json.Serialization;

namespace SolarWin.Models;

// —— Open-Meteo geocoding ——

public sealed class GeoSearchResponse
{
    [JsonPropertyName("results")]
    public List<GeoResult>? Results { get; set; }
}

public sealed class GeoResult
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("admin1")]
    public string? Admin1 { get; set; }

    [JsonPropertyName("timezone")]
    public string? Timezone { get; set; }

    public string DisplayName
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Name))
            {
                parts.Add(Name!);
            }

            if (!string.IsNullOrWhiteSpace(Admin1) &&
                !string.Equals(Admin1, Name, StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(Admin1!);
            }

            if (!string.IsNullOrWhiteSpace(Country))
            {
                parts.Add(Country!);
            }

            return parts.Count > 0 ? string.Join(" · ", parts) : "未知地点";
        }
    }
}

// —— Open-Meteo forecast ——

public sealed class ForecastResponse
{
    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }

    [JsonPropertyName("timezone")]
    public string? Timezone { get; set; }

    [JsonPropertyName("current")]
    public CurrentWeatherBlock? Current { get; set; }

    [JsonPropertyName("hourly")]
    public HourlyWeatherBlock? Hourly { get; set; }

    [JsonPropertyName("daily")]
    public DailyWeatherBlock? Daily { get; set; }
}

public sealed class CurrentWeatherBlock
{
    [JsonPropertyName("time")]
    public string? Time { get; set; }

    [JsonPropertyName("temperature_2m")]
    public double Temperature2m { get; set; }

    [JsonPropertyName("relative_humidity_2m")]
    public int RelativeHumidity2m { get; set; }

    [JsonPropertyName("apparent_temperature")]
    public double ApparentTemperature { get; set; }

    [JsonPropertyName("weather_code")]
    public int WeatherCode { get; set; }

    [JsonPropertyName("wind_speed_10m")]
    public double WindSpeed10m { get; set; }

    [JsonPropertyName("precipitation")]
    public double Precipitation { get; set; }
}

public sealed class HourlyWeatherBlock
{
    [JsonPropertyName("time")]
    public List<string>? Time { get; set; }

    [JsonPropertyName("temperature_2m")]
    public List<double>? Temperature2m { get; set; }

    [JsonPropertyName("weather_code")]
    public List<int>? WeatherCode { get; set; }

    [JsonPropertyName("precipitation_probability")]
    public List<int>? PrecipitationProbability { get; set; }

    [JsonPropertyName("uv_index")]
    public List<double>? UvIndex { get; set; }

    [JsonPropertyName("precipitation")]
    public List<double>? Precipitation { get; set; }
}

public sealed class DailyWeatherBlock
{
    [JsonPropertyName("time")]
    public List<string>? Time { get; set; }

    [JsonPropertyName("weather_code")]
    public List<int>? WeatherCode { get; set; }

    [JsonPropertyName("temperature_2m_max")]
    public List<double>? Temperature2mMax { get; set; }

    [JsonPropertyName("temperature_2m_min")]
    public List<double>? Temperature2mMin { get; set; }

    [JsonPropertyName("sunrise")]
    public List<string>? Sunrise { get; set; }

    [JsonPropertyName("sunset")]
    public List<string>? Sunset { get; set; }

    [JsonPropertyName("uv_index_max")]
    public List<double>? UvIndexMax { get; set; }

    [JsonPropertyName("precipitation_sum")]
    public List<double>? PrecipitationSum { get; set; }

    [JsonPropertyName("precipitation_probability_max")]
    public List<int>? PrecipitationProbabilityMax { get; set; }
}

// —— Open-Meteo air quality ——

public sealed class AirQualityResponse
{
    [JsonPropertyName("current")]
    public AirQualityCurrentBlock? Current { get; set; }
}

public sealed class AirQualityCurrentBlock
{
    [JsonPropertyName("time")]
    public string? Time { get; set; }

    [JsonPropertyName("us_aqi")]
    public int? UsAqi { get; set; }

    [JsonPropertyName("european_aqi")]
    public int? EuropeanAqi { get; set; }

    [JsonPropertyName("pm2_5")]
    public double? Pm25 { get; set; }

    [JsonPropertyName("pm10")]
    public double? Pm10 { get; set; }
}

// —— IP geolocation (ipwho.is, no key) ——

public sealed class IpWhoResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("region")]
    public string? Region { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

// —— UI / domain helpers ——

public enum WeatherMood
{
    Clear,
    Cloudy,
    Rain,
    Snow,
    Storm,
    Fog,
}

public static class WeatherCodeMapper
{
    public static string Describe(int code) => code switch
    {
        0 => "晴",
        1 => "大体晴",
        2 => "多云",
        3 => "阴",
        45 or 48 => "雾",
        51 or 53 or 55 => "毛毛雨",
        56 or 57 => "冻毛毛雨",
        61 or 63 or 65 => "雨",
        66 or 67 => "冻雨",
        71 or 73 or 75 => "雪",
        77 => "雪粒",
        80 or 81 or 82 => "阵雨",
        85 or 86 => "阵雪",
        95 => "雷暴",
        96 or 99 => "雷暴伴冰雹",
        _ => "未知",
    };

    public static string Emoji(int code) => code switch
    {
        0 => "☀️",
        1 or 2 => "⛅",
        3 => "☁️",
        45 or 48 => "🌫️",
        51 or 53 or 55 or 56 or 57 => "🌦️",
        61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 => "🌧️",
        71 or 73 or 75 or 77 or 85 or 86 => "❄️",
        95 or 96 or 99 => "⛈️",
        _ => "🌡️",
    };

    public static WeatherMood Mood(int code) => code switch
    {
        0 or 1 => WeatherMood.Clear,
        2 or 3 => WeatherMood.Cloudy,
        45 or 48 => WeatherMood.Fog,
        71 or 73 or 75 or 77 or 85 or 86 => WeatherMood.Snow,
        95 or 96 or 99 => WeatherMood.Storm,
        51 or 53 or 55 or 56 or 57 or 61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 => WeatherMood.Rain,
        _ => WeatherMood.Cloudy,
    };

    public static string UvLevel(double uv) => uv switch
    {
        < 3 => "低",
        < 6 => "中等",
        < 8 => "高",
        < 11 => "很高",
        _ => "极端",
    };

    public static string UvAdvice(double uv) => uv switch
    {
        < 3 => "无需特别防护",
        < 6 => "建议佩戴太阳镜",
        < 8 => "注意防晒",
        < 11 => "减少户外暴晒",
        _ => "尽量避免户外活动",
    };

    public static string AqiLevel(int aqi) => aqi switch
    {
        <= 50 => "优",
        <= 100 => "良",
        <= 150 => "轻度污染",
        <= 200 => "中度污染",
        <= 300 => "重度污染",
        _ => "严重污染",
    };

    public static string AqiAdvice(int aqi) => aqi switch
    {
        <= 50 => "空气清新，适宜户外",
        <= 100 => "空气质量可接受",
        <= 150 => "敏感人群减少户外",
        <= 200 => "建议减少长时间户外",
        <= 300 => "避免户外活动",
        _ => "尽量留在室内",
    };
}
