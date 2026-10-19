using BDTest.Contracts;
using BDTest.Rendering;
using BDTest.Razor.Reports.Serialization;

namespace BDTest.ReportGenerator.Tests;

public class SharedRendererTests
{
    [Test]
    public async Task Renderer_is_signed_and_references_only_contracts_from_BDTest()
    {
        var assembly = typeof(ReportHtmlRenderer).Assembly;
        await Assert.That(assembly.GetName().Name).IsEqualTo("BDTest.Rendering");
        await Assert.That(assembly.GetName().GetPublicKey()).IsNotEmpty();
        await Assert.That(assembly.GetReferencedAssemblies().Where(reference => reference.Name!.StartsWith("BDTest", StringComparison.Ordinal))
            .Select(reference => reference.Name!).ToArray()).IsEquivalentTo(new[] { "BDTest.Contracts" });
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Independent_host_can_generate_offline_HTML_from_current_and_historical_contracts(bool historical)
    {
        var report = historical
            ? HostedReportJson.Deserialize(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "JsonData-v2.0.60.json")))!
            : new BDTestReport(1, "report", "test producer", [new ScenarioResult("scenario", "report", "story", "<scenario>", Status.Passed)]);
        // Serialize once more through source-generated metadata to exercise immutable input.
        var restored = BDTestReportJson.Deserialize(BDTestReportJson.Serialize(report));
        var html = ReportHtmlRenderer.GenerateHtmlReport(restored);
        await Assert.That(html).StartsWith("<!DOCTYPE html>");
        await Assert.That(html).Contains("Content-Security-Policy");
        await Assert.That(html).Contains("<style>");
        await Assert.That(html).Contains("<script>");
        await Assert.That(html).DoesNotContain("<script src=");
        await Assert.That(html).Contains(System.Net.WebUtility.HtmlEncode(restored.Scenarios[0].ScenarioText));
        await Assert.That(html).Contains("Run result");
        await Assert.That(html).Contains("Scenarios");
    }
}
