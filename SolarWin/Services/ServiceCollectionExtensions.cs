using Microsoft.Extensions.DependencyInjection;
using SolarWin.Helpers;
using SolarWin.ViewModels;

namespace SolarWin.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSolarWinServices(this IServiceCollection services)
    {
        services.AddSingleton<ITokenStorage, PasswordVaultTokenStorage>();
        services.AddSingleton<IAccountSessionService, AccountSessionService>();
        services.AddSingleton<ISystemNotificationService, SystemNotificationService>();
        services.AddSingleton<ITrayService, TrayService>();
        services.AddSingleton<IDeepLinkService, DeepLinkService>();

        // Named HttpClient used by SolarApiClient via IHttpClientFactory (safe for Singleton).
        // BaseAddress / User-Agent match Node 3 requirements.
        services.AddHttpClient(SolarApiClient.HttpClientName, client =>
        {
            client.BaseAddress = new Uri(SolarApiClient.BaseUrl.TrimEnd('/') + "/");
            // Allow larger Drive uploads/downloads.
            client.Timeout = TimeSpan.FromMinutes(10);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "SolarWin/1.1");
        });

        // Open-Meteo + IP geo (no API key)
        services.AddHttpClient(WeatherService.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "SolarWin/1.1");
        });

        services.AddSingleton<ISolarApiClient, SolarApiClient>();
        services.AddSingleton<SocialLoginService>();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<ICaptchaService, CaptchaService>();
        services.AddSingleton<IToastService, ToastService>();
        services.AddSingleton<IWeatherService, WeatherService>();
        services.AddSingleton<IVoiceRecorderService, VoiceRecorderService>();
        services.AddSingleton<IChatWebSocketService, ChatWebSocketService>();
        services.AddSingleton<IChatMessageNotifier, ChatMessageNotifier>();
        // Messager API response cache (rooms / messages / members) — process-wide
        services.AddSingleton<IChatDataCache, ChatDataCache>();
        services.AddSingleton<DysonFileImageLoader>();
        services.AddSingleton<MainViewModel>();

        services.AddTransient<LoginViewModel>();
        services.AddTransient<RegisterViewModel>();
        services.AddTransient<RecoverViewModel>();
        services.AddTransient<HomeViewModel>();
        // Chat list UI keeps room items across navigations; data lives in IChatDataCache
        services.AddSingleton<ChatViewModel>();
        services.AddTransient<ChatDetailViewModel>();
        services.AddTransient<FilesViewModel>();
        services.AddTransient<NotificationsViewModel>();
        services.AddTransient<WalletViewModel>();
        // Posts feed keeps in-memory cache across navigations (detail page returns must not reload)
        services.AddSingleton<PostsViewModel>();
        services.AddTransient<PostDetailViewModel>();
        services.AddSingleton<WeatherViewModel>();
        services.AddTransient<ProfileViewModel>();
        services.AddTransient<UserProfileViewModel>();
        services.AddTransient<RealmDetailViewModel>();
        services.AddTransient<SphereExploreViewModel>();
        services.AddTransient<ThinkingViewModel>();
        services.AddTransient<PublisherDetailViewModel>();
        services.AddTransient<PostFeedViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<SecurityViewModel>();
        services.AddTransient<MainPageViewModel>();

        return services;
    }
}
