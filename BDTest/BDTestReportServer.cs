using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using BDTest.Helpers;
using BDTest.Maps;
using BDTest.Settings;

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
        
    public static async Task<Uri> SendDataAndGetReportUriAsync(Uri serverAddress, BDTestRunDescriptor bdTestRunDescriptor)
    {
        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(2)
        };

        return await SendDataAndGetReportUriAsync(serverAddress, bdTestRunDescriptor, httpClient).ConfigureAwait(false);
    }
        
    public static Task<Uri> SendDataAndGetReportUriAsync(Uri serverAddress, BDTestRunDescriptor bdTestRunDescriptor,
        HttpClient httpClient) => SendDataAndGetReportUriAsync(serverAddress, bdTestRunDescriptor, httpClient, BDTestRun.Default);

    /// <summary>Uploads only the scenarios and pending definitions owned by the supplied run.</summary>
    public static async Task<Uri> SendDataAndGetReportUriAsync(BDTestRun run, Uri serverAddress, BDTestRunDescriptor bdTestRunDescriptor)
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        return await SendDataAndGetReportUriAsync(serverAddress, bdTestRunDescriptor, httpClient, run).ConfigureAwait(false);
    }

    /// <summary>Uploads the supplied run using a caller-owned HTTP client.</summary>
    public static async Task<Uri> SendDataAndGetReportUriAsync(Uri serverAddress, BDTestRunDescriptor bdTestRunDescriptor,
        HttpClient httpClient, BDTestRun run)
    {
        if (run == null) throw new ArgumentNullException(nameof(run));
        run.ThrowIfDisposed();
        var pingUri = new UriBuilder(serverAddress)
        {
            Path = "bdtest/ping"
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
            Path = "bdtest/data"
        }.Uri;

        var scenarios = BDTestUtil.GetScenarios(run);
            
        var dataOutputModel = new BDTestOutputModel
        {
            Id = run.ReportId,
            Environment = bdTestRunDescriptor?.Environment ?? BDTestSettings.LegacyEnvironment,
            Tag = bdTestRunDescriptor?.Tag ?? BDTestSettings.LegacyTag,
            BranchName = bdTestRunDescriptor?.BranchName ?? BDTestSettings.LegacyBranchName,
            MachineName = Environment.MachineName,
            Scenarios = scenarios,
            Version = BDTestVersionHelper.CurrentVersion,
            NotRun = BDTestUtil.GetNotRunScenarios(run),
            TestTimer = scenarios.Count == 0 ? new Output.TestTimer { TestsStartedAt = run.StartedAt } : BDTestUtil.GetTestTimer(scenarios)
        };

        AddCustomProperties(dataOutputModel);

        var stringContent = BDTestJsonHelper.Serialize(dataOutputModel);

        string responseContent;
        for (var attempt = 1; ; attempt++)
        {
            // Three attempts, with 250ms then 500ms backoff unless the server asks for longer.
            var retryDelay = TimeSpan.FromMilliseconds(250 * attempt);
            DateTimeOffset? retryAt = null;
            var retryable = true;
                
            try
            {
                using var httpRequestMessage = new HttpRequestMessage
                {
                    RequestUri = uploadUri,
                    Method = HttpMethod.Post,
                    Content = new StringContent(stringContent, Encoding.UTF8, "application/json")
                };

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

                responseContent = await response.EnsureSuccessStatusCode().Content.ReadAsStringAsync().ConfigureAwait(false);
                break;
            }
            catch (Exception exception) when (attempt < 3 && retryable
                && (exception is HttpRequestException || exception is TaskCanceledException))
            {
                await WaitForRetryAfterAsync(retryDelay, retryAt).ConfigureAwait(false);
            }
        }
            
        return new Uri(responseContent);
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

    private static void AddCustomProperties(BDTestOutputModel dataOutputModel)
    {
        foreach (var customProperty in BDTestSettings.CustomProperties)
        {
            dataOutputModel.CustomProperties.Add(customProperty.Key, customProperty.Value);
        }
    }
}
