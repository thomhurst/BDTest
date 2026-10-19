using BDTest.Contracts;
using BDTest.Rendering;

namespace BDTest.Helpers;

public static class BDTestHtmlHelper
{
    /// <summary>Creates a report containing only the supplied run's scenarios and pending definitions.</summary>
    /// <param name="bdTestRunDescriptor">Optional export metadata; null uses default metadata.</param>
    /// <param name="run">The caller-owned run. Export after tests and teardown finish, before disposing it.</param>
    /// <returns>Self-contained HTML. The caller chooses whether and where to save it.</returns>
    public static string GetTestHtmlReport(BDTestRunDescriptor? bdTestRunDescriptor, BDTestRun run) =>
        GenerateHtmlReport(BDTestOutputModelBuilder.Build(run, bdTestRunDescriptor));

    /// <summary>Renders an immutable report without requiring a live run or writing a file.</summary>
    public static string GenerateHtmlReport(BDTestReport data) => ReportHtmlRenderer.GenerateHtmlReport(data);
}
