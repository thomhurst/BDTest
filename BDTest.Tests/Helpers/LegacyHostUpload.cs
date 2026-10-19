using System.Net.Http.Headers;
using System.Text;
using BDTest.Helpers;
using BDTest.Maps;

namespace BDTest.Tests.Helpers;

// Legacy host endpoint tests keep using legacy payloads until the hosted migration (#936).
// Production versioned uploads are covered independently by capture handlers and rejection tests.
internal static class LegacyHostUpload
{
    internal static async Task<Uri> SendAsync(Uri address, BDTestRunDescriptor? descriptor, HttpClient client, BDTestRun run)
    {
        var model = new BDTestOutputModel
        {
            Id = run.ReportId,
            Scenarios = run.Store.ScenariosByInternalId.Values.Where(s => s.OutlineId == null).ToList(),
            NotRun = run.Store.NotRun.Values.Select(d => d.ToSummary()).ToList(),
            Environment = descriptor?.Environment, Tag = descriptor?.Tag, BranchName = descriptor?.BranchName,
            Version = BDTestVersionHelper.CurrentVersion
        };
        var prefix = address.AbsolutePath == "/" ? "/bdtest/" : address.AbsolutePath.TrimEnd('/') + "/";
        var endpoint = new UriBuilder(address) { Path = prefix + "data" }.Uri;
        using var payload = new StringContent(BDTestJsonHelper.Serialize(model), Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(endpoint, payload);
        response.EnsureSuccessStatusCode();
        var result = new Uri(await response.Content.ReadAsStringAsync());
        var attachments = BDTestAttachmentHelper.GetAttachments(model).ToArray();
        if (attachments.Length == 0) return result;
        var reportId = response.Headers.GetValues("X-BDTest-Report-Id").Single();
        var token = response.Headers.GetValues("X-BDTest-Attachment-Token").Single();
        foreach (var attachment in attachments)
        {
            using var content = new StreamContent(run.OpenAttachment(attachment.Id!));
            content.Headers.ContentType = MediaTypeHeaderValue.Parse(attachment.MimeType!);
            content.Headers.ContentLength = attachment.Length;
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint.AbsoluteUri + "/" + reportId + "/attachments/" + attachment.Id) { Content = content };
            request.Headers.Add("X-BDTest-Attachment-Token", token);
            using var uploaded = await client.SendAsync(request);
            uploaded.EnsureSuccessStatusCode();
        }
        return result;
    }
}
