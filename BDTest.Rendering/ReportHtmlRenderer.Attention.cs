using System.Text;

namespace BDTest.Rendering;

public static partial class ReportHtmlRenderer
{
    /// <summary>Renders shared failure-group cards from compact display data, retaining host-owned grouping and navigation.</summary>
    public static string RenderAttention(IReadOnlyList<ReportFailureOverview> groups, int failedScenarioCount,
        int? totalCauses = null, Func<string, string?>? scenarioUrl = null, string? allFailuresUrl = null)
    {
        if (groups == null) throw new ArgumentNullException(nameof(groups));
        if (groups.Count == 0) return string.Empty;
        var causeCount = totalCauses ?? groups.Count;
        var html = new StringBuilder();
        html.AppendLine("<section class=\"wrap sec\" aria-labelledby=\"att-h\">");
        html.AppendLine("<div class=\"sec-head\"><h2 id=\"att-h\" class=\"h2\">Needs attention</h2>");
        html.AppendLine($"<p class=\"sec-note\">{failedScenarioCount} failed {(failedScenarioCount == 1 ? "scenario traces" : "scenarios trace")} back to {causeCount} {(causeCount == 1 ? "cause" : "causes")}.</p>");
        if (allFailuresUrl != null)
            html.AppendLine($"<a href=\"{EscapeHtml(OverviewUrl(allFailuresUrl))}\">All failures by cause</a>");
        html.AppendLine("</div><div class=\"att-grid\">");
        foreach (var group in groups)
        {
            html.AppendLine("<article class=\"cause\">");
            html.AppendLine($"<div class=\"cause-top\"><span class=\"err-type\">{EscapeHtml(group.TypeName)}</span><span class=\"cause-n\">{group.ScenarioCount} {(group.ScenarioCount == 1 ? "scenario" : "scenarios")}</span></div>");
            html.AppendLine($"<p class=\"cause-msg\">{EscapeHtml(group.Message)}</p>");
            if (!string.IsNullOrEmpty(group.Location))
                html.AppendLine($"<div class=\"cause-where\">{EscapeHtml(group.Location)}</div>");
            html.AppendLine("<ul>");
            foreach (var scenario in group.Scenarios)
            {
                var url = scenarioUrl?.Invoke(scenario.ScenarioId);
                html.Append("<li>");
                if (scenarioUrl == null)
                    html.Append($"<button class=\"linkbtn\" type=\"button\" data-go=\"{EscapeHtml(scenario.ScenarioId)}\">{EscapeHtml(scenario.Name)}</button>");
                else if (url != null)
                    html.Append($"<a class=\"linkbtn\" href=\"{EscapeHtml(OverviewUrl(url))}\">{EscapeHtml(scenario.Name)}</a>");
                else
                    html.Append(EscapeHtml(scenario.Name));
                if (!string.IsNullOrEmpty(scenario.Description))
                    html.Append($"<div class=\"cause-where\">{EscapeHtml(scenario.Description)}</div>");
                if (scenario.Duration.HasValue)
                    html.Append($"<span class=\"dur\">{FormatDuration(scenario.Duration.Value)}</span>");
                html.AppendLine("</li>");
            }
            html.AppendLine("</ul>");
            if (allFailuresUrl != null && group.ScenarioCount > group.Scenarios.Count)
                html.AppendLine($"<a href=\"{EscapeHtml(OverviewUrl(allFailuresUrl))}\">{group.ScenarioCount - group.Scenarios.Count} more</a>");
            html.AppendLine("</article>");
        }
        html.AppendLine("</div>");
        if (allFailuresUrl != null && causeCount > groups.Count)
            html.AppendLine($"<a href=\"{EscapeHtml(OverviewUrl(allFailuresUrl))}\">{causeCount - groups.Count} more {(causeCount - groups.Count == 1 ? "cause" : "causes")}</a>");
        html.AppendLine("</section>");
        return html.ToString();
    }
}
