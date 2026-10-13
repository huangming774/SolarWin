using SolarWin.Models;
using SolarWin.Services;
using SolarWin.ViewModels;

namespace SolarWin.Tests;

public sealed class WeatherViewModelTests : IDisposable
{
    private readonly string _cacheKey = "weather_test_" + Guid.NewGuid().ToString("N");
    public void Dispose() => SolarWin.Helpers.OfflineCache.Remove(_cacheKey);

    [Fact]
    public async Task ForecastIsVisibleBeforeAirQualityCompletes()
    {
        var service = new DelayedWeather();
        var vm = new WeatherViewModel(service, _cacheKey);
        var load = vm.SelectCityCommand.ExecuteAsync(City("A", 1));
        service.Forecasts[1].SetResult(Forecast(23));
        await UntilAsync(() => vm.HasData);

        Assert.Equal("23°", vm.TemperatureText);
        Assert.False(vm.IsLoading);
        Assert.False(load.IsCompleted);
        service.Air[1].SetResult(null);
        await load;
    }

    [Fact]
    public async Task SupersededCityCannotOverwriteNewCity()
    {
        var service = new DelayedWeather();
        var vm = new WeatherViewModel(service, _cacheKey);
        var first = vm.SelectCityCommand.ExecuteAsync(City("A", 1));
        var second = vm.SelectCityCommand.ExecuteAsync(City("B", 2));
        service.Forecasts[2].SetResult(Forecast(18));
        service.Air[2].SetResult(null);
        await second;
        service.Forecasts[1].SetResult(Forecast(30));
        service.Air[1].SetResult(null);
        await first;

        Assert.Equal("B", vm.LocationTitle);
        Assert.Equal("18°", vm.TemperatureText);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task LeavingPagePreventsLatePaint()
    {
        var service = new DelayedWeather();
        var vm = new WeatherViewModel(service, _cacheKey);
        var load = vm.SelectCityCommand.ExecuteAsync(City("A", 1));
        vm.CancelPendingRequests();
        service.Forecasts[1].SetResult(Forecast(30));
        service.Air[1].SetResult(null);
        await load;

        Assert.False(vm.HasData);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task RefreshFailureKeepsPreviousCityAndWeather()
    {
        var service = new DelayedWeather();
        var vm = new WeatherViewModel(service, _cacheKey);
        var first = vm.SelectCityCommand.ExecuteAsync(City("A", 1));
        service.Forecasts[1].SetResult(Forecast(20));
        service.Air[1].SetResult(null);
        await first;
        var second = vm.SelectCityCommand.ExecuteAsync(City("B", 2));
        service.Forecasts[2].SetException(new WeatherServiceException("unavailable"));
        service.Air[2].SetResult(null);
        await second;

        Assert.Equal("A", vm.LocationTitle);
        Assert.Equal("20°", vm.TemperatureText);
        Assert.True(vm.HasError);
        Assert.True(vm.HasData);
    }

    private static GeoResult City(string name, int latitude) => new() { Name = name, Latitude = latitude };
    private static ForecastResponse Forecast(double temperature) => new()
    {
        Timezone = "Asia/Shanghai",
        Current = new CurrentWeatherBlock { Temperature2m = temperature },
    };

    private static async Task UntilAsync(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (!predicate()) await Task.Delay(10, timeout.Token);
    }

    private sealed class DelayedWeather : IWeatherService
    {
        public Dictionary<double, TaskCompletionSource<ForecastResponse>> Forecasts { get; } = [];
        public Dictionary<double, TaskCompletionSource<AirQualityResponse?>> Air { get; } = [];

        public Task<ForecastResponse> GetForecastAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
        {
            var pending = new TaskCompletionSource<ForecastResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            Forecasts[latitude] = pending;
            return pending.Task;
        }
        public Task<AirQualityResponse?> GetAirQualityAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
        {
            var pending = new TaskCompletionSource<AirQualityResponse?>(TaskCreationOptions.RunContinuationsAsynchronously);
            Air[latitude] = pending;
            return pending.Task;
        }
        public Task<IReadOnlyList<GeoResult>> SearchCitiesAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<GeoResult>>([]);
        public Task<IReadOnlyList<GeoResult>> SearchChinaCitiesAsync(string name, CancellationToken cancellationToken = default)
            => SearchCitiesAsync(name, cancellationToken);
        public Task<GeoResult> ResolveLocationFromIpAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(City("A", 1));
    }
}
