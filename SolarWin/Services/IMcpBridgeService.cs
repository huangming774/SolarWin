namespace SolarWin.Services;

/// <summary>Lifecycle for the opt-in, loopback-only MCP endpoint.</summary>
public interface IMcpBridgeService
{
    bool IsRunning { get; }

    string Endpoint { get; }

    string? LastError { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
