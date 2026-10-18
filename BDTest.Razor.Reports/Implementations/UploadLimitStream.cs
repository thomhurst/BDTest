using Microsoft.AspNetCore.Http;

namespace BDTest.Razor.Reports.Implementations;

// Bounds actual reads, including chunked bodies and decompression. Never owns the underlying stream.
internal sealed class UploadLimitStream(Stream inner, long maximumBytes, string limitName) : Stream
{
    private long _bytesRead;

    private int ReadLength(int requested)
    {
        var remaining = maximumBytes - _bytesRead;
        // Read at most one byte beyond the limit so exact-size bodies can still reach EOF.
        return remaining < requested ? (int)remaining + 1 : requested;
    }

    private int Count(int read)
    {
        if (read > maximumBytes - _bytesRead)
        {
            throw new BadHttpRequestException($"Report upload exceeds {limitName}.", StatusCodes.Status413PayloadTooLarge);
        }
        _bytesRead += read;
        return read;
    }

    public override int Read(byte[] buffer, int offset, int count) => Count(inner.Read(buffer, offset, ReadLength(count)));

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => Count(await inner.ReadAsync(buffer, offset, ReadLength(count), cancellationToken));

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => Count(await inner.ReadAsync(buffer[..ReadLength(buffer.Length)], cancellationToken));

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
