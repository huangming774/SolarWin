using Microsoft.Extensions.DependencyInjection;
using SolarWin.Helpers;
using SolarWin.ViewModels;

namespace SolarWin.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSolarWinServices(this IServiceCollection services)
    {
        services.AddSingleton<ITokenStorage, PasswordVaultTokenStorage>();

        // Named HttpClient used by SolarApiClient via IHttpClientFactory (safe for Singleton).
        // BaseAddress / User-Agent match Node 3 requirements.
        services.AddHttpClient(SolarApiClient.HttpClientName, client =>
        {
            client.BaseAddress = new Uri(SolarApiClient.BaseUrl.TrimEnd('/') + "/");
            // Allow larger Drive uploads/downloads.
            client.Timeout = TimeSpan.FromMinutes(10);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "SolarWin/1.0");
        });

        services.AddSingleton<ISolarApiClient, SolarApiClient>();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<IToastService, ToastService>();
        services.AddSingleton<DysonFileImageLoader>();
        services.AddSingleton<MainViewModel>();

        services.AddTransient<LoginViewModel>();
        services.AddTransient<HomeViewModel>();
        // Chat list keeps in-memory cache across navigations
        services.AddSingleton<ChatViewModel>();
        services.AddTransient<ChatDetailViewModel>();
        services.AddTransient<FilesViewModel>();
        services.AddTransient<NotificationsViewModel>();
        services.AddTransient<ProfileViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<MainPageViewModel>();

        return services;
    }
}
