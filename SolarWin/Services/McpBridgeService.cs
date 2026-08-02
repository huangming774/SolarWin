using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SolarWin.Helpers;

namespace SolarWin.Services;

/// <summary>
/// Opt-in MCP Streamable HTTP server. It is deliberately bound to IPv4 loopback,
/// requires a per-install bearer token and rejects browser origins.
/// </summary>
public sealed class McpBridgeService : IMcpBridgeService
{
    private readonly IServiceProvider _appServices;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private WebApplication? _server;

    public McpBridgeService(IServiceProvider appServices) => _appServices = appServices;

    public bool IsRunning => _server is not null;

    public string Endpoint => $"http://127.0.0.1:{AppSettings.McpPort}/mcp";

    public string? LastError { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_server is not null)
            {
                return;
            }

            var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
            {
                ApplicationName = typeof(McpBridgeService).Assembly.FullName,
                Args = [],
            });
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls($"http://127.0.0.1:{AppSettings.McpPort}");
            builder.Services.AddSingleton(_appServices.GetRequiredService<IAuthService>());
            builder.Services.AddMcpServer()
                .WithHttpTransport()
                .WithTools<SolarWinMcpTools>();

            var server = builder.Build();
            server.Use(async (context, next) =>
            {
                if (!IsLoopback(context.Connection.RemoteIpAddress)
                    || !IsAllowedOrigin(context.Request.Headers.Origin)
                    || !HasValidToken(context.Request.Headers.Authorization))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }

                await next(context).ConfigureAwait(false);
            });
            server.MapMcp("/mcp");

            try
            {
                await server.StartAsync(cancellationToken).ConfigureAwait(false);
                _server = server;
                LastError = null;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                await server.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var server = _server;
            _server = null;
            if (server is null)
            {
                return;
            }

            try
            {
                await server.StopAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                await server.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private static bool IsLoopback(IPAddress? address)
        => address is not null && IPAddress.IsLoopback(address);

    private static bool IsAllowedOrigin(string? origin)
        => string.IsNullOrWhiteSpace(origin)
           || Uri.TryCreate(origin, UriKind.Absolute, out var uri)
           && (uri.IsLoopback || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase));

    private static bool HasValidToken(string? authorization)
    {
        const string prefix = "Bearer ";
        if (string.IsNullOrWhiteSpace(authorization)
            || !authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var supplied = authorization[prefix.Length..].Trim();
        var expected = AppSettings.McpAccessToken;
        return supplied.Length == expected.Length
               && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                   System.Text.Encoding.UTF8.GetBytes(supplied),
                   System.Text.Encoding.UTF8.GetBytes(expected));
    }
}
