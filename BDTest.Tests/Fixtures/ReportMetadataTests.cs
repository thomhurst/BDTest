using System.Net;
using BDTest.Helpers;
using BDTest.Settings;
using BDTest.Tests.Helpers;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
public class ReportMetadataTests
{
    [Test]
    public async Task DescriptorValuesAreExported(
        [Values("upload", "json", "html")] string path, [Values(false, true)] bool emptyValues)
    {
        var environment = emptyValues ? "" : "Descriptor environment";
        var tag = emptyValues ? "" : "Descriptor tag";
        var branchName = emptyValues ? "" : "Descriptor branch";
        var descriptor = new BDTestRunDescriptor { Environment = environment, Tag = tag, BranchName = branchName };

        await AssertMetadataAsync(path, descriptor, environment, tag, branchName);
    }

    [Test]
    public async Task NullDescriptorPropertyRemainsNullIndependently(
        [Values("upload", "json", "html")] string path,
        [Values("Environment", "Tag", "BranchName")] string missingProperty)
    {
        var descriptor = new BDTestRunDescriptor
        {
            Environment = "Descriptor environment", Tag = "Descriptor tag", BranchName = "Descriptor branch"
        };
        switch (missingProperty)
        {
            case "Environment": descriptor.Environment = null; break;
            case "Tag": descriptor.Tag = null; break;
            case "BranchName": descriptor.BranchName = null; break;
        }

        await AssertMetadataAsync(path, descriptor,
            missingProperty == "Environment" ? null : "Descriptor environment",
            missingProperty == "Tag" ? null : "Descriptor tag",
            missingProperty == "BranchName" ? null : "Descriptor branch");
    }

    public static IEnumerable<TestCaseData> MissingDescriptorCases()
    {
        foreach (var path in new[] { "upload", "json", "html" })
        foreach (var mode in new[] { "null", "empty" })
        {
            yield return new TestCaseData(path, mode);
        }
    }

    [TestCaseSource(nameof(MissingDescriptorCases))]
    public async Task MissingMetadataIsNullInJsonAndShownAsNotSpecifiedInHtml(
        string path, string descriptorMode)
    {

        await AssertMetadataAsync(path, descriptorMode == "null" ? null : new BDTestRunDescriptor(),
            null, null, null);
    }

    [Test]
    public void Missing_metadata_stays_null_in_the_shared_model()
    {
        using var run = BDTestRun.Start();

        var report = BDTestOutputModelBuilder.Build(run, new BDTestRunDescriptor());

        Assert.That(report.Environment, Is.Null);
        Assert.That(report.Tag, Is.Null);
        Assert.That(report.BranchName, Is.Null);
        Assert.That(BDTestHtmlHelper.GetTestHtmlReport(null, run),
            Does.Contain("<dt>Environment</dt><dd class=\"unset\">Not Specified</dd>")
                .And.Contain("<dt>Tag</dt><dd class=\"unset\">Not Specified</dd>")
                .And.Contain("<dt>Branch</dt><dd class=\"unset\">Not Specified</dd>"));
    }

    private static async Task AssertMetadataAsync(string path, BDTestRunDescriptor? descriptor,
        string? environment, string? tag, string? branchName)
    {
        using var run = BDTestRun.Start();
        if (path == "html")
        {
            var html = BDTestHtmlHelper.GetTestHtmlReport(descriptor, run);
            Assert.That(html, Does.Match($"<dt>Environment</dt><dd[^>]*>{System.Text.RegularExpressions.Regex.Escape(environment ?? "Not Specified")}</dd>"));
            Assert.That(html, Does.Match($"<dt>Tag</dt><dd[^>]*>{System.Text.RegularExpressions.Regex.Escape(tag ?? "Not Specified")}</dd>"));
            Assert.That(html, Does.Match($"<dt>Branch</dt><dd[^>]*>{System.Text.RegularExpressions.Regex.Escape(branchName ?? "Not Specified")}</dd>"));
            return;
        }

        string json;
        if (path == "upload")
        {
            using var handler = new MetadataUploadHandler();
            using var client = new HttpClient(handler);
            // The injected-client overload allows an entirely local upload assertion.
            await BDTestReportServer.SendDataAndGetReportUriAsync(new Uri("https://reports.example"), descriptor, client, run);
            json = Required(handler.UploadJson);
        }
        else
        {
            json = BDTestJsonHelper.GetTestJsonData(descriptor, run);
        }

        var metadata = JObject.Parse(json);
        Assert.That(metadata.Value<string>("Environment"), Is.EqualTo(environment));
        Assert.That(metadata.Value<string>("Tag"), Is.EqualTo(tag));
        Assert.That(metadata.Value<string>("BranchName"), Is.EqualTo(branchName));
    }

    private sealed class MetadataUploadHandler : HttpMessageHandler
    {
        public string? UploadJson { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post)
            {
                UploadJson = await Required(request.Content).ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("https://reports.example/bdtest/report/test-id")
            };
        }
    }
}
