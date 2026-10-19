using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using BDTest.Contracts;

namespace BDTest.Rendering;

public static partial class ReportHtmlRenderer
{
    /// <summary>Renders the complete evidence and metadata of one scenario, without a document shell.</summary>
    public static string RenderScenario(ScenarioResult scenario, ReportRenderOptions? options = null)
    {
        if (scenario == null) throw new ArgumentNullException(nameof(scenario));
        var html = new StringBuilder("<div class=\"scenario-content\">\n");
        GenerateScenarioDetails(html, scenario, options);
        html.AppendLine("<dl class=\"side-dl\" aria-label=\"Scenario details\">");
        html.AppendLine($"<div><dt>Status</dt><dd>{Pill(Key(scenario.Status))}</dd></div>");
        if (scenario.TimedOut) html.AppendLine("<div><dt>Timeout</dt><dd>Timed out</dd></div>");
        html.AppendLine($"<div><dt>Duration</dt><dd class=\"mono\">{FormatDuration(scenario.TimeTaken)}</dd></div>");
        if (scenario.StartTime != default)
            html.AppendLine($"<div><dt>Started</dt><dd class=\"mono\">{FormatTime(scenario.StartTime, "yyyy-MM-dd HH:mm:ss.fff")} UTC</dd></div>");
        if (scenario.EndTime != default)
            html.AppendLine($"<div><dt>Finished</dt><dd class=\"mono\">{FormatTime(scenario.EndTime, "yyyy-MM-dd HH:mm:ss.fff")} UTC</dd></div>");
        if (!string.IsNullOrEmpty(scenario.FileName))
            html.AppendLine($"<div><dt>File</dt><dd class=\"mono\" title=\"{EscapeHtml(scenario.FileName)}\">{EscapeHtml(GetFileName(scenario.FileName))}</dd></div>");
        if (scenario.Tags.Count > 0)
        {
            html.Append("<div><dt>Tags</dt><dd><div class=\"tags\">");
            foreach (var tag in scenario.Tags)
                html.Append(options?.Hosted == true
                    ? $"<span class=\"tag\">@{EscapeHtml(tag)}</span>"
                    : $"<button class=\"tag\" type=\"button\" data-tag=\"{EscapeHtml(JsonSerializer.Serialize(tag, BDTestReportJsonContext.Default.String))}\" aria-label=\"Show only scenarios tagged {EscapeHtml(tag)}\">@{EscapeHtml(tag)}</button>");
            html.AppendLine("</div></dd></div>");
        }
        if (scenario.Timeout.HasValue)
            html.AppendLine($"<div><dt>Timeout limit</dt><dd>{FormatDuration(scenario.Timeout.Value)}</dd></div>");
        foreach (var information in scenario.CustomTestInformation)
            html.AppendLine($"<div><dt>Information</dt><dd>{EscapeHtml(information)}</dd></div>");
        html.AppendLine("</dl></div>");
        return html.ToString();
    }

    /// <summary>Renders outline rows using the same scenario composition.</summary>
    public static string RenderExamples(ScenarioResult scenario, ReportRenderOptions? options = null)
    {
        var html = new StringBuilder();
        if (scenario.IsOutline) GenerateExamplesTable(html, scenario, options);
        return html.ToString();
    }

    /// <summary>Renders retry status, timings and complete exception trees.</summary>
    public static string RenderRetryHistory(ScenarioResult scenario)
    {
        var html = new StringBuilder();
        AppendRetryHistory(html, scenario);
        return html.ToString();
    }

    /// <summary>Renders compact metadata for declared tests that never ran.</summary>
    public static string RenderNotRun(IEnumerable<ScenarioDefinitionSummary> notRun)
    {
        var rows = notRun.OrderBy(row => row.StoryText, StringComparer.Ordinal)
            .ThenBy(row => row.ScenarioText, StringComparer.Ordinal).ToList();
        var html = new StringBuilder();
        if (rows.Count > 0) GenerateNotRunSection(html, rows);
        return html.ToString();
    }

