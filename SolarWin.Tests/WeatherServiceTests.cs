using System.Net;
using SolarWin.Helpers;
using SolarWin.Models;
using SolarWin.Services;

namespace SolarWin.Tests;

public sealed class WeatherServiceTests
{
    [Theory]
    [InlineData("Asia/Shanghai", 20)]
    [InlineData("America/New_York", 8)]
    public void ForecastClockUsesDestinationTimezone(string timezone, int hour)
    {
        var now = new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(hour, WeatherTime.LocalNow(new ForecastResponse { Timezone = timezone }, now).Hour);
    }

    [Fact]
    public void UnknownTimezoneUsesForecastUtcOffset()
    {
        var forecast = new ForecastResponse { Timezone = "invalid/weather-zone", UtcOffsetSeconds = 19800 };
        var local = WeatherTime.LocalNow(forecast, new DateTimeOffset(2026, 10, 14, 0, 0, 0, TimeSpan.Zero));
        Assert.Equal(new DateTime(2026, 10, 14, 5, 30, 0), local);
    }

    [Fact]
    public async Task CancelledSearchRemainsCancellationNotUserFacingError()
    {
        using var factory = new StubFactory();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new WeatherService(factory).SearchCitiesAsync("Shanghai", cts.Token));
    }

    [Fact]
    public async Task CancelledLocationDoesNotTurnIntoShanghaiFallback()
    {
        using var factory = new StubFactory();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new WeatherService(factory).ResolveLocationFromIpAsync(cts.Token));
    }

    [Fact]
    public async Task FailedAirQualityRemainsOptional()
    {
        using var factory = new StubFactory();
        Assert.Null(await new WeatherService(factory).GetAirQualityAsync(31, 121));
    }

    [Fact]
    public async Task CancelledForecastRemainsCancellation()
    {
        using var factory = new StubFactory();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new WeatherService(factory).GetForecastAsync(31, 121, cts.Token));
    }

    private sealed class StubFactory : HttpMessageHandler, IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }
}
