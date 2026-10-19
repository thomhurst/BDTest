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
            using var compressed = new GZipStream(stream, CompressionLevel.Optimal, leaveOpen: true);
            await JsonSerializer.SerializeAsync(compressed, report, BDTestReportJsonContext.Default.BDTestReport, cancellationToken).ConfigureAwait(false);
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
}
