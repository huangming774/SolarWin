using System.Text.Json;

namespace SolarWin.Services;

public enum ChatWsConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Error,
}

/// <summary>
/// Solar Network gateway WebSocket (<c>wss://api.solian.app/ws</c>), same as Solian client.
/// </summary>
public interface IChatWebSocketService : IAsyncDisposable
{
    ChatWsConnectionState State { get; }

    event EventHandler<ChatWsConnectionState>? StateChanged;

    event EventHandler<ChatWsPacket>? PacketReceived;

    Task ConnectAsync(CancellationToken cancellationToken = default);

    Task DisconnectAsync();

    bool TrySendPing();
}

public sealed class ChatWsPacket
{
    public required string Type { get; init; }

    public JsonElement? Data { get; init; }

    public string? Endpoint { get; init; }

    public string? ErrorMessage { get; init; }

    public string RawJson { get; init; } = string.Empty;
}
