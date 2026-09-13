using Microsoft.Extensions.DependencyInjection;
using System.Net;
using SolarWin.Data;
using SolarWin.Helpers;
using SolarWin.Repositories;
using SolarWin.ViewModels;

namespace SolarWin.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSolarWinServices(this IServiceCollection services)
    {
        // Local SQLite (per-account) + Phase 2 write pump + Phase 3 local reads.
        services.AddSingleton<IAccountDbContextFactory, AccountDbContextFactory>();
        services.AddSingleton<IChatWritePump, ChatWritePump>();
        services.AddSingleton<IChatLocalStore, ChatLocalStore>();
        services.AddSingleton<IRoomLocalStore, RoomLocalStore>();
        services.AddSingleton<IDbConnectionFactory, SqliteConnectionFactory>();
        services.AddSingleton<IChatAnalyticsRepository, ChatAnalyticsRepository>();
        services.AddSingleton<ITextTokenizer, LocalTextTokenizer>();
        services.AddSingleton<IWordCloudLayoutService, WordCloudLayoutService>();
        services.AddSingleton<IChatAnalyticsService, ChatAnalyticsService>();

        services.AddSingleton<ITokenStorage, PasswordVaultTokenStorage>();
        services.AddSingleton<IMlsDeviceIdProvider, PersistentMlsDeviceIdProvider>();
        services.AddSingleton<IMlsSecureStore, MlsSecureStore>();
        services.AddSingleton<IMlsClientService, MlsClientService>();
        services.AddSingleton<IAccountSessionService, AccountSessionService>();
        services.AddSingleton<ISystemNotificationService, SystemNotificationService>();
        services.AddSingleton<ITrayService, TrayService>();
        services.AddSingleton<IMcpBridgeService, McpBridgeService>();
        services.AddSingleton<ILuckinMcpService, LuckinMcpService>();
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

        services.AddHttpClient(AuthService.AnonymousHttpClientName, client =>
        {
            client.BaseAddress = new Uri(SolarApiClient.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(30);
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

        // User-configured OpenAI-compatible AI endpoint (dynamic base URL; long timeout for stream + 1M context)
        services.AddHttpClient(AiChatService.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromMinutes(30);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "SolarWin/1.1");
        });

        services.AddHttpClient(LinkPreviewService.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(8);
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "SolarWin/1.1 LinkPreview");
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
        });

        services.AddSingleton<IAiChatService, AiChatService>();
        services.AddSingleton<ISolarApiClient, SolarApiClient>();
        services.AddSingleton<SocialLoginService>();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<ICaptchaService, CaptchaService>();
        services.AddSingleton<IToastService, ToastService>();
        services.AddSingleton<IWeatherService, WeatherService>();
        services.AddSingleton<IVoiceRecorderService, VoiceRecorderService>();
        // LiveKit media for room realtime calls (voice / video)
        services.AddSingleton<IRealtimeCallService, LiveKitRealtimeCallService>();
        services.AddSingleton<IIncomingCallService, IncomingCallService>();
        services.AddSingleton<IChatWebSocketService, ChatWebSocketService>();
        services.AddSingleton<IChatMessageNotifier, ChatMessageNotifier>();
        // Messager API response cache (rooms / messages / members) — process-wide
        services.AddSingleton<IChatDataCache, ChatDataCache>();
        services.AddSingleton<DysonFileImageLoader>();
        services.AddSingleton<VideoMediaCache>();
        services.AddSingleton<LinkPreviewService>();
        services.AddSingleton<MainViewModel>();

        services.AddTransient<LoginViewModel>();
        services.AddTransient<RegisterViewModel>();
        services.AddTransient<RecoverViewModel>();
        services.AddTransient<HomeViewModel>();
        // Chat list UI keeps room items across navigations; data lives in IChatDataCache
        services.AddSingleton<ChatViewModel>();
        services.AddTransient<ChatDetailViewModel>();
        services.AddTransient<ChatDataCenterViewModel>();
        services.AddTransient<FilesViewModel>();
        services.AddTransient<NotificationsViewModel>();
        services.AddTransient<StellarProgramViewModel>();
        services.AddTransient<WalletViewModel>();
        // 点餐状态跨导航保留：菜单/购物车/订单号；登出时由 ShellPage.OnLoggedOut 调 OrderViewModel.Reset() 清空跨账号会话。
        services.AddSingleton<OrderViewModel>();
        // Posts feed keeps in-memory cache across navigations (detail page returns must not reload)
        services.AddSingleton<PostsViewModel>();
        services.AddTransient<PostDetailViewModel>();
        services.AddSingleton<WeatherViewModel>();
        // AI chat keeps history across navigations
        services.AddSingleton<AiViewModel>();
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
