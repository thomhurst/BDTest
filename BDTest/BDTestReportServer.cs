using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
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
        
    public static async Task<Uri> SendDataAndGetReportUriAsync(Uri serverAddress, BDTestRunDescriptor? bdTestRunDescriptor)
    {
        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(2)
        };

        return await SendDataAndGetReportUriAsync(serverAddress, bdTestRunDescriptor, httpClient).ConfigureAwait(false);
    }
        
    public static Task<Uri> SendDataAndGetReportUriAsync(Uri serverAddress, BDTestRunDescriptor? bdTestRunDescriptor,
        HttpClient httpClient) => SendDataAndGetReportUriAsync(serverAddress, bdTestRunDescriptor, httpClient, BDTestRun.Default);

    /// <summary>Uploads only the scenarios and pending definitions owned by the supplied run.</summary>
    public static async Task<Uri> SendDataAndGetReportUriAsync(BDTestRun run, Uri serverAddress, BDTestRunDescriptor? bdTestRunDescriptor)
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        return await SendDataAndGetReportUriAsync(serverAddress, bdTestRunDescriptor, httpClient, run).ConfigureAwait(false);
    }

    /// <summary>Uploads the supplied run using a caller-owned HTTP client.</summary>
    public static async Task<Uri> SendDataAndGetReportUriAsync(Uri serverAddress, BDTestRunDescriptor? bdTestRunDescriptor,
        HttpClient httpClient, BDTestRun run)
    {
        var dataOutputModel = BDTestOutputModelBuilder.Build(run, bdTestRunDescriptor);
        var pingUri = new UriBuilder(serverAddress)
        {
            Path = GetReportBasePath(serverAddress) + "ping"
        }.Uri;
            
        try
        {
            using var response = await httpClient.GetAsync(pingUri).ConfigureAwait(false);
        }
        catch
        {
            // Result ignored - This is just to make sure the server is warmed up. If not, it'll warm it up!
        }
            
        var uploadUri = new UriBuilder(serverAddress)
        {
            Path = GetReportBasePath(serverAddress) + "data"
        }.Uri;

        var attachments = BDTestAttachmentHelper.GetAttachments(dataOutputModel).ToArray();
        // Check ownership before posting JSON. File content is opened again for each upload attempt.
        foreach (var attachment in attachments)
        {
            using var content = run.OpenAttachment(attachment.Id ?? throw new InvalidOperationException("The attachment has no ID."));
            if (content.Length != attachment.Length) throw new InvalidDataException("Captured attachment length changed.");
        }
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
        }).ConfigureAwait(false);

        if (attachments.Length > 0)
        {
            if (reportId?.Length != 32 || reportId.Any(character => !Uri.IsHexDigit(character)) || string.IsNullOrEmpty(attachmentToken))
                throw new InvalidOperationException("The report server did not authorize attachment uploads. Upgrade the server and configure AttachmentStore.");
            foreach (var attachment in attachments)
            {
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
                }, _ => Task.CompletedTask).ConfigureAwait(false);
            }
        }
        return new Uri(responseContent ?? throw new InvalidOperationException("The report server returned no report URI."));
    }

    // A bare origin keeps the default mapping; a supplied path is the full report prefix.
    private static string GetReportBasePath(Uri serverAddress) => serverAddress.AbsolutePath == "/"
        ? "/bdtest/" : serverAddress.AbsolutePath.TrimEnd('/') + "/";

    private static async Task SendWithRetryAsync(HttpClient httpClient, Func<HttpRequestMessage> createRequest,
        Func<HttpResponseMessage, Task> consumeResponse)
    {
        for (var attempt = 1; ; attempt++)
        {
            // Three attempts, with 250ms then 500ms backoff unless the server asks for longer.
            var retryDelay = TimeSpan.FromMilliseconds(250 * attempt);
            DateTimeOffset? retryAt = null;
            var retryable = true;
                
            try
            {
                using var httpRequestMessage = createRequest();
                using var response = await httpClient.SendAsync(httpRequestMessage).ConfigureAwait(false);
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

                response.EnsureSuccessStatusCode();
                await consumeResponse(response).ConfigureAwait(false);
                break;
            }
            catch (Exception exception) when (attempt < 3 && retryable
                && (exception is HttpRequestException || exception is TaskCanceledException))
            {
                await WaitForRetryAfterAsync(retryDelay, retryAt).ConfigureAwait(false);
            }
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode) => statusCode is
        HttpStatusCode.RequestTimeout or (HttpStatusCode)429 or HttpStatusCode.InternalServerError
        or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

    private static async Task WaitForRetryAfterAsync(TimeSpan delay, DateTimeOffset? retryAt)
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
            await Task.Delay(milliseconds).ConfigureAwait(false);
        }
    }
}
