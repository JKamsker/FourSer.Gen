namespace FourSer.Tests.Behavioural.Infrastructure;

internal sealed class TrackingWriteStream : Stream
{
    public int WriteCallCount { get; private set; }

    public int LargestWriteBytes { get; private set; }

    public byte[] ToArray()
    {
        return _inner.ToArray();
    }

    public override bool CanRead => false;

    public override bool CanSeek => _inner.CanSeek;

    public override bool CanWrite => true;

    public override long Length => _inner.Length;

    public override long Position
    {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    private readonly MemoryStream _inner = new();

    public override void Flush()
    {
        _inner.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return _inner.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        _inner.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        RecordWrite(count);
        _inner.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        RecordWrite(buffer.Length);
        _inner.Write(buffer);
    }

    private void RecordWrite(int count)
    {
        WriteCallCount++;
        LargestWriteBytes = Math.Max(LargestWriteBytes, count);
    }
}
