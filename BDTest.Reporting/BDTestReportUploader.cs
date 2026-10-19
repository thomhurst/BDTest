using BDTest.Contracts;
using System.Threading;
using BDTest.Helpers;

namespace BDTest;

/// <summary>Streams an explicit run to a report server using a caller-owned HTTP client.</summary>
/// <remarks>Does not mutate or dispose the client. No warm-up request is sent.</remarks>
public sealed class BDTestReportUploader
{
    private readonly HttpClient client;
    private readonly BDTestReportUploadOptions options;

    public BDTestReportUploader(HttpClient httpClient, BDTestReportUploadOptions options)
    {
        client = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.options = (options ?? throw new ArgumentNullException(nameof(options))).ValidatedCopy();
    }

    /// <summary>Uploads a run snapshot followed by its attachments and returns the report URI.</summary>
    /// <remarks>Cancellation may leave a partial report. Captures remain owned by the run.</remarks>
    public async Task<Uri> UploadAsync(BDTestRun run, BDTestRunDescriptor? descriptor = null, CancellationToken cancellationToken = default)
        => (await UploadReportAsync(run, descriptor, cancellationToken).ConfigureAwait(false)).ReportUrl;

    /// <summary>Uploads a run and its attachments, returning the report URI and server-assigned ID.</summary>
    /// <remarks>Legacy servers without receipt headers return a null report ID. Cancellation may leave a partial report.</remarks>
    public Task<BDTestReportUploadResult> UploadReportAsync(BDTestRun run, BDTestRunDescriptor? descriptor = null, CancellationToken cancellationToken = default)
        => ReportUploadTransport.UploadAsync(options.ServerAddress, descriptor, client, run, cancellationToken, options);
}
