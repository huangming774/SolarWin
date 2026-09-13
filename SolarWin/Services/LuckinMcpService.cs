using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Microsoft.Extensions.Logging;
using SolarWin.Helpers;
using System.Text.Json;
using Windows.Security.Credentials;

namespace SolarWin.Services;

public interface ILuckinMcpService
{
    Task<JsonElement> CallAsync(string tool, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default);
    Task SaveTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<bool> HasTokenAsync(CancellationToken cancellationToken = default);
}

public sealed class LuckinMcpService : ILuckinMcpService, IAsyncDisposable
{
    private const string Resource = "SolarWin.LuckinMcp";
    private const string User = "bearer";
    private readonly ILoggerFactory _loggerFactory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private McpClient? _client;

    public LuckinMcpService(ILoggerFactory loggerFactory) => _loggerFactory = loggerFactory;

    public async Task SaveTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        cancellationToken.ThrowIfCancellationRequested();
        var vault = new PasswordVault();
        try { var old = vault.Retrieve(Resource, User); vault.Remove(old); } catch { }
        vault.Add(new PasswordCredential(Resource, User, token.Trim()));
        await DisposeClientAsync().ConfigureAwait(false);
    }

    public Task<bool> HasTokenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var credential = new PasswordVault().Retrieve(Resource, User);
            credential.RetrievePassword();
            return Task.FromResult(!string.IsNullOrWhiteSpace(credential.Password));
        }
        catch { return Task.FromResult(false); }
    }

    public async Task<JsonElement> CallAsync(string tool, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var client = await GetClientAsync(cancellationToken).ConfigureAwait(false);
        var result = await client.CallToolAsync(tool, arguments, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (result.IsError == true)
        {
            var message = string.Join("\n", result.Content.OfType<TextContentBlock>().Select(x => x.Text));
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(message) ? "瑞幸服务返回错误" : message);
        }

        if (result.StructuredContent is JsonElement structured)
        {
            LuckinJson.ThrowIfBusinessError(structured);
            return structured;
        }

        var text = string.Join("\n", result.Content.OfType<TextContentBlock>().Select(x => x.Text));
        var parsed = JsonSerializer.Deserialize<JsonElement>(text);
        LuckinJson.ThrowIfBusinessError(parsed);
        return parsed;
    }

    private async Task<McpClient> GetClientAsync(CancellationToken cancellationToken)
    {
        if (_client is not null) return _client;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_client is not null) return _client;
            var vault = new PasswordVault();
            PasswordCredential credential;
            try { credential = vault.Retrieve(Resource, User); credential.RetrievePassword(); }
            catch { throw new InvalidOperationException("请先在点餐页面保存瑞幸 MCP 密钥"); }
            if (string.IsNullOrWhiteSpace(credential.Password)) throw new InvalidOperationException("瑞幸 MCP 密钥为空");
            var options = new HttpClientTransportOptions
            {
                Endpoint = new Uri(AppSettings.LuckinMcpEndpoint),
                TransportMode = HttpTransportMode.StreamableHttp,
                AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {credential.Password}" },
            };
            var transport = new HttpClientTransport(options, _loggerFactory);
            _client = await McpClient.CreateAsync(transport, new McpClientOptions(), _loggerFactory, cancellationToken).ConfigureAwait(false);
            return _client;
        }
        finally { _gate.Release(); }
    }

    private async Task DisposeClientAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try { if (_client is not null) await _client.DisposeAsync().ConfigureAwait(false); _client = null; }
        finally { _gate.Release(); }
    }

    public ValueTask DisposeAsync() => new(DisposeClientAsync());
}
