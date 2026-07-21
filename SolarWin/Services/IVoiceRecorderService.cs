namespace SolarWin.Services;

/// <summary>Microphone capture for chat voice messages.</summary>
public interface IVoiceRecorderService
{
    bool IsRecording { get; }

    /// <summary>Elapsed recording time while active.</summary>
    TimeSpan Elapsed { get; }

    event EventHandler<TimeSpan>? ElapsedChanged;

    /// <summary>Start recording to an in-memory buffer (16 kHz mono PCM WAV).</summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop recording and return WAV bytes + duration.
    /// Returns null when nothing was captured.
    /// </summary>
    Task<VoiceRecordingResult?> StopAsync(CancellationToken cancellationToken = default);

    /// <summary>Cancel and discard the current take.</summary>
    Task CancelAsync();
}

public sealed class VoiceRecordingResult
{
    public required byte[] WavBytes { get; init; }

    public required int DurationMs { get; init; }

    public required string FileName { get; init; }

    public string ContentType { get; init; } = "audio/wav";
}
