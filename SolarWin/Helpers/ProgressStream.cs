namespace SolarWin.Helpers;

/// <summary>Read-only stream wrapper that reports progress (0..1).</summary>
public sealed class ProgressStream : Stream
{
    private readonly Stream _inner;
    private readonly long _length;
    private readonly IProgress<double>? _progress;
    private long _position;

    public ProgressStream(Stream inner, long length, IProgress<double>? progress)
    {
        _inner = inner;
        _length = length > 0 ? length : inner.Length;
        _progress = progress;
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _length;
    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    public override void Flush() => _inner.Flush();

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = _inner.Read(buffer, offset, count);
        Report(read);
        return read;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var read = await _inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
        Report(read);
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var read = await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        Report(read);
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }

    private void Report(int read)
    {
        if (read <= 0)
        {
            return;
        }

        _position += read;
        if (_length > 0)
        {
            _progress?.Report(Math.Clamp((double)_position / _length, 0, 1));
        }
    }
}
