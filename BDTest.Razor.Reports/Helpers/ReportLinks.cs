using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace BDTest.Razor.Reports.Helpers;

public static class ReportLinks
{
    public static string ReportAction(this IUrlHelper url, string action, object values = null) =>
        url.ActionContext.HttpContext.RequestServices.GetRequiredService<LinkGenerator>()
            .GetPathByAction(url.ActionContext.HttpContext, action, "BDTest", values)
        ?? throw new InvalidOperationException("MapBDTestReports must be called before rendering report links.");

    /// <summary>Returns the scenario detail URL, or null when a stored report lacks either ID.</summary>
    public static string ReportScenario(this IUrlHelper url, string reportId, string scenarioId) =>
        string.IsNullOrEmpty(reportId) || string.IsNullOrEmpty(scenarioId)
            ? null
            : url.ReportAction("SpecificScenario", new { reportId, scenarioId });

    public static string ReportAsset(this IUrlHelper url, string path) =>
        url.ActionContext.HttpContext.RequestServices.GetRequiredService<LinkGenerator>()
            .GetPathByName(url.ActionContext.HttpContext, "BDTest.Asset" + path,
                new { v = typeof(ReportLinks).Assembly.ManifestModule.ModuleVersionId.ToString("N") })
        ?? throw new InvalidOperationException("MapBDTestReports must be called before rendering report assets.");
}
