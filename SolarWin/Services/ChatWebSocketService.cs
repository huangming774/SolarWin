using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using SolarWin.Helpers;

namespace SolarWin.Services;

/// <summary>
/// ClientWebSocket to Solian gateway <c>/ws</c> with Bearer auth + ping heartbeat.
/// </summary>
public sealed class ChatWebSocketService : IChatWebSocketService
{
    public const string WsUrl = "wss://api.solian.app/ws";

    private readonly ITokenStorage _tokenStorage;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ClientWebSocket? _socket;
    private CancellationTokenSource? _loopCts;
    private Task? _receiveTask;
    private Task? _heartbeatTask;
    private int _generation;

    public ChatWebSocketService(ITokenStorage tokenStorage)
    {
        _tokenStorage = tokenStorage;
    }

    public ChatWsConnectionState State { get; private set; } = ChatWsConnectionState.Disconnected;

    public event EventHandler<ChatWsConnectionState>? StateChanged;

    public event EventHandler<ChatWsPacket>? PacketReceived;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (State is ChatWsConnectionState.Connected or ChatWsConnectionState.Connecting)
            {
                return;
            }

            await DisconnectCoreAsync().ConfigureAwait(false);

            var token = await _tokenStorage.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(token))
            {
                SetState(ChatWsConnectionState.Error);
                return;
            }

            SetState(ChatWsConnectionState.Connecting);
            var gen = Interlocked.Increment(ref _generation);

            _socket = new ClientWebSocket();
            _socket.Options.SetRequestHeader("Authorization", $"Bearer {token}");
            _socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);

            try
            {
                await _socket.ConnectAsync(new Uri(WsUrl), cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                _socket.Dispose();
                _socket = null;
                SetState(ChatWsConnectionState.Error);
                throw;
            }

            if (gen != _generation)
            {
                return;
            }

            SetState(ChatWsConnectionState.Connected);
            _loopCts = new CancellationTokenSource();
            var loopToken = _loopCts.Token;
            _receiveTask = Task.Run(() => ReceiveLoopAsync(gen, loopToken), loopToken);
            _heartbeatTask = Task.Run(() => HeartbeatLoopAsync(gen, loopToken), loopToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await DisconnectCoreAsync().ConfigureAwait(false);
            SetState(ChatWsConnectionState.Disconnected);
        }
        finally
        {
            _gate.Release();
        }
    }

    public bool TrySendPing()
    {
        var socket = _socket;
        if (socket is not { State: WebSocketState.Open })
        {
            return false;
        }

        try
        {
            var json = """{"type":"ping","data":null}""";
            var bytes = Encoding.UTF8.GetBytes(json);
            socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task ReceiveLoopAsync(int generation, CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        var sb = new StringBuilder();

        while (!cancellationToken.IsCancellationRequested && generation == _generation)
        {
            var socket = _socket;
            if (socket is not { State: WebSocketState.Open })
            {
                break;
            }

            try
            {
                sb.Clear();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await DisconnectAsync().ConfigureAwait(false);
                        _ = TryReconnectAsync();
                        return;
                    }

                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
                while (!result.EndOfMessage);

                HandleRawPacket(sb.ToString());
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                if (generation == _generation)
                {
                    SetState(ChatWsConnectionState.Error);
                    _ = TryReconnectAsync();
                }

                break;
            }
        }
    }

    private async Task HeartbeatLoopAsync(int generation, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && generation == _generation)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(45), cancellationToken).ConfigureAwait(false);
                if (generation != _generation)
                {
                    break;
                }

                TrySendPing();
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void HandleRawPacket(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            var type = root.TryGetProperty("type", out var t) ? t.GetString() ?? string.Empty : string.Empty;
            if (string.Equals(type, "pong", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (string.Equals(type, "error.dupe", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "error", StringComparison.OrdinalIgnoreCase))
            {
                var err = root.TryGetProperty("error_message", out var em) ? em.GetString()
                    : root.TryGetProperty("errorMessage", out var em2) ? em2.GetString() : null;
                PacketReceived?.Invoke(this, new ChatWsPacket
                {
                    Type = type,
                    ErrorMessage = err,
                    RawJson = raw,
                });
                return;
            }

            JsonElement? data = null;
            if (root.TryGetProperty("data", out var d) && d.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
            {
                data = d.Clone();
            }

            var endpoint = root.TryGetProperty("endpoint", out var ep) ? ep.GetString() : null;
            PacketReceived?.Invoke(this, new ChatWsPacket
            {
                Type = type,
                Data = data,
                Endpoint = endpoint,
                RawJson = raw,
            });
        }
        catch (JsonException)
        {
            // ignore malformed
        }
    }

    private async Task TryReconnectAsync()
    {
        await Task.Delay(1500).ConfigureAwait(false);
        try
        {
            var token = await _tokenStorage.GetAccessTokenAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            await ConnectAsync().ConfigureAwait(false);
        }
        catch
        {
            // leave disconnected; UI can manual reconnect
        }
    }

    private async Task DisconnectCoreAsync()
    {
        Interlocked.Increment(ref _generation);
        try
        {
            _loopCts?.Cancel();
        }
        catch
        {
            // ignore
        }

        if (_receiveTask is not null)
        {
            try
            {
                await _receiveTask.ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }
        }

        if (_heartbeatTask is not null)
        {
            try
            {
                await _heartbeatTask.ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }
        }

        _loopCts?.Dispose();
        _loopCts = null;
        _receiveTask = null;
        _heartbeatTask = null;

        if (_socket is not null)
        {
            try
            {
                if (_socket.State == WebSocketState.Open)
                {
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None)
                        .ConfigureAwait(false);
                }
            }
            catch
            {
                // ignore
            }

            _socket.Dispose();
            _socket = null;
        }
    }

    private void SetState(ChatWsConnectionState state)
    {
        if (State == state)
        {
            return;
        }

        State = state;
        StateChanged?.Invoke(this, state);
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        _gate.Dispose();
    }
}
