using SolarWin.Models;

namespace SolarWin.Services;

public interface IWeatherService
{
    /// <summary>Open-Meteo geocoding search.</summary>
    Task<IReadOnlyList<GeoResult>> SearchCitiesAsync(string name, CancellationToken cancellationToken = default);

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
