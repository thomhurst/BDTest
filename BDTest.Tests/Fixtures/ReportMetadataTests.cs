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
    private string _environment;
    private string _tag;
    private string _branchName;

#pragma warning disable CS0618 // Exercise the shipped legacy settings through their public setters.
    [SetUp]
    public void SetUp()
    {
        TestResetHelper.ResetData();
        _environment = BDTestSettings.Environment;
        _tag = BDTestSettings.Tag;
        _branchName = BDTestSettings.BranchName;
        SetLegacySettings("Legacy environment", "Legacy tag", "Legacy branch");
    }

    [TearDown]
    public void TearDown()
    {
        SetLegacySettings(_environment, _tag, _branchName);
        TestResetHelper.ResetData();
    }

    private static void SetLegacySettings(string environment, string tag, string branchName)
    {
        BDTestSettings.Environment = environment;
        BDTestSettings.Tag = tag;
        BDTestSettings.BranchName = branchName;
    }
#pragma warning restore CS0618

    [Test]
    public async Task DescriptorValuesOverrideLegacySettings(
        [Values("upload", "json", "html")] string path, [Values(false, true)] bool emptyValues)
    {
        var environment = emptyValues ? "" : "Descriptor environment";
        var tag = emptyValues ? "" : "Descriptor tag";
        var branchName = emptyValues ? "" : "Descriptor branch";
        var descriptor = new BDTestRunDescriptor { Environment = environment, Tag = tag, BranchName = branchName };

        await AssertMetadataAsync(path, descriptor, environment, tag, branchName);
    }

    [Test]
    public async Task NullDescriptorPropertyFallsBackIndependently(
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
            missingProperty == "Environment" ? "Legacy environment" : "Descriptor environment",
            missingProperty == "Tag" ? "Legacy tag" : "Descriptor tag",
            missingProperty == "BranchName" ? "Legacy branch" : "Descriptor branch");
    }

    public static IEnumerable<TestCaseData> DescriptorFallbackCases()
    {
        foreach (var path in new[] { "upload", "json", "html" })
        foreach (var mode in new[] { "null", "empty", "default overload" })
        {
            if (path == "upload" && mode == "default overload") continue;
            yield return new TestCaseData(path, mode);
        }
    }

    [TestCaseSource(nameof(DescriptorFallbackCases))]
    public async Task MissingDescriptorValuesUseLegacySettings(
        string path, string descriptorMode)
    {
        await AssertMetadataAsync(path, descriptorMode == "null" ? null : new BDTestRunDescriptor(),
            "Legacy environment", "Legacy tag", "Legacy branch", descriptorMode == "default overload");
    }

    [TestCaseSource(nameof(DescriptorFallbackCases))]
    public async Task MissingDescriptorAndLegacyValuesKeepPathSpecificDefaults(
        string path, string descriptorMode)
    {
        SetLegacySettings(null, null, null);

        await AssertMetadataAsync(path, descriptorMode == "null" ? null : new BDTestRunDescriptor(),
            null, null, null, descriptorMode == "default overload");
    }

    private static async Task AssertMetadataAsync(string path, BDTestRunDescriptor descriptor,
        string environment, string tag, string branchName, bool useDefaultOverload = false)
    {
        if (path == "html")
        {
            var html = useDefaultOverload
                ? BDTestHtmlHelper.GetTestHtmlReport()
                : BDTestHtmlHelper.GetTestHtmlReport(descriptor);
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
            await BDTestReportServer.SendDataAndGetReportUriAsync(new Uri("https://reports.example"), descriptor, client);
            json = handler.UploadJson;
        }
        else
        {
            json = useDefaultOverload
                ? BDTestJsonHelper.GetTestJsonData()
                : BDTestJsonHelper.GetTestJsonData(descriptor);
        }

        var metadata = JObject.Parse(json);
        Assert.That(metadata.Value<string>("Environment"), Is.EqualTo(environment));
        Assert.That(metadata.Value<string>("Tag"), Is.EqualTo(tag));
        Assert.That(metadata.Value<string>("BranchName"), Is.EqualTo(branchName));
    }

    private sealed class MetadataUploadHandler : HttpMessageHandler
    {
        public string UploadJson { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post)
            {
                UploadJson = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("https://reports.example/bdtest/report/test-id")
            };
        }
    }
}
