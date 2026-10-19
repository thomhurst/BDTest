using BDTest.Contracts;
using BDTest.Rendering;

namespace BDTest.Helpers;

public static class BDTestHtmlHelper
{
    /// <summary>Creates a report containing only the supplied run's scenarios and pending definitions.</summary>
    public static string GetTestHtmlReport(BDTestRunDescriptor? bdTestRunDescriptor, BDTestRun run) =>
        GenerateHtmlReport(BDTestOutputModelBuilder.Build(run, bdTestRunDescriptor));

    public static string GenerateHtmlReport(BDTestReport data) => ReportHtmlRenderer.GenerateHtmlReport(data);
}
