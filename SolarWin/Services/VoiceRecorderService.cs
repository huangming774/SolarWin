using System.Diagnostics;
using NAudio.Wave;

namespace SolarWin.Services;

/// <summary>
/// WASAPI capture via NAudio → 16 kHz mono 16-bit WAV (suitable for chat voice clips).
/// </summary>
public sealed class VoiceRecorderService : IVoiceRecorderService, IDisposable
{
    private const int SampleRate = 16000;
    private const int BitsPerSample = 16;
    private const int Channels = 1;
    private const int MaxRecordingSeconds = 60;
    private const int MaxPcmBytes = SampleRate * (BitsPerSample / 8) * Channels * MaxRecordingSeconds;

    private readonly object _gate = new();
    private WaveInEvent? _waveIn;
    private MemoryStream? _pcm;
    private WaveFileWriter? _writer;
    private Stopwatch? _clock;
    private DispatcherTimerProxy? _tick;
    private int _pcmBytesWritten;

    public bool IsRecording { get; private set; }

    public TimeSpan Elapsed
    {
        get
        {
            var elapsed = _clock?.Elapsed ?? TimeSpan.Zero;
            return elapsed < MaxDuration ? elapsed : MaxDuration;
        }
    }

    public TimeSpan MaxDuration { get; } = TimeSpan.FromSeconds(MaxRecordingSeconds);

    public event EventHandler<TimeSpan>? ElapsedChanged;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (IsRecording)
            {
                return Task.CompletedTask;
            }

            _pcm = new MemoryStream();
            _pcmBytesWritten = 0;
            var format = new WaveFormat(SampleRate, BitsPerSample, Channels);
            _writer = new WaveFileWriter(new IgnoreDisposeStream(_pcm), format);
            _waveIn = new WaveInEvent
            {
                WaveFormat = format,
                BufferMilliseconds = 50,
            };
            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;
            _waveIn.StartRecording();
            _clock = Stopwatch.StartNew();
            IsRecording = true;

            _tick = new DispatcherTimerProxy(TimeSpan.FromMilliseconds(200), () =>
            {
                if (IsRecording)
                {
                    ElapsedChanged?.Invoke(this, Elapsed);
                }
            });
            _tick.Start();
        }

        return Task.CompletedTask;
    }

    public Task<VoiceRecordingResult?> StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!IsRecording || _waveIn is null || _writer is null || _pcm is null || _clock is null)
            {
                return Task.FromResult<VoiceRecordingResult?>(null);
            }

            try
            {
                _waveIn.StopRecording();
            }
            catch
            {
                // ignore
            }

            _tick?.Stop();
            _tick = null;
            _clock.Stop();
            var durationMs = (int)Math.Max(1, Elapsed.TotalMilliseconds);

            _writer.Flush();
            _writer.Dispose();
            _writer = null;

            _waveIn.DataAvailable -= OnDataAvailable;
            _waveIn.RecordingStopped -= OnRecordingStopped;
            _waveIn.Dispose();
            _waveIn = null;

            var stream = _pcm;
            _pcm = null;
            _clock = null;
            _pcmBytesWritten = 0;
            IsRecording = false;

            if (stream.Length < 100 || durationMs < 200)
            {
                stream.Dispose();
                return Task.FromResult<VoiceRecordingResult?>(null);
            }

            stream.Position = 0;
            var result = new VoiceRecordingResult
            {
                WavStream = stream,
                DurationMs = durationMs,
                FileName = $"voice_{DateTimeOffset.Now:yyyyMMdd_HHmmss}.wav",
                ContentType = "audio/wav",
            };
            return Task.FromResult<VoiceRecordingResult?>(result);
        }
    }

    public Task CancelAsync()
    {
        lock (_gate)
        {
            if (!IsRecording)
            {
                return Task.CompletedTask;
            }

            try
            {
                _waveIn?.StopRecording();
            }
            catch
            {
                // ignore
            }

            _tick?.Stop();
            _tick = null;
            CleanupRecordingState();
            IsRecording = false;
        }

        return Task.CompletedTask;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        lock (_gate)
        {
            if (_writer is null || e.BytesRecorded <= 0)
            {
                return;
            }

            var remaining = MaxPcmBytes - _pcmBytesWritten;
            if (remaining <= 0)
            {
                return;
            }

            var bytesToWrite = Math.Min(e.BytesRecorded, remaining);
            _writer.Write(e.Buffer, 0, bytesToWrite);
            _pcmBytesWritten += bytesToWrite;
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        // handled in Stop/Cancel
    }

    private void CleanupRecordingState()
    {
        try
        {
            _writer?.Dispose();
        }
        catch
        {
            // ignore
        }

        _writer = null;

        if (_waveIn is not null)
        {
            _waveIn.DataAvailable -= OnDataAvailable;
            _waveIn.RecordingStopped -= OnRecordingStopped;
            try
            {
                _waveIn.Dispose();
            }
            catch
            {
                // ignore
            }

            _waveIn = null;
        }

        try
        {
            _pcm?.Dispose();
        }
        catch
        {
            // ignore
        }

        _pcm = null;
        _clock = null;
        _pcmBytesWritten = 0;
    }

    public void Dispose()
    {
        _ = CancelAsync();
    }

    /// <summary>WaveFileWriter disposes the stream by default; we keep the MemoryStream open.</summary>
    private sealed class IgnoreDisposeStream : Stream
    {
        private readonly Stream _inner;

        public IgnoreDisposeStream(Stream inner) => _inner = inner;

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

        protected override void Dispose(bool disposing)
        {
            // do not dispose inner
        }
    }

    /// <summary>Lightweight timer that doesn't require a UI thread affinity at construction.</summary>
    private sealed class DispatcherTimerProxy
    {
        private readonly TimeSpan _interval;
        private readonly Action _callback;
        private CancellationTokenSource? _cts;

        public DispatcherTimerProxy(TimeSpan interval, Action callback)
        {
            _interval = interval;
            _callback = callback;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _ = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(_interval, token).ConfigureAwait(false);
                        if (!token.IsCancellationRequested)
                        {
                            _callback();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }, token);
        }

        public void Stop()
        {
            try
            {
                _cts?.Cancel();
            }
            catch
            {
                // ignore
            }

            _cts?.Dispose();
            _cts = null;
        }
    }
}