    /// <summary>Renders attachment metadata. Only host-resolved local routes can become links or raster previews.</summary>
    public static string RenderAttachments(IEnumerable<AttachmentInfo> attachments, ReportRenderOptions? options = null)
    {
        var html = new StringBuilder();
        // Match Razor's encoding for attributes and preserve all MIME parsing rules.
        static string Encode(string? value) => HtmlEncoder.Default.Encode(value ?? string.Empty);
        foreach (var attempt in attachments.GroupBy(file => file.AttemptNumber).OrderBy(group => group.Key))
        {
            html.AppendLine($"<section class=\"attachment-list\" aria-label=\"Attachments\" data-attempt=\"{attempt.Key}\"><div class=\"k\">Attachments · Attempt {attempt.Key}</div><ul>");
            foreach (var file in attempt)
            {
                var candidate = options?.AttachmentUrl?.Invoke(file);
                // A callback is a route resolver, not permission to embed captured remote/data/script URLs.
                var url = IsLocalRoute(candidate) ? candidate : null;
                var preview = url != null && MediaTypeHeaderValue.TryParse(file.MimeType, out var mime)
                    && mime.MediaType?.ToLowerInvariant() is "image/png" or "image/jpeg" or "image/gif" or "image/webp" or "image/bmp" or "image/avif";
                html.AppendLine($"<li class=\"attachment-item\" data-attachment-id=\"{Encode(file.Id)}\">");
                if (preview)
                {
                    html.AppendLine($"<button class=\"attachment-preview\" type=\"button\" data-bdtest-action=\"preview-attachment\" aria-label=\"Preview {Encode(file.Name)}\"><img src=\"{Encode(url)}\" alt=\"{Encode(file.Name)}\" loading=\"lazy\" data-attachment-image /></button>");
                    html.AppendLine("<span class=\"small muted\" data-attachment-unavailable hidden>Preview unavailable. Try downloading the attachment.</span>");
                }
                html.AppendLine("<div>");
                html.AppendLine(url == null
                    ? $"<span>{Encode(file.Name)}</span><span class=\"small muted\">Content unavailable</span>"
                    : $"<a class=\"lnk\" href=\"{Encode(url)}\" download>{Encode(file.Name)}</a>");
                html.AppendLine($"<div class=\"small muted\">{Encode(file.MimeType)} · {file.Length} bytes</div></div></li>");
            }
            html.AppendLine("</ul></section>");
        }
        return html.ToString();
    }

    private static bool IsLocalRoute(string? url) => !string.IsNullOrEmpty(url)
        && url!.StartsWith("/", StringComparison.Ordinal) && !url.StartsWith("//", StringComparison.Ordinal)
        && !url.Any(character => character == '\\' || char.IsControl(character));

    /// <summary>Renders uploaded HTML inside a scriptless, separately sandboxed document.</summary>
    public static string RenderCustomHtml(string content, bool hosted = false)
    {
        var document = "<!doctype html><html><head><meta charset=\"utf-8\">"
            + "<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; "
            + "style-src 'unsafe-inline'; img-src data: https: http:" + (hosted ? "" : " file:")
            + "; base-uri 'none'; form-action 'none'\"><meta name=\"referrer\" content=\"no-referrer\"></head><body>"
            + content + "</body></html>";
        return $"<iframe class=\"custom-body\" title=\"Custom test output\" sandbox=\"{(hosted ? "" : "allow-popups")}\" referrerpolicy=\"no-referrer\" srcdoc=\"{EscapeHtml(document)}\"></iframe>";
    }

    /// <summary>Shared styles, scoped to report fragment containers.</summary>
    public static string RenderStyles() => EmbeddedStyles;

    /// <summary>Shared event listeners for every report fragment on a page.</summary>
    public static string GetScript() => ReportScript;
}
