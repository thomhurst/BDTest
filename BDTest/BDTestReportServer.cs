using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using System.Threading;
using BDTest.Helpers;

namespace BDTest;

[SuppressMessage("ReSharper", "UnusedMember.Global")]
[SuppressMessage("ReSharper", "UnusedType.Global")]
public static class BDTestReportServer
{
    [Obsolete("SendDataAndGetReportUriAsync(Uri) is obsolete. You should instead use SendDataAndGetReportUriAsync(Uri, BDTestRunDescriptor)")]
    public static Task<Uri> SendDataAndGetReportUriAsync(Uri serverAddress)
    {
        return SendDataAndGetReportUriAsync(serverAddress, new BDTestRunDescriptor());
    }
        
    public static Task<Uri> SendDataAndGetReportUriAsync(Uri serverAddress, BDTestRunDescriptor? bdTestRunDescriptor)
        => SendDataAndGetReportUriAsync(serverAddress, bdTestRunDescriptor, CancellationToken.None);

    /// <summary>Uploads the default run, allowing cancellation of warm-up, requests and retry waits.</summary>
    public static async Task<Uri> SendDataAndGetReportUriAsync(Uri serverAddress, BDTestRunDescriptor? bdTestRunDescriptor,
        CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(2)
        };

        return await SendDataAndGetReportUriAsync(serverAddress, bdTestRunDescriptor, httpClient, cancellationToken).ConfigureAwait(false);
    }
        
    public static Task<Uri> SendDataAndGetReportUriAsync(Uri serverAddress, BDTestRunDescriptor? bdTestRunDescriptor,
        HttpClient httpClient) => SendDataAndGetReportUriAsync(serverAddress, bdTestRunDescriptor, httpClient, CancellationToken.None);

    /// <summary>Uploads the default run using a caller-owned HTTP client and cancellation token.</summary>
    public static Task<Uri> SendDataAndGetReportUriAsync(Uri serverAddress, BDTestRunDescriptor? bdTestRunDescriptor,
        HttpClient httpClient, CancellationToken cancellationToken)
        => SendDataAndGetReportUriAsync(serverAddress, bdTestRunDescriptor, httpClient, BDTestRun.Default, cancellationToken);

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
    public static async Task<Uri> SendDataAndGetReportUriAsync(Uri serverAddress, BDTestRunDescriptor? bdTestRunDescriptor,
        HttpClient httpClient, BDTestRun run, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dataOutputModel = BDTestOutputModelBuilder.Build(run, bdTestRunDescriptor);
        var pingUri = new UriBuilder(serverAddress)
        {
            Path = GetReportBasePath(serverAddress) + "ping"
        }.Uri;
            
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, pingUri);
            using var response = await SendAndBufferAsync(httpClient, request, cancellationToken).ConfigureAwait(false);
        }
        catch when (!cancellationToken.IsCancellationRequested)
        {
            // Result ignored - This is just to make sure the server is warmed up. If not, it'll warm it up!
        }
            
        cancellationToken.ThrowIfCancellationRequested();
        var uploadUri = new UriBuilder(serverAddress)
        {
            Path = GetReportBasePath(serverAddress) + "data"
        }.Uri;

        var attachments = BDTestAttachmentHelper.GetAttachments(dataOutputModel).ToArray();
        // Check ownership before posting JSON. File content is opened again for each upload attempt.
        foreach (var attachment in attachments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var content = run.OpenAttachment(attachment.Id ?? throw new InvalidOperationException("The attachment has no ID."));
            if (content.Length != attachment.Length) throw new InvalidDataException("Captured attachment length changed.");
        }
        cancellationToken.ThrowIfCancellationRequested();
        var stringContent = BDTestJsonHelper.Serialize(dataOutputModel);
        string? responseContent = null;
        string? reportId = null;
        string? attachmentToken = null;
        await SendWithRetryAsync(httpClient, () => new HttpRequestMessage(HttpMethod.Post, uploadUri)
        {
            Content = new StringContent(stringContent, Encoding.UTF8, "application/json")
        }, async response =>
        {
            responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (response.Headers.TryGetValues("X-BDTest-Report-Id", out var ids)) reportId = ids.Single();
            if (response.Headers.TryGetValues("X-BDTest-Attachment-Token", out var tokens)) attachmentToken = tokens.Single();
        }, cancellationToken).ConfigureAwait(false);

        if (attachments.Length > 0)
        {
            if (reportId?.Length != 32 || reportId.Any(character => !Uri.IsHexDigit(character)) || string.IsNullOrEmpty(attachmentToken))
                throw new InvalidOperationException("The report server did not authorize attachment uploads. Upgrade the server and configure AttachmentStore.");
            foreach (var attachment in attachments)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var attachmentId = attachment.Id ?? throw new InvalidOperationException("The attachment has no ID.");
                var attachmentUri = new Uri(uploadUri.AbsoluteUri.TrimEnd('/') + "/" + reportId + "/attachments/" + Uri.EscapeDataString(attachmentId));
                var mime = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(attachment.MimeType ?? throw new InvalidOperationException("The attachment has no MIME type."));
                await SendWithRetryAsync(httpClient, () =>
                {
                    var content = new StreamContent(run.OpenAttachment(attachmentId), 81920);
                    content.Headers.ContentType = mime;
                    content.Headers.ContentLength = attachment.Length;
                    var request = new HttpRequestMessage(HttpMethod.Post, attachmentUri) { Content = content };
                    request.Headers.Add("X-BDTest-Attachment-Token", attachmentToken);
                    return request;
                }, _ => Task.CompletedTask, cancellationToken).ConfigureAwait(false);
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new Uri(responseContent ?? throw new InvalidOperationException("The report server returned no report URI."));
    }

    // A bare origin keeps the default mapping; a supplied path is the full report prefix.
    private static string GetReportBasePath(Uri serverAddress) => serverAddress.AbsolutePath == "/"
        ? "/bdtest/" : serverAddress.AbsolutePath.TrimEnd('/') + "/";

    private static async Task SendWithRetryAsync(HttpClient httpClient, Func<HttpRequestMessage> createRequest,
        Func<HttpResponseMessage, Task> consumeResponse, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Three attempts, with 250ms then 500ms backoff unless the server asks for longer.
            var retryDelay = TimeSpan.FromMilliseconds(250 * attempt);
            DateTimeOffset? retryAt = null;
            var retryable = true;
                
            try
            {
                using var httpRequestMessage = createRequest();
                using var response = await SendAndBufferAsync(httpClient, httpRequestMessage, cancellationToken).ConfigureAwait(false);
                retryable = response.IsSuccessStatusCode || IsTransient(response.StatusCode);
                if (IsTransient(response.StatusCode))
                {
                    var retryAfter = response.Headers.RetryAfter;
                    if (retryAfter?.Delta > retryDelay)
                    {
                        retryDelay = retryAfter.Delta.Value;
                    }
                    retryAt = retryAfter?.Date;
                }

                cancellationToken.ThrowIfCancellationRequested();
                response.EnsureSuccessStatusCode();
                // The response is already buffered by the cancellable send helper.
                await consumeResponse(response).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                break;
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested && attempt < 3 && retryable
                && (exception is HttpRequestException || exception is TaskCanceledException))
            {
                await WaitForRetryAfterAsync(retryDelay, retryAt, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task<HttpResponseMessage> SendAndBufferAsync(HttpClient client,
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // ResponseHeadersRead ends HttpClient's timeout at the headers. Keep the same
        // per-attempt timeout through buffering, including on .NET Framework.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (client.Timeout != Timeout.InfiniteTimeSpan) timeout.CancelAfter(client.Timeout);
        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
        try
        {
            if (response.Content == null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return response;
            }
            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = timeout.Token.Register(() => cancelled.TrySetCanceled());
            var buffering = response.Content.LoadIntoBufferAsync(client.MaxResponseContentBufferSize);
            // netstandard2.0 has no cancellable buffering overload. Stop waiting and
            // dispose the response on cancellation even when custom content ignores it.
            if (await Task.WhenAny(buffering, cancelled.Task).ConfigureAwait(false) != buffering)
            {
                _ = buffering.ContinueWith(task => { _ = task.Exception; }, CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                cancellationToken.ThrowIfCancellationRequested();
                await cancelled.Task.ConfigureAwait(false);
            }
            await buffering.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (timeout.IsCancellationRequested) throw new TaskCanceledException("The HTTP response buffering timed out.");
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode) => statusCode is
        HttpStatusCode.RequestTimeout or (HttpStatusCode)429 or HttpStatusCode.InternalServerError
        or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

    private static async Task WaitForRetryAfterAsync(TimeSpan delay, DateTimeOffset? retryAt, CancellationToken cancellationToken)
    {
        var elapsed = Stopwatch.StartNew();
        while (true)
        {
            var remaining = delay - elapsed.Elapsed;
            if (retryAt.HasValue && retryAt.Value - DateTimeOffset.UtcNow > remaining)
            {
                remaining = retryAt.Value - DateTimeOffset.UtcNow;
            }
            if (remaining <= TimeSpan.Zero)
            {
                return;
            }

            // Timers have millisecond resolution and can wake before the deadline.
            // Round up, then recheck; cap each wait to Task.Delay's supported range.
            var milliseconds = (int)Math.Min(Math.Ceiling(remaining.TotalMilliseconds), int.MaxValue - 1);
            await Task.Delay(milliseconds, cancellationToken).ConfigureAwait(false);
        }
    }
}
