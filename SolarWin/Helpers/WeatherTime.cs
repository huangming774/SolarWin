using SolarWin.Models;

namespace SolarWin.Helpers;

internal static class WeatherTime
{
    internal static DateTime LocalNow(ForecastResponse forecast, DateTimeOffset utcNow)
    {
        if (!string.IsNullOrWhiteSpace(forecast.Timezone))
        {
            try { return TimeZoneInfo.ConvertTime(utcNow, TimeZoneInfo.FindSystemTimeZoneById(forecast.Timezone)).DateTime; }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return utcNow.UtcDateTime.AddSeconds(forecast.UtcOffsetSeconds);
    }
}
