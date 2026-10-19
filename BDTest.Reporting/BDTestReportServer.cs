using System.Diagnostics.CodeAnalysis;
using System.Threading;
using BDTest.Helpers;

namespace BDTest;

[SuppressMessage("ReSharper", "UnusedMember.Global")]
[SuppressMessage("ReSharper", "UnusedType.Global")]
public static class BDTestReportServer
{
    /// <summary>Uploads only the scenarios and pending definitions owned by the supplied run.</summary>
    public static Task<Uri> SendDataAndGetReportUriAsync(BDTestRun run, Uri serverAddress, BDTestRunDescriptor? bdTestRunDescriptor)
        => SendDataAndGetReportUriAsync(run, serverAddress, bdTestRunDescriptor, CancellationToken.None);

    /// <summary>Uploads only the supplied run, allowing cancellation of warm-up, requests and retry waits.</summary>
    public static async Task<Uri> SendDataAndGetReportUriAsync(BDTestRun run, Uri serverAddress, BDTestRunDescriptor? bdTestRunDescriptor,
        CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        return await SendDataAndGetReportUriAsync(serverAddress, bdTestRunDescriptor, httpClient, run, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Uploads the supplied run using a caller-owned HTTP client.</summary>
    public static Task<Uri> SendDataAndGetReportUriAsync(Uri serverAddress, BDTestRunDescriptor? bdTestRunDescriptor,
        HttpClient httpClient, BDTestRun run)
        => SendDataAndGetReportUriAsync(serverAddress, bdTestRunDescriptor, httpClient, run, CancellationToken.None);

    /// <summary>Uploads the supplied run using a caller-owned HTTP client and cancellation token.</summary>
    /// <remarks>Cancellation does not dispose the client or remove captures, and may leave a partial report on the server.</remarks>
    public static Task<Uri> SendDataAndGetReportUriAsync(Uri serverAddress, BDTestRunDescriptor? bdTestRunDescriptor,
        HttpClient httpClient, BDTestRun run, CancellationToken cancellationToken)
        => ReportUploadTransport.UploadAsync(serverAddress, bdTestRunDescriptor, httpClient, run, cancellationToken);
}
