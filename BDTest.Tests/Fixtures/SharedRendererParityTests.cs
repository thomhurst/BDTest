using System.Text.RegularExpressions;
using BDTest.Contracts;
using BDTest.Helpers;
using BDTest.Razor.Reports.Serialization;
using BDTest.Razor.Reports.Models.ViewModels;
using BDTest.Rendering;

namespace BDTest.Tests.Fixtures;

public class SharedRendererParityTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Execution_facade_and_contract_renderer_produce_identical_documents(bool historical)
    {
        var report = historical
            ? Required(HostedReportJson.Deserialize(HistoricalReportFixture.ReadJson()))
            : CreateReport();
        var json = BDTestReportJson.Serialize(report);
        var restored = BDTestReportJson.Deserialize(json);
        var direct = ReportHtmlRenderer.GenerateHtmlReport(restored);
        var facade = BDTestHtmlHelper.GenerateHtmlReport(restored);
        // Generation time is the only wall-clock value, and can cross a minute boundary.
        static string WithoutGenerationTime(string html) => Regex.Replace(html, @"Generated \d{4}-\d{2}-\d{2} \d{2}:\d{2} UTC", "Generated UTC");
        await Assert.That(WithoutGenerationTime(facade)).IsEqualTo(WithoutGenerationTime(direct));
        await Assert.That(BDTestReportJson.Serialize(restored)).IsEqualTo(json);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Hosted_pages_and_standalone_share_argument_and_exception_sections(bool outline)
    {
        var report = CreateReport();
        var scenario = report.Scenarios[0];
        var model = outline
            ? new ScenarioResult("outline", "report", "story", "outline", Status.Failed, examples: [scenario], isOutline: true)
            : scenario;
        var hosted = await ReportHandlerEncodingTests.RenderAsync(
            outline ? "/Views/Shared/_ScenarioExamples.cshtml" : "/Views/BDTest/Scenario.cshtml", model);
        var standalone = ReportHtmlRenderer.GenerateHtmlReport(new BDTestReport(1, "report", "tests", [model]));
        var arguments = ReportHtmlRenderer.RenderArguments(scenario.Steps[0].Arguments);
        var exception = ReportHtmlRenderer.RenderException(scenario.Exception);
        await Assert.That(hosted).Contains(arguments);
        await Assert.That(standalone).Contains(arguments);
        await Assert.That(hosted).Contains(exception);
        // The standalone original-text block adds copy/wrap controls; the diagnostic tree is identical.
        var tree = exception[..exception.IndexOf("<details><summary>Original exception text", StringComparison.Ordinal)];
        await Assert.That(standalone).Contains(tree);
        await Assert.That(hosted).Contains("Show all 25 rows");
        foreach (var html in new[] { hosted, standalone })
        {
            await Assert.That(html).Contains("&lt;unsafe&gt;");
            await Assert.That(html).Contains("inner-cause");
            await Assert.That(html).Contains("diagnostic-value");
            await Assert.That(html).DoesNotContain("<unsafe>");
        }
    }

    [Test]
    public async Task Historical_exception_sections_match_hosted_views()
    {
        var report = Required(HostedReportJson.Deserialize(HistoricalReportFixture.ReadJson()));
        var scenario = report.Scenarios.First(value => value.Exception != null && !value.IsOutline);
        var expected = ReportHtmlRenderer.RenderException(scenario.Exception);
        var hosted = await ReportHandlerEncodingTests.RenderAsync("/Views/BDTest/Scenario.cshtml", scenario,
            configureViewData: data => data[nameof(ReportLayoutViewModel)] = ReportLayoutViewModel.Create(report));
        await Assert.That(hosted).Contains(expected);
        await Assert.That(BDTest.Helpers.ExceptionHtmlHelper.GetHtml(scenario.Exception)).IsEqualTo(expected);
    }

    private static BDTestReport CreateReport()
    {
        var exception = new ExceptionInfo("Failure", "<unsafe>", "stack", "original diagnostic",
            data: new Dictionary<string, string> { ["diagnostic"] = "diagnostic-value" },
            innerExceptions: [new ExceptionInfo("Cause", "inner-cause")]);
        var step = new StepResult(StepType.Then, "Then <unsafe>", Status.Failed, exception: exception,
            arguments: [new Contracts.StepTable(["<unsafe>"], Enumerable.Range(0, 25)
                .Select(index => (IReadOnlyList<string>)new[] { index.ToString(System.Globalization.CultureInfo.InvariantCulture) }).ToArray()),
                new Contracts.StepDocString("<unsafe>", "text/plain")]);
        var scenario = new ScenarioResult("scenario", "report", "story", "scenario", Status.Failed, [step],
            exception: exception, testStartupInformation: "startup", tearDownOutput: "teardown", output: "output",
            customHtmlOutputForReport: "<p>Custom evidence</p>");
        return BDTestReportJson.Deserialize(BDTestReportJson.Serialize(new BDTestReport(1, "report", "tests", [scenario])));
    }
}
