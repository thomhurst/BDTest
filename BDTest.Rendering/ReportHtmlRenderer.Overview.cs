using System.Text;
using BDTest.Contracts;

namespace BDTest.Rendering;

public static partial class ReportHtmlRenderer
{
    /// <summary>Renders a run verdict, timing, metadata and status counts from a compact projection.</summary>
    /// <param name="data">Display data without scenario graphs.</param>
    /// <param name="statusUrl">Hosted navigation for a status, or null for all scenarios. Omit for offline filter controls.</param>
    /// <param name="notRunUrl">Hosted navigation for built but never run tests. Defaults to the offline warning section.</param>
    public static string RenderRunOverview(ReportRunOverview data, Func<Status?, string?>? statusUrl = null, string? notRunUrl = null)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        var html = new StringBuilder();
        GenerateVerdictSection(html, data);
        GenerateSummarySection(html, data, statusUrl, notRunUrl);
        return html.ToString();
    }
    private static void GenerateVerdictSection(StringBuilder html, ReportRunOverview data)
    {
        var total = data.ScenarioCount + data.NotRunCount;
        var failed = data.Counts[Status.Failed];
        var passed = data.Counts[Status.Passed];

        string verdict;
        if (data.ScenarioCount == 0)
            verdict = "<span class=\"v-word\">No scenarios ran</span>";
        else if (failed > 0)
            verdict = $"<span class=\"v-word\">{failed} failed</span> <span class=\"of\">of {total} scenarios</span>";
        else
            verdict = $"<span class=\"v-word\">{(passed == total ? "All passed" : "No failures")}</span> <span class=\"of\">of {total} scenarios</span>";

        var startedAt = data.Timer?.TestsStartedAt ?? default;
        var finishedAt = data.Timer?.TestsFinishedAt ?? default;
        var timing = startedAt == default || finishedAt == default
            ? "<span class=\"unset\">No timing recorded</span>"
            : $"<span class=\"mono\">{FormatDuration(finishedAt - startedAt)}</span> · {FormatTime(startedAt, "yyyy-MM-dd HH:mm:ss")} → {FormatTime(finishedAt, "HH:mm:ss")} UTC";

        html.AppendLine("<section class=\"wrap verdict\" aria-labelledby=\"verdict-h\">");
        html.AppendLine("<div>");
        html.AppendLine("<p class=\"eyebrow\">Run result</p>");
        html.AppendLine($"<h1 id=\"verdict-h\" class=\"verdict-h\" data-status=\"{(failed > 0 ? "failed" : data.ScenarioCount == 0 ? "all" : "passed")}\">{verdict}</h1>");
        html.AppendLine($"<p class=\"verdict-sub\">{timing}</p>");
        html.AppendLine("</div>");
        html.AppendLine("<dl class=\"meta\">");
        AppendMeta(html, "Environment", data.Environment, false);
        AppendMeta(html, "Tag", data.Tag, false);
        AppendMeta(html, "Branch", data.BranchName, true);
        AppendMeta(html, "Machine", data.MachineName, true);
        AppendMeta(html, "Version", data.ProducerVersion, true);
        html.AppendLine("</dl>");
        html.AppendLine("</section>");
    }

    private static void AppendMeta(StringBuilder html, string label, string? value, bool mono)
    {
        var cssClass = string.IsNullOrEmpty(value) || value == NotSpecified ? " class=\"unset\"" : mono ? " class=\"mono\"" : "";
        html.AppendLine($"<div><dt>{label}</dt><dd{cssClass}>{EscapeHtml(value ?? NotSpecified)}</dd></div>");
    }

    private static void GenerateSummarySection(StringBuilder html, ReportRunOverview data, Func<Status?, string?>? statusUrl, string? notRunUrl)
    {
        var total = data.ScenarioCount + data.NotRunCount;
        var counts = data.Counts;

        html.AppendLine("<section class=\"wrap\" aria-label=\"Test summary\">");
        if (total > 0)
        {
            var labels = Statuses.Where(s => counts[s] > 0)
                .Select(s => $"{counts[s]} {Label(Key(s)).ToLowerInvariant()}").ToList();
            if (data.NotRunCount > 0)
                labels.Add($"{data.NotRunCount} built but never run");
            html.AppendLine($"<div class=\"bar\" role=\"img\" aria-label=\"{EscapeHtml(string.Join(", ", labels))}\">");
            foreach (var status in Statuses.Where(s => counts[s] > 0))
            {
                html.AppendLine($"<div class=\"bar-seg\" data-status=\"{Key(status)}\" style=\"flex-grow: {counts[status]}\" title=\"{counts[status]} {Label(Key(status)).ToLowerInvariant()}\"></div>");
            }
            if (data.NotRunCount > 0)
                html.AppendLine($"<div class=\"bar-seg\" data-status=\"notrun\" style=\"flex-grow: {data.NotRunCount}\" title=\"{data.NotRunCount} built but never run\"></div>");
            html.AppendLine("</div>");
        }

        html.AppendLine("<div class=\"tiles\" role=\"group\" aria-label=\"Filter scenarios by status\">");
        AppendOverviewTile(html, null, total, "scenarios", data.ScenarioCount == 0, statusUrl);
        foreach (var status in Statuses)
        {
            var count = counts[status];
            var percent = total == 0 ? "0%" : $"{Math.Round(count * 100.0 / total)}%";
            AppendOverviewTile(html, status, count, percent, count == 0, statusUrl);
        }
        if (data.NotRunCount > 0)
        {
            var percent = $"{Math.Round(data.NotRunCount * 100.0 / total)}%";
            var url = OverviewUrl(notRunUrl ?? "#not-run");
            html.AppendLine($"<a class=\"tile\" data-status=\"notrun\" href=\"{EscapeHtml(url)}\"><span class=\"tile-top\"><span class=\"glyph\" data-g=\"notrun\" aria-hidden=\"true\"></span><span class=\"label\">Built but never run</span></span><span class=\"tile-n\">{data.NotRunCount}</span><span class=\"tile-pct\">{percent}</span></a>");
        }
        html.AppendLine("</div>");

        if (data.CustomProperties?.Count > 0)
        {
            html.AppendLine($"<details class=\"runprops\"><summary>Run properties ({data.CustomProperties.Count})</summary><dl>");
            foreach (var property in data.CustomProperties)
            {
                html.AppendLine($"<dt>{EscapeHtml(property.Key)}</dt><dd>{EscapeHtml(property.Value)}</dd>");
            }
            html.AppendLine("</dl></details>");
        }
        html.AppendLine("</section>");
    }

    private static void AppendOverviewTile(StringBuilder html, Status? status, int count, string detail,
        bool disabled, Func<Status?, string?>? statusUrl)
    {
        var key = status.HasValue ? Key(status.Value) : "all";
        var label = status.HasValue ? Label(key) : "Total";
        var url = statusUrl == null || disabled ? null : statusUrl(status);
        var tag = statusUrl == null ? "button" : url == null ? "div" : "a";
        html.Append($"<{tag} class=\"tile\" data-status=\"{key}\"");
        if (statusUrl == null)
            html.Append($" type=\"button\" data-filter=\"{key}\" aria-pressed=\"{(status == null ? "true" : "false")}\"{(disabled ? " disabled" : "")}");
        else if (url != null)
            html.Append($" href=\"{EscapeHtml(OverviewUrl(url))}\"");
        html.Append("><span class=\"tile-top\">");
        if (status.HasValue) html.Append($"<span class=\"glyph\" data-g=\"{key}\" aria-hidden=\"true\"></span>");
        html.AppendLine($"<span class=\"label\">{label}</span></span><span class=\"tile-n\">{count}</span><span class=\"tile-pct\">{detail}</span></{tag}>");
    }

    private static string OverviewUrl(string url)
    {
        // Caller-supplied navigation is encoded, and cannot introduce executable URI schemes.
        if (url.Any(char.IsControl) || url.IndexOf('\\') >= 0
            || (!url.StartsWith("/", StringComparison.Ordinal) && Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme != "https" && uri.Scheme != "http"))
            throw new ArgumentException("Report navigation must use relative or HTTP(S) URLs.", nameof(url));
        return url;
    }
}
