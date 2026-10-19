using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using BDTest.Contracts;

namespace BDTest.Helpers;

internal sealed class StreamingReportContent : HttpContent
{
    private readonly BDTestReport report;
    private readonly bool gzip;
    private readonly CancellationToken cancellationToken;

    internal StreamingReportContent(BDTestReport report, bool gzip, CancellationToken cancellationToken)
    {
        this.report = report;
        this.gzip = gzip;
        this.cancellationToken = cancellationToken;
        Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        if (gzip) Headers.ContentEncoding.Add("gzip");
    }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (gzip)
        {
            using var output = new GzipOutputStream(stream, cancellationToken);
            var compressed = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true);
            try
            {
                await JsonSerializer.SerializeAsync(compressed, report, BDTestReportJsonContext.Default.BDTestReport, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
#if NET8_0_OR_GREATER
                await compressed.DisposeAsync().ConfigureAwait(false);
#else
                // Older targets can only finish gzip synchronously. Capture those
                // remaining compressed bytes in memory, then send them asynchronously.
                compressed.Dispose();
#endif
            }
            await output.DrainAsync().ConfigureAwait(false);
        }
        else
        {
            await JsonSerializer.SerializeAsync(stream, report, BDTestReportJsonContext.Default.BDTestReport, cancellationToken).ConfigureAwait(false);
        }
    }

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }

    // Leave the HTTP stream open, and retain the attempt token even when gzip's
    // DisposeAsync calls WriteAsync without one. Synchronous gzip writes never
    // reach the network; only its final buffered bytes need temporary storage.
    private sealed class GzipOutputStream(Stream destination, CancellationToken attemptToken) : Stream
    {
        private MemoryStream? pending;

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => (pending ??= new MemoryStream()).Write(buffer, offset, count);

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await DrainAsync().ConfigureAwait(false);
            await destination.WriteAsync(buffer, offset, count, attemptToken).ConfigureAwait(false);
        }

#if NET8_0_OR_GREATER
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await DrainAsync().ConfigureAwait(false);
            await destination.WriteAsync(buffer, attemptToken).ConfigureAwait(false);
        }
#endif

        internal async Task DrainAsync()
        {
            attemptToken.ThrowIfCancellationRequested();
            if (pending == null || pending.Length == 0) return;
            await destination.WriteAsync(pending.GetBuffer(), 0, checked((int)pending.Length), attemptToken).ConfigureAwait(false);
            pending.SetLength(0);
            pending.Position = 0;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) pending?.Dispose();
            base.Dispose(disposing);
        }
    }
}
