using System.Diagnostics;
using System.Net;
using System.Text.Json;
using BDTest.Contracts;
using System.Threading;

namespace BDTest.Helpers;

internal static class ReportUploadTransport
{
    internal static async Task<BDTestReportUploadResult> UploadAsync(Uri serverAddress, BDTestRunDescriptor? bdTestRunDescriptor,
        HttpClient httpClient, BDTestRun run, CancellationToken cancellationToken,
        BDTestReportUploadOptions options, Action<CancellationTokenSource, TimeSpan>? scheduleAttemptTimeout = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dataOutputModel = BDTestOutputModelBuilder.Build(run, bdTestRunDescriptor);
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
        BDTestReportUploadResult? result = null;
        string? reportId = null;
        string? attachmentToken = null;
        await SendWithRetryAsync(httpClient, token =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, uploadUri)
            {
                Content = new StreamingReportContent(dataOutputModel, options.UseGzip, token)
            };
            request.Headers.Add("X-BDTest-Upload-Protocol", "1");
            // Per-request values prevent caller defaults from selecting a different response format.
            request.Headers.Accept.ParseAdd("application/json");
            return request;
        }, async response =>
        {
            reportId = response.Headers.TryGetValues("X-BDTest-Report-Id", out var ids) ? ids.Single() : null;
            attachmentToken = response.Headers.TryGetValues("X-BDTest-Attachment-Token", out var tokens) ? tokens.Single() : null;
            result = ParseResult(await response.Content.ReadAsStringAsync().ConfigureAwait(false), reportId);
            reportId = result.ReportId;
        }, cancellationToken, options, scheduleAttemptTimeout).ConfigureAwait(false);

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
                await SendWithRetryAsync(httpClient, _ =>
                {
                    var content = new StreamContent(run.OpenAttachment(attachmentId), 81920);
                    content.Headers.ContentType = mime;
                    content.Headers.ContentLength = attachment.Length;
                    var request = new HttpRequestMessage(HttpMethod.Post, attachmentUri) { Content = content };
                    request.Headers.Add("X-BDTest-Attachment-Token", attachmentToken);
                    return request;
                }, _ => Task.CompletedTask, cancellationToken, options, scheduleAttemptTimeout).ConfigureAwait(false);
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        return result ?? throw new InvalidOperationException("The report server returned no upload result.");
    }

    private static BDTestReportUploadResult ParseResult(string content, string? receiptId)
    {
        Uri? reportUrl;
        string? reportId = receiptId;
        var trimmed = content.TrimStart();
        if (trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            var result = JsonSerializer.Deserialize(content, BDTestReportJsonContext.Default.BDTestReportUploadResult)
                ?? throw new InvalidOperationException("The report server returned no upload result.");
            reportUrl = result.ReportUrl;
            reportId = result.ReportId;
            if (reportId?.Length != 32 || reportId.Any(character => !Uri.IsHexDigit(character)))
                throw new InvalidOperationException("The report server returned an invalid report ID.");
            if (receiptId != null && !string.Equals(receiptId, reportId, StringComparison.Ordinal))
                throw new InvalidOperationException("The report ID does not match the attachment receipt.");
        }
        else
        {
            // Older MVC hosts may format the legacy URI as a JSON string for Accept: application/json.
            if (trimmed.StartsWith("\"", StringComparison.Ordinal))
            {
                using var document = JsonDocument.Parse(content);
                content = document.RootElement.GetString() ?? string.Empty;
            }
            Uri.TryCreate(content, UriKind.Absolute, out reportUrl);
        }
        if (reportUrl == null || !reportUrl.IsAbsoluteUri ||
            (reportUrl.Scheme != Uri.UriSchemeHttp && reportUrl.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("The report server returned an invalid report URI.");
        return new BDTestReportUploadResult(reportUrl, reportId);
    }

    // A bare origin keeps the default mapping; a supplied path is the full report prefix.
    private static string GetReportBasePath(Uri serverAddress) => serverAddress.AbsolutePath == "/"
        ? "/bdtest/" : serverAddress.AbsolutePath.TrimEnd('/') + "/";

    private static async Task SendWithRetryAsync(HttpClient httpClient, Func<CancellationToken, HttpRequestMessage> createRequest,
        Func<HttpResponseMessage, Task> consumeResponse, CancellationToken cancellationToken, BDTestReportUploadOptions options,
        Action<CancellationTokenSource, TimeSpan>? scheduleAttemptTimeout)
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
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var attemptTimeout = options.Timeout;
                // Honor a shorter caller timeout through response buffering too.
                if (httpClient.Timeout != Timeout.InfiniteTimeSpan &&
                    (attemptTimeout == Timeout.InfiniteTimeSpan || httpClient.Timeout < attemptTimeout))
                    attemptTimeout = httpClient.Timeout;
                if (attemptTimeout != Timeout.InfiniteTimeSpan)
                {
                    // Tests can expire an attempt after its blocking operation starts.
                    // The hook is per upload; production uses the real deadline.
                    if (scheduleAttemptTimeout == null) timeout.CancelAfter(attemptTimeout);
                    else scheduleAttemptTimeout(timeout, attemptTimeout);
                }
                using var httpRequestMessage = createRequest(timeout.Token);
                if (options.UploadKey != null) httpRequestMessage.Headers.Add("X-BDTest-Upload-Key", options.UploadKey);
                using var response = await SendAndBufferAsync(httpClient, httpRequestMessage, timeout.Token).ConfigureAwait(false);
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
                && (exception is HttpRequestException || exception is OperationCanceledException))
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
