using BDTest.Attributes;
using BDTest.Maps;
using BDTest.Output;
using BDTest.Settings;
using BDTest.Test;
using System.Globalization;
using System.Text;

namespace BDTest.Helpers;

public static class BDTestHtmlHelper
{
    private const string NotSpecified = "Not Specified";

    // Display order for status tiles, filters and story totals.
    private static readonly Status[] Statuses =
        { Status.Passed, Status.Failed, Status.Skipped, Status.Inconclusive, Status.NotImplemented };

    public static string GetTestHtmlReport()
    {
        return GetTestHtmlReport(new BDTestRunDescriptor());
    }

    public static string GetTestHtmlReport(BDTestRunDescriptor bdTestRunDescriptor) => GetTestHtmlReport(bdTestRunDescriptor, BDTestRun.Default);

    /// <summary>Creates a report containing only the supplied run's scenarios and pending definitions.</summary>
    public static string GetTestHtmlReport(BDTestRunDescriptor bdTestRunDescriptor, BDTestRun run)
    {
        if (run == null) throw new ArgumentNullException(nameof(run));
        run.ThrowIfDisposed();
        var scenarios = BDTestUtil.GetScenarios(run);
        var testTimer = GetTestTimer(scenarios, run);
        
        var dataToOutput = new BDTestOutputModel
        {
            Id = run.ReportId,
            Environment = bdTestRunDescriptor?.Environment ?? BDTestSettings.LegacyEnvironment ?? NotSpecified,
            Tag = bdTestRunDescriptor?.Tag ?? BDTestSettings.LegacyTag ?? NotSpecified,
            BranchName = bdTestRunDescriptor?.BranchName ?? BDTestSettings.LegacyBranchName ?? NotSpecified,
            MachineName = Environment.MachineName,
            Scenarios = scenarios,
            TestTimer = testTimer,
            NotRun = BDTestUtil.GetNotRunScenarios(run),
            Version = BDTestVersionHelper.CurrentVersion
        };

        return GenerateHtmlReport(dataToOutput);
    }

    private static TestTimer GetTestTimer(IEnumerable<Scenario> scenarios, BDTestRun run)
    {
        var enumerable = scenarios.ToList();

        if (enumerable.Count == 0)
        {
            return new TestTimer { TestsStartedAt = run.StartedAt };
        }

        var testTimer = new TestTimer
        {
            TestsStartedAt = enumerable.GetStartDateTime(),
            TestsFinishedAt = enumerable.GetEndDateTime()
        };

        return testTimer;
    }

    private static string GenerateHtmlReport(BDTestOutputModel data)
    {
        // Group by story so the page, the story headers and the client-side sort share one order.
        var stories = data.Scenarios
            .GroupBy(scenario => scenario.GetStoryText())
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.OrderBy(scenario => scenario.GetScenarioText(), StringComparer.Ordinal).ToList())
            .ToList();

        var html = new StringBuilder();

        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset=\"UTF-8\">");
        html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        html.AppendLine("<title>BDTest Report</title>");
        html.AppendLine(EmbeddedStyles);
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("<div class=\"rpt\" data-grouped=\"true\">");

        html.AppendLine("<header class=\"top\"><div class=\"wrap top-row\">");
        html.AppendLine("<div class=\"brand\"><svg width=\"24\" height=\"24\" viewBox=\"0 0 24 24\" aria-hidden=\"true\"><rect x=\"1\" y=\"1\" width=\"22\" height=\"22\" rx=\"6\" fill=\"currentColor\"></rect><path d=\"M7 8h10M7 12h7M7 16h10\" stroke=\"var(--bg)\" stroke-width=\"2\" stroke-linecap=\"round\"></path></svg><span>BDTest</span><span class=\"brand-sub\">Test report</span></div>");
        html.AppendLine($"<div class=\"gen\">Generated {FormatTime(DateTime.UtcNow, "yyyy-MM-dd HH:mm")} UTC · single file, works offline</div>");
        html.AppendLine("</div></header>");

        html.AppendLine("<main>");
        GenerateVerdictSection(html, data);
        GenerateSummarySection(html, data);

        if (data.Scenarios.Count == 0)
        {
            html.AppendLine("<section class=\"wrap sec\"><div class=\"empty\">");
            html.AppendLine("<p class=\"empty-title\">The report was generated before any test finished.</p>");
            html.AppendLine("<p>Call <code>GetTestHtmlReport()</code> after your tests finish, for example in a <code>[OneTimeTearDown]</code>, so every scenario is included.</p>");
            html.AppendLine("</div></section>");
        }
        else
        {
            GenerateAttentionSection(html, data);
            GenerateScenariosSection(html, data.Scenarios, stories);
        }

        if (data.NotRun.Any())
        {
            GenerateNotRunSection(html, data.NotRun);
        }
        html.AppendLine("</main>");

        html.AppendLine("<footer class=\"wrap foot\"><span>BDTest · snapshot of one test run</span><span>Times are UTC.</span></footer>");
        html.AppendLine("</div>");
        html.AppendLine(EmbeddedScript);
        html.AppendLine("</body>");
        html.AppendLine("</html>");

        return html.ToString();
    }

    private static void GenerateVerdictSection(StringBuilder html, BDTestOutputModel data)
    {
        var total = data.Scenarios.Count;
        var failed = data.Scenarios.Count(s => s.Status == Status.Failed);
        var passed = data.Scenarios.Count(s => s.Status == Status.Passed);

        string verdict;
        if (total == 0)
            verdict = "<span class=\"v-word\">No scenarios ran</span>";
        else if (failed > 0)
            verdict = $"<span class=\"v-word\">{failed} failed</span> <span class=\"of\">of {total} scenarios</span>";
        else
            verdict = $"<span class=\"v-word\">{(passed == total ? "All passed" : "No failures")}</span> <span class=\"of\">of {total} scenarios</span>";

        var startedAt = data.TestTimer?.TestsStartedAt ?? default;
        var finishedAt = data.TestTimer?.TestsFinishedAt ?? default;
        var timing = startedAt == default || finishedAt == default
            ? "<span class=\"unset\">No timing recorded</span>"
            : $"<span class=\"mono\">{FormatDuration(finishedAt - startedAt)}</span> · {FormatTime(startedAt, "yyyy-MM-dd HH:mm:ss")} → {FormatTime(finishedAt, "HH:mm:ss")} UTC";

        html.AppendLine("<section class=\"wrap verdict\" aria-labelledby=\"verdict-h\">");
        html.AppendLine("<div>");
        html.AppendLine("<p class=\"eyebrow\">Run result</p>");
        html.AppendLine($"<h1 id=\"verdict-h\" class=\"verdict-h\" data-status=\"{(failed > 0 ? "failed" : total == 0 ? "all" : "passed")}\">{verdict}</h1>");
        html.AppendLine($"<p class=\"verdict-sub\">{timing}</p>");
        html.AppendLine("</div>");
        html.AppendLine("<dl class=\"meta\">");
        AppendMeta(html, "Environment", data.Environment, false);
        AppendMeta(html, "Tag", data.Tag, false);
        AppendMeta(html, "Branch", data.BranchName, true);
        AppendMeta(html, "Machine", data.MachineName, true);
        AppendMeta(html, "Version", data.Version, true);
        html.AppendLine("</dl>");
        html.AppendLine("</section>");
    }

    private static void AppendMeta(StringBuilder html, string label, string value, bool mono)
    {
        var cssClass = string.IsNullOrEmpty(value) || value == NotSpecified ? " class=\"unset\"" : mono ? " class=\"mono\"" : "";
        html.AppendLine($"<div><dt>{label}</dt><dd{cssClass}>{EscapeHtml(value)}</dd></div>");
    }

    private static void GenerateSummarySection(StringBuilder html, BDTestOutputModel data)
    {
        var scenarios = data.Scenarios;
        var total = scenarios.Count;
        var counts = Statuses.ToDictionary(status => status, status => scenarios.Count(s => s.Status == status));

        html.AppendLine("<section class=\"wrap\" aria-label=\"Test summary\">");
        if (total > 0)
        {
            var label = string.Join(", ", Statuses.Where(s => counts[s] > 0).Select(s => $"{counts[s]} {Label(Key(s)).ToLowerInvariant()}"));
            html.AppendLine($"<div class=\"bar\" role=\"img\" aria-label=\"{EscapeHtml(label)}\">");
            foreach (var status in Statuses.Where(s => counts[s] > 0))
            {
                html.AppendLine($"<div class=\"bar-seg\" data-status=\"{Key(status)}\" style=\"flex-grow: {counts[status]}\" title=\"{counts[status]} {Label(Key(status)).ToLowerInvariant()}\"></div>");
            }
            html.AppendLine("</div>");
        }

        html.AppendLine("<div class=\"tiles\" role=\"group\" aria-label=\"Filter scenarios by status\">");
        html.AppendLine($"<button class=\"tile\" type=\"button\" data-status=\"all\" data-filter=\"all\" aria-pressed=\"true\"{(total == 0 ? " disabled" : "")}><span class=\"tile-top\"><span class=\"label\">Total</span></span><span class=\"tile-n\">{total}</span><span class=\"tile-pct\">scenarios</span></button>");
        foreach (var status in Statuses)
        {
            var key = Key(status);
            var count = counts[status];
            var percent = total == 0 ? "0%" : $"{Math.Round(count * 100.0 / total)}%";
            html.AppendLine($"<button class=\"tile\" type=\"button\" data-status=\"{key}\" data-filter=\"{key}\" aria-pressed=\"false\"{(count == 0 ? " disabled" : "")}><span class=\"tile-top\"><span class=\"glyph\" data-g=\"{key}\" aria-hidden=\"true\"></span><span class=\"label\">{Label(key)}</span></span><span class=\"tile-n\">{count}</span><span class=\"tile-pct\">{percent}</span></button>");
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

    private static void GenerateAttentionSection(StringBuilder html, BDTestOutputModel data)
    {
        // Failures that share an exception type and message usually share a cause.
        var causes = new List<(ExceptionWrapper Exception, List<(Scenario Scenario, string Suffix)> Scenarios)>();
        void AddCause(ExceptionWrapper exception, Scenario scenario, string suffix)
        {
            var cause = causes.FirstOrDefault(c => c.Exception.TypeName == exception.TypeName && c.Exception.Message == exception.Message);
            if (cause.Exception == null)
            {
                cause = (exception, new List<(Scenario, string)>());
                causes.Add(cause);
            }
            cause.Scenarios.Add((scenario, suffix));
        }

        foreach (var scenario in data.Scenarios.Where(s => s.Status == Status.Failed).OrderBy(s => s.GetScenarioText(), StringComparer.Ordinal))
        {
            if (scenario.IsOutline)
            {
                for (var index = 0; index < scenario.Examples.Count; index++)
                {
                    var row = scenario.Examples[index];
                    if (row.Status == Status.Failed && row.Exception != null)
                        AddCause(row.Exception, scenario, $" · example {index + 1}");
                }
            }
            else if (scenario.Exception != null)
            {
                AddCause(scenario.Exception, scenario, "");
            }
        }

        var failedCount = data.Scenarios.Count(s => s.Status == Status.Failed);
        if (causes.Count == 0)
        {
            return;
        }

        html.AppendLine("<section class=\"wrap sec\" aria-labelledby=\"att-h\">");
        html.AppendLine("<div class=\"sec-head\">");
        html.AppendLine("<h2 id=\"att-h\" class=\"h2\">Needs attention</h2>");
        html.AppendLine($"<p class=\"sec-note\">{failedCount} failed {(failedCount == 1 ? "scenario traces" : "scenarios trace")} back to {causes.Count} {(causes.Count == 1 ? "cause" : "causes")}.</p>");
        html.AppendLine("</div>");
        html.AppendLine("<div class=\"att-grid\">");
        foreach (var cause in causes.OrderByDescending(c => c.Scenarios.Count))
        {
            var where = GetFirstFrame(cause.Exception.StackTrace);
            html.AppendLine("<article class=\"cause\">");
            html.AppendLine($"<div class=\"cause-top\"><span class=\"err-type\">{EscapeHtml(cause.Exception.TypeName)}</span><span class=\"cause-n\">{cause.Scenarios.Count} {(cause.Scenarios.Count == 1 ? "scenario" : "scenarios")}</span></div>");
            html.AppendLine($"<p class=\"cause-msg\">{EscapeHtml(cause.Exception.Message)}</p>");
            if (where != null)
            {
                html.AppendLine($"<div class=\"cause-where\">at {EscapeHtml(where)}</div>");
            }
            html.AppendLine("<ul>");
            foreach (var (scenario, suffix) in cause.Scenarios)
            {
                html.AppendLine($"<li><button class=\"linkbtn\" type=\"button\" data-go=\"{EscapeHtml(scenario.Guid)}\">{EscapeHtml(scenario.GetScenarioText() + suffix)}</button></li>");
            }
            html.AppendLine("</ul>");
            html.AppendLine("</article>");
        }
        html.AppendLine("</div>");

        var also = new List<string>();
        foreach (var status in new[] { Status.Inconclusive, Status.NotImplemented, Status.Skipped })
        {
            var count = data.Scenarios.Count(s => s.Status == status);
            if (count > 0)
                also.Add($"<span class=\"also-item\"><span class=\"glyph\" data-g=\"{Key(status)}\" aria-hidden=\"true\"></span><button class=\"linkbtn\" type=\"button\" data-show=\"{Key(status)}\">{count} {Label(Key(status)).ToLowerInvariant()}</button></span>");
        }
        if (data.NotRun.Count > 0)
        {
            also.Add($"<span class=\"also-item\"><span class=\"glyph\" data-g=\"notrun\" aria-hidden=\"true\"></span><a href=\"#not-run\">{data.NotRun.Count} built but never run</a></span>");
        }
        if (also.Count > 0)
        {
            html.AppendLine($"<div class=\"also\"><span>Also in this run:</span>{string.Concat(also)}</div>");
        }
        html.AppendLine("</section>");
    }

    private static void GenerateScenariosSection(StringBuilder html, List<Scenario> scenarios, List<List<Scenario>> stories)
    {
        var tags = scenarios.SelectMany(s => s.Tags ?? Array.Empty<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList();

        html.AppendLine("<section class=\"sec\" aria-labelledby=\"scn-h\">");
        html.AppendLine("<div class=\"wrap\"><div class=\"sec-head\" style=\"margin-bottom: 0\"><h2 id=\"scn-h\" class=\"h2\">Scenarios</h2></div></div>");
        html.AppendLine("<div class=\"toolbar\"><div class=\"wrap\">");
        html.AppendLine("<div class=\"tb-row\">");
        html.AppendLine("<div class=\"search\"><label for=\"q\" class=\"sr\">Search scenarios</label><svg width=\"16\" height=\"16\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><circle cx=\"11\" cy=\"11\" r=\"7\"></circle><path d=\"m20 20-3.5-3.5\"></path></svg><input id=\"q\" type=\"search\" placeholder=\"Scenario, step, story or tag\" autocomplete=\"off\"></div>");
        html.AppendLine("<div class=\"seg\" role=\"group\" aria-label=\"Status\">");
        html.AppendLine($"<button class=\"seg-btn\" type=\"button\" data-filter=\"all\" aria-pressed=\"true\"><span>All</span><span class=\"n\">{scenarios.Count}</span></button>");
        foreach (var status in Statuses)
        {
            var count = scenarios.Count(s => s.Status == status);
            if (count > 0)
                html.AppendLine($"<button class=\"seg-btn\" type=\"button\" data-filter=\"{Key(status)}\" aria-pressed=\"false\"><span>{Label(Key(status))}</span><span class=\"n\">{count}</span></button>");
        }
        html.AppendLine("</div>");
        if (tags.Count > 0)
        {
            html.AppendLine("<label class=\"ctl\">Tag <select id=\"tag\"><option value=\"\">All tags</option>");
            foreach (var tag in tags)
            {
                html.AppendLine($"<option value=\"{EscapeHtml(tag.ToLowerInvariant())}\">@{EscapeHtml(tag)}</option>");
            }
            html.AppendLine("</select></label>");
        }
        html.AppendLine("<label class=\"ctl\">Sort <select id=\"sort\"><option value=\"story\">Story, then name</option><option value=\"status\">Failures first</option><option value=\"duration\">Slowest first</option></select></label>");
        html.AppendLine("<label class=\"ctl\"><input id=\"group\" type=\"checkbox\" checked> Group by story</label>");
        html.AppendLine("<div class=\"tb-actions\"><button class=\"ghost\" type=\"button\" data-expand=\"true\">Expand all</button><button class=\"ghost\" type=\"button\" data-expand=\"false\">Collapse all</button></div>");
        html.AppendLine("</div>");
        html.AppendLine($"<div class=\"tb-sub\"><span id=\"showing\">{scenarios.Count} scenarios across {stories.Count} {(stories.Count == 1 ? "story" : "stories")}</span><button class=\"linkbtn\" type=\"button\" data-clear hidden>Clear filters</button></div>");
        html.AppendLine("</div></div>");

        html.AppendLine("<div class=\"wrap\">");
        html.AppendLine("<div class=\"empty\" id=\"no-results\" hidden><p class=\"empty-title\">No scenarios match these filters.</p><button class=\"ghost\" type=\"button\" data-clear>Clear filters</button></div>");
        html.AppendLine("<div class=\"stories\">");
        var index = 0;
        for (var storyIndex = 0; storyIndex < stories.Count; storyIndex++)
        {
            var story = stories[storyIndex];
            html.AppendLine($"<section class=\"story\" data-story=\"{storyIndex}\">");
            html.AppendLine("<div class=\"story-head\"><div class=\"story-main\"><div class=\"story-label\">Story</div>");
            html.AppendLine($"<p class=\"story-text\">{EscapeHtml(story[0].GetStoryText())}</p></div>");
            html.AppendLine("<div class=\"story-stats\">");
            foreach (var status in Statuses)
            {
                var count = story.Count(s => s.Status == status);
                if (count > 0)
                    html.AppendLine($"<span class=\"ss\"><span class=\"glyph\" data-g=\"{Key(status)}\" aria-hidden=\"true\"></span><b>{count}</b> {Label(Key(status)).ToLowerInvariant()}</span>");
            }
            html.AppendLine("</div></div>");
            html.AppendLine("<div class=\"scn-list\">");
            foreach (var scenario in story)
            {
                GenerateScenarioItem(html, scenario, storyIndex, index++);
            }
            html.AppendLine("</div>");
            html.AppendLine("</section>");
        }
        html.AppendLine("<div class=\"scn-list\" id=\"flat-list\" hidden></div>");
        html.AppendLine("</div>");
        html.AppendLine("<div class=\"more\" id=\"more\" hidden><span id=\"more-label\"></span><button class=\"ghost\" type=\"button\" data-more>Show more</button></div>");
        html.AppendLine("</div>");
        html.AppendLine("</section>");
    }

    private static void GenerateScenarioItem(StringBuilder html, Scenario scenario, int storyIndex, int index)
    {
        var status = Key(scenario.Status);
        var guid = EscapeHtml(scenario.Guid);
        var tags = scenario.Tags ?? Array.Empty<string>();
        var tagsJson = "[" + string.Join(",", tags.Select(t => "\"" + t.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"")) + "]";

        html.AppendLine($"<div class=\"scn\" id=\"scenario-{guid}\" data-status=\"{status}\" data-story=\"{storyIndex}\" data-i=\"{index}\" data-ms=\"{(long)scenario.TimeTaken.TotalMilliseconds}\" data-sev=\"{scenario.Status.GetOrder()}\" data-tags=\"{EscapeHtml(tagsJson)}\">");
        html.AppendLine($"<button class=\"scn-head\" type=\"button\" aria-expanded=\"false\" aria-controls=\"details-{guid}\">");
        html.AppendLine($"<span>{Pill(status)}</span>");
        html.AppendLine("<span class=\"scn-main\">");
        html.AppendLine($"<span class=\"scenario-title\">{EscapeHtml(scenario.GetScenarioText())}</span>");
        html.Append("<span class=\"scn-sub\">");
        html.Append($"<span class=\"scn-story\">{EscapeHtml(StorySummary(scenario.GetStoryText()))}</span>");
        if (tags.Length > 0)
            html.Append($"<span class=\"mono\">{EscapeHtml(string.Join("  ", tags.Select(t => "@" + t)))}</span>");
        if (scenario.IsOutline)
            html.Append($"<span>{scenario.Examples.Count} {(scenario.Examples.Count == 1 ? "example" : "examples")}</span>");
        html.AppendLine("</span>");
        html.AppendLine("</span>");
        html.AppendLine($"<span class=\"retry-slot\">{(scenario.RetryCount > 0 ? $"<span class=\"badge\">Retries: {scenario.RetryCount}</span>" : "")}</span>");
        html.AppendLine($"<span class=\"dur\">{FormatDuration(scenario.TimeTaken)}</span>");
        html.AppendLine("<svg class=\"chev\" width=\"20\" height=\"20\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"m6 9 6 6 6-6\"></path></svg>");
        html.AppendLine("</button>");

        html.AppendLine($"<div class=\"scn-body\" id=\"details-{guid}\" hidden>");
        html.AppendLine("<div class=\"body-main\">");
        var note = GetStatusNote(scenario.Status);
        if (note != null)
        {
            html.AppendLine($"<p class=\"note\" data-status=\"{status}\"><b>{Label(status)}.</b> {note}</p>");
        }
        GenerateScenarioDetails(html, scenario);
        html.AppendLine("</div>");

        html.AppendLine("<aside class=\"body-side\" aria-label=\"Scenario details\"><dl class=\"side-dl\">");
        html.AppendLine($"<div><dt>Duration</dt><dd class=\"mono\">{FormatDuration(scenario.TimeTaken)}</dd></div>");
        if (scenario.StartTime != default)
            html.AppendLine($"<div><dt>Started</dt><dd class=\"mono\">{FormatTime(scenario.StartTime, "yyyy-MM-dd HH:mm:ss.fff")} UTC</dd></div>");
        if (!string.IsNullOrEmpty(scenario.FileName))
            html.AppendLine($"<div><dt>File</dt><dd class=\"mono\" title=\"{EscapeHtml(scenario.FileName)}\">{EscapeHtml(GetFileName(scenario.FileName))}</dd></div>");
        if (tags.Length > 0)
        {
            html.Append("<div><dt>Tags</dt><dd><div class=\"tags\">");
            foreach (var tag in tags)
                html.Append($"<button class=\"tag\" type=\"button\" data-tag=\"{EscapeHtml(tag.ToLowerInvariant())}\" aria-label=\"Show only scenarios tagged {EscapeHtml(tag)}\">@{EscapeHtml(tag)}</button>");
            html.AppendLine("</div></dd></div>");
        }
        foreach (var information in scenario.CustomTestInformation ?? Array.Empty<TestInformationAttribute>())
        {
            if (information == null) continue;
            var name = information.GetType().Name;
            name = name == nameof(TestInformationAttribute) ? "Information"
                : name.EndsWith("Attribute", StringComparison.Ordinal) ? name.Substring(0, name.Length - "Attribute".Length) : name;
            html.AppendLine($"<div><dt>{EscapeHtml(name)}</dt><dd>{EscapeHtml(information.Information)}</dd></div>");
        }
        html.AppendLine("</dl></aside>");
        html.AppendLine("</div>");
        html.AppendLine("</div>");
    }

    private static void GenerateScenarioDetails(StringBuilder html, Scenario scenario)
    {
        if (scenario.IsOutline)
        {
            GenerateExamplesTable(html, scenario);
        }

        if (scenario.RetryCount > 0 && scenario.Attempts?.Count > 0)
        {
            html.AppendLine("<div><h3 class=\"h3\">Retry history</h3><div class=\"attempts\">");
            for (var index = 0; index < scenario.Attempts.Count; index++)
            {
                var attempt = scenario.Attempts[index];
                var key = Key(attempt.Status);
                var final = index == scenario.Attempts.Count - 1;
                var label = $"<span class=\"glyph\" data-g=\"{key}\" aria-hidden=\"true\"></span>Attempt {attempt.Number} · {Label(key)}{(final ? " (final)" : "")} <span class=\"mono\">{FormatDuration(attempt.TimeTaken)}</span>";
                if (attempt.Exception == null)
                {
                    html.AppendLine($"<span class=\"attempt\" data-final=\"{(final ? "true" : "false")}\">{label}</span>");
                    continue;
                }
                html.AppendLine($"<details class=\"attempt-d\"><summary class=\"attempt\" data-final=\"{(final ? "true" : "false")}\">{label}</summary>");
                AppendCode(html, "Exception", attempt.Exception.AsString, true);
                html.AppendLine("</details>");
            }
            html.AppendLine("</div></div>");
        }

        // Errors raised outside a step (setup, skip rules, retry hooks) have no step to show them.
        var exception = scenario.IsOutline ? null : scenario.Exception;
        if (exception != null && scenario.Steps?.Any(step => ReferenceEquals(step.Exception, exception)) != true)
        {
            html.AppendLine("<div><h3 class=\"h3\">Error</h3>");
            AppendError(html, exception, false);
            html.AppendLine("</div>");
        }

        if (scenario.Steps?.Any() == true && !scenario.IsOutline)
        {
            html.AppendLine("<div><h3 class=\"h3\">Steps</h3>");
            AppendSteps(html, scenario.Steps);
            html.AppendLine("</div>");
        }

        AppendOutput(html, "Startup output", scenario.TestStartupInformation);
        if (!scenario.IsOutline)
            AppendOutput(html, "Output", scenario.Output);
        AppendOutput(html, "Teardown output", scenario.TearDownOutput);

        if (!string.IsNullOrEmpty(scenario.CustomHtmlOutputForReport))
        {
            // Test HTML is untrusted: keep it in a scriptless, cross-origin frame with its own policy,
            // and attribute-encode srcdoc so quotes or </iframe> cannot escape into the report.
            var document = "<!doctype html><html><head><meta charset=\"utf-8\">"
                + "<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; "
                + "style-src 'unsafe-inline'; img-src data: https: http:; base-uri 'none'; form-action 'none'\">"
                + "<meta name=\"referrer\" content=\"no-referrer\"></head><body>"
                + scenario.CustomHtmlOutputForReport + "</body></html>";
            html.AppendLine("<div><h3 class=\"h3\">Custom output</h3><div class=\"custom\">");
            html.AppendLine("<div class=\"custom-bar\"><span>HTML written by the test through HtmlWriter</span><span class=\"sandbox\"><svg width=\"12\" height=\"12\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><rect x=\"5\" y=\"11\" width=\"14\" height=\"10\" rx=\"2\"></rect><path d=\"M8 11V8a4 4 0 0 1 8 0v3\"></path></svg>Sandboxed frame · scripts off</span></div>");
            html.AppendLine($"<iframe class=\"custom-body\" title=\"Custom test output\" sandbox=\"\" referrerpolicy=\"no-referrer\" srcdoc=\"{EscapeHtml(document)}\"></iframe>");
            html.AppendLine("</div></div>");
        }
    }

    private static void GenerateExamplesTable(StringBuilder html, Scenario scenario)
    {
        var columns = scenario.Examples.SelectMany(row => row.ExampleValues.Keys).Distinct().ToArray();
        var columnCount = columns.Length + 5;
        html.AppendLine("<div><h3 class=\"h3\">Examples</h3><div class=\"ex-box\">");
        html.Append($"<table class=\"examples\" id=\"examples-{EscapeHtml(scenario.Guid)}\"><thead><tr><th>#</th>");
        foreach (var column in columns) html.Append($"<th>{EscapeHtml(column)}</th>");
        html.AppendLine("<th>Status</th><th>Retries</th><th>Duration</th><th><span class=\"sr\">Details</span></th></tr></thead><tbody>");
        for (var index = 0; index < scenario.Examples.Count; index++)
        {
            var row = scenario.Examples[index];
            var detailsId = $"example-{EscapeHtml(row.Guid)}";
            html.Append($"<tr class=\"example-row\" data-status=\"{Key(row.Status)}\"><td class=\"mono\">{index + 1}</td>");
            foreach (var column in columns)
                html.Append($"<td>{EscapeHtml(row.ExampleValues.TryGetValue(column, out var value) ? value : "")}</td>");
            html.Append($"<td>{Pill(Key(row.Status))}</td><td class=\"mono\">{row.RetryCount}</td><td class=\"mono\" title=\"{row.TimeTaken}\">{FormatDuration(row.TimeTaken)}</td>");
            html.AppendLine($"<td><button class=\"mini ex-toggle\" type=\"button\" aria-expanded=\"false\" aria-controls=\"{detailsId}\">Steps and output</button></td></tr>");
            html.AppendLine($"<tr class=\"ex-detail\" id=\"{detailsId}\" hidden><td colspan=\"{columnCount}\"><div class=\"ex-detail-in\">");
            GenerateScenarioDetails(html, row);
            html.AppendLine("</div></td></tr>");
        }
        html.AppendLine("</tbody></table></div></div>");
    }

    private static void AppendSteps(StringBuilder html, IEnumerable<Step> steps)
    {
        html.AppendLine("<ol class=\"steps\">");
        foreach (var step in steps)
        {
            // Steps after a failure keep their default status but never started.
            var status = step.Status == Status.Inconclusive && step.StartTime == default && step.Exception == null
                ? "notrun" : Key(step.Status);
            var (keyword, text) = SplitStepText(step.StepText);
            html.AppendLine($"<li class=\"step\" data-status=\"{status}\">");
            html.AppendLine($"<div class=\"step-row\"><span class=\"glyph\" data-g=\"{status}\" aria-hidden=\"true\"></span><span class=\"kw\">{EscapeHtml(keyword)}</span><span class=\"step-text\">{EscapeHtml(text)}</span><span class=\"dur\">{(status == "notrun" ? "" : FormatDuration(step.TimeTaken))}</span></div>");
            if (status == "notrun")
                html.AppendLine("<p class=\"step-note\">Not run: an earlier step did not pass.</p>");
            if (step.Exception != null)
                AppendError(html, step.Exception, step.Status == Status.NotImplemented);
            html.AppendLine("</li>");
        }
        html.AppendLine("</ol>");
    }

    private static void AppendError(StringBuilder html, ExceptionWrapper exception, bool notImplemented)
    {
        if (notImplemented)
        {
            html.AppendLine($"<p class=\"step-note\">{EscapeHtml(exception.TypeName)}: {EscapeHtml(exception.Message)}</p>");
            return;
        }
        html.AppendLine("<div class=\"err\">");
        html.AppendLine($"<span class=\"err-type\">{EscapeHtml(exception.TypeName)}</span>");
        html.AppendLine($"<p class=\"err-msg\">{EscapeHtml(exception.Message)}</p>");
        if (!string.IsNullOrEmpty(exception.StackTrace))
            AppendCode(html, "Stack trace", exception.StackTrace, true);
        html.AppendLine("</div>");
    }

    private static void AppendOutput(StringBuilder html, string title, string output)
    {
        if (string.IsNullOrEmpty(output)) return;
        html.AppendLine($"<div><h3 class=\"h3\">{title}</h3>");
        var lines = output.TrimEnd().Split('\n').Length;
        AppendCode(html, $"{lines} {(lines == 1 ? "line" : "lines")}", output, true);
        html.AppendLine("</div>");
    }

    private static void AppendCode(StringBuilder html, string title, string content, bool actions)
    {
        html.Append($"<div class=\"code\"><div class=\"code-bar\"><span>{EscapeHtml(title)}</span>");
        if (actions)
            html.Append("<span class=\"code-actions\"><button class=\"mini\" type=\"button\" data-wrap aria-pressed=\"false\">Wrap</button><button class=\"mini\" type=\"button\" data-copy>Copy</button></span>");
        html.AppendLine($"</div><pre class=\"pre\">{EscapeHtml(content)}</pre></div>");
    }

    private static void GenerateNotRunSection(StringBuilder html, List<BuildableTest> notRun)
    {
        html.AppendLine("<section class=\"wrap sec\" id=\"not-run\" aria-labelledby=\"nr-h\">");
        html.AppendLine("<div class=\"sec-head\"><h2 id=\"nr-h\" class=\"h2\">Tests not run</h2><p class=\"sec-note\">Built by the test framework but never executed.</p></div>");
        html.AppendLine("<div class=\"nr\">");

        foreach (var test in notRun)
        {
            var where = string.IsNullOrEmpty(test.CallerFile) ? test.CallerMember
                : string.IsNullOrEmpty(test.CallerMember) ? GetFileName(test.CallerFile)
                : $"{GetFileName(test.CallerFile)} › {test.CallerMember}";
            html.AppendLine("<div class=\"nr-row\">");
            html.AppendLine($"<span class=\"nr-title\"><span class=\"glyph\" data-g=\"notrun\" aria-hidden=\"true\"></span>{EscapeHtml(test.GetScenarioText())}</span>");
            html.AppendLine($"<span class=\"story-mini\">{EscapeHtml(StorySummary(test.GetStoryText()))}</span>");
            html.AppendLine($"<span class=\"mono\">{EscapeHtml(where)}</span>");
            html.AppendLine("</div>");
        }

        html.AppendLine("</div>");
        html.AppendLine("</section>");
    }

    private static string Key(Status status) => status.ToString().ToLowerInvariant();

    private static string Label(string key) => key switch
    {
        "passed" => "Passed",
        "failed" => "Failed",
        "skipped" => "Skipped",
        "inconclusive" => "Inconclusive",
        "notimplemented" => "Not implemented",
        "notrun" => "Not run",
        _ => key
    };

    private static string Pill(string key) =>
        $"<span class=\"pill\" data-status=\"{key}\"><span class=\"glyph\" data-g=\"{key}\" aria-hidden=\"true\"></span>{Label(key)}</span>";

    private static string GetStatusNote(Status status) => status switch
    {
        Status.Skipped => "This scenario did not run its steps.",
        Status.Inconclusive => "The test could not confirm the outcome either way.",
        Status.NotImplemented => "A step in this scenario has no implementation yet.",
        _ => null
    };

    private static (string Keyword, string Text) SplitStepText(string stepText)
    {
        if (string.IsNullOrEmpty(stepText)) return ("", "");
        var space = stepText.IndexOf(' ');
        var first = space < 0 ? stepText : stepText.Substring(0, space);
        return first is "Given" or "When" or "Then" or "And" or "But"
            ? (first, space < 0 ? "" : stepText.Substring(space + 1))
            : ("", stepText);
    }

    private static string StorySummary(string story)
    {
        var lines = story.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).Take(2);
        return string.Join(" · ", lines);
    }

    // Turns "at Ns.Type.Method(args) in /src/File.cs:line 88" into "Type.Method · File.cs:88".
    private static string GetFirstFrame(string stackTrace)
    {
        var line = stackTrace?.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
        if (line == null) return null;
        if (line.StartsWith("at ", StringComparison.Ordinal)) line = line.Substring(3);

        var inIndex = line.IndexOf(" in ", StringComparison.Ordinal);
        var method = inIndex < 0 ? line : line.Substring(0, inIndex);
        var paren = method.IndexOf('(');
        if (paren > 0) method = method.Substring(0, paren);
        var parts = method.Split('.');
        if (parts.Length > 2) method = string.Join(".", parts.Skip(parts.Length - 2));
        if (inIndex < 0) return method;

        var location = line.Substring(inIndex + 4);
        var lineIndex = location.LastIndexOf(":line ", StringComparison.Ordinal);
        var file = GetFileName(lineIndex < 0 ? location : location.Substring(0, lineIndex));
        return lineIndex < 0 ? $"{method} · {file}" : $"{method} · {file}:{location.Substring(lineIndex + 6)}";
    }

    // Reports can be generated on a different OS from the one that compiled the tests.
    private static string GetFileName(string path) => path?.Split('/', '\\').Last();

    private static string FormatTime(DateTime time, string format) => time.ToString(format, CultureInfo.InvariantCulture);

    internal static string FormatDuration(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero) return "—";
        if (duration.TotalMilliseconds < 1000) return $"{(long)Math.Round(duration.TotalMilliseconds)} ms";
        if (duration.TotalSeconds < 60) return duration.TotalSeconds.ToString("0.##", CultureInfo.InvariantCulture) + " s";
        if (duration.TotalMinutes < 60) return $"{(int)duration.TotalMinutes} m {duration.Seconds:00} s";
        if (duration.TotalHours < 24) return $"{(int)duration.TotalHours} h {duration.Minutes:00} m";
        return $"{(int)duration.TotalDays} d {duration.Hours} h";
    }

    private static string EscapeHtml(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        return input.Replace("&", "&amp;")
                   .Replace("<", "&lt;")
                   .Replace(">", "&gt;")
                   .Replace("\"", "&quot;")
                   .Replace("'", "&#39;");
    }

    private const string EmbeddedStyles = """
<style>
body{margin:0}
.rpt{--sans:"Segoe UI Variable Text","Segoe UI",-apple-system,BlinkMacSystemFont,"Helvetica Neue",system-ui,sans-serif;--mono:ui-monospace,"Cascadia Mono","SF Mono",Menlo,Consolas,"Liberation Mono",monospace;
--bg:#F3F4F1;--surface:#FFFFFF;--surface-2:#F7F8F6;--sunken:#ECEEEA;--ink:#15171A;--ink-2:#3D4249;--muted:#5A6069;--rule:#DCDFD9;--rule-2:#C4C8C1;--accent:#1F4FD1;--code-bg:#F0F1EE;--code-ink:#22262B;
--passed:#1B7A42;--passed-bg:#E2F1E7;--failed:#BF2519;--failed-bg:#FBE6E3;--skipped:#965400;--skipped-bg:#FAEDD8;--inconclusive:#545E6A;--inconclusive-bg:#E7E9EC;--notimplemented:#6A3EC2;--notimplemented-bg:#EEE7FB;--notrun:#7A8089;--notrun-bg:#EEEFEC;
font-family:var(--sans);color:var(--ink);background:var(--bg);min-height:100vh;font-size:15px;line-height:1.5;-webkit-font-smoothing:antialiased}
@media (prefers-color-scheme:dark){.rpt{--bg:#0F1113;--surface:#171A1D;--surface-2:#1C2024;--sunken:#22272C;--ink:#ECEEF0;--ink-2:#C2C7CE;--muted:#99A0AA;--rule:#2A2F35;--rule-2:#3A4047;--accent:#8EAEFF;--code-bg:#0B0D0F;--code-ink:#D9DDE2;
--passed:#5CCF8C;--passed-bg:#13291D;--failed:#FF7F72;--failed-bg:#35160F;--skipped:#F0B060;--skipped-bg:#2F2310;--inconclusive:#AEB6C1;--inconclusive-bg:#252A30;--notimplemented:#BBA0FF;--notimplemented-bg:#261D3B;--notrun:#8C939C;--notrun-bg:#1E2226}}
.rpt *{box-sizing:border-box}
.rpt [hidden]{display:none!important}
.rpt button,.rpt input,.rpt select{font:inherit;color:inherit}
.rpt :focus-visible{outline:2px solid var(--accent);outline-offset:2px}
.rpt a{color:var(--accent)}.rpt a:hover{color:var(--ink)}
.rpt code{font-family:var(--mono);font-size:.9em}
.wrap{max-width:1200px;margin:0 auto;padding:0 32px}
.sr{position:absolute;width:1px;height:1px;overflow:hidden;clip:rect(0 0 0 0);white-space:nowrap}
.mono{font-family:var(--mono)}
[data-status=passed]{--s:var(--passed);--s-bg:var(--passed-bg)}
[data-status=failed]{--s:var(--failed);--s-bg:var(--failed-bg)}
[data-status=skipped]{--s:var(--skipped);--s-bg:var(--skipped-bg)}
[data-status=inconclusive]{--s:var(--inconclusive);--s-bg:var(--inconclusive-bg)}
[data-status=notimplemented]{--s:var(--notimplemented);--s-bg:var(--notimplemented-bg)}
[data-status=notrun]{--s:var(--notrun);--s-bg:var(--notrun-bg)}
[data-status=all]{--s:var(--ink);--s-bg:var(--sunken)}
.glyph{display:inline-block;flex:none;width:9px;height:9px;border-radius:50%;background:var(--g)}
.glyph[data-g=passed]{--g:var(--passed)}
.glyph[data-g=failed]{--g:var(--failed);border-radius:1px;transform:rotate(45deg) scale(.88)}
.glyph[data-g=skipped]{--g:var(--skipped);background:none;border:2px solid var(--g)}
.glyph[data-g=inconclusive]{--g:var(--inconclusive);border:2px solid var(--g);background:linear-gradient(90deg,var(--g) 50%,transparent 50%)}
.glyph[data-g=notimplemented]{--g:var(--notimplemented);background:none;border:2px solid var(--g);border-radius:2px}
.glyph[data-g=notrun]{--g:var(--notrun);height:2px;border-radius:1px}
.pill{display:inline-flex;align-items:center;gap:7px;padding:3px 10px 3px 9px;border-radius:999px;background:var(--s-bg);color:var(--s);font-size:12.5px;font-weight:650;white-space:nowrap;line-height:1.3}
.top{border-bottom:1px solid var(--rule);background:var(--surface)}
.top-row{display:flex;justify-content:space-between;align-items:center;gap:12px;min-height:56px;flex-wrap:wrap}
.brand{display:flex;align-items:center;gap:10px;font-weight:700;font-size:16px;letter-spacing:-.01em}
.brand-sub{font-weight:500;color:var(--muted)}
.gen{font-size:13px;color:var(--muted)}
.eyebrow{font-size:12px;letter-spacing:.08em;text-transform:uppercase;color:var(--muted);font-weight:650;margin:0}
.verdict{display:flex;flex-wrap:wrap;gap:28px 64px;align-items:flex-end;padding-top:44px;padding-bottom:28px}
.verdict-h{margin:6px 0;font-size:52px;line-height:1.04;letter-spacing:-.025em;font-weight:750}
.verdict-h .of{color:var(--ink-2);font-weight:500}
.v-word{color:var(--s)}
.verdict-sub{margin:0;color:var(--ink-2);font-size:15px}
.meta{flex:1 1 520px;display:grid;grid-template-columns:repeat(auto-fit,minmax(140px,1fr));gap:14px 28px;margin:0}
.meta dt{font-size:11.5px;letter-spacing:.07em;text-transform:uppercase;color:var(--muted);font-weight:650}
.meta dd{margin:2px 0 0;font-size:14.5px;overflow-wrap:anywhere}
.unset{color:var(--muted);font-style:italic}
.bar{display:flex;height:12px;border-radius:999px;overflow:hidden;gap:2px;background:var(--rule)}
.bar-seg{background:var(--s);min-width:6px}
.tiles{display:grid;grid-template-columns:repeat(auto-fit,minmax(min(150px,100%),1fr));gap:10px;margin-top:16px}
.tile{appearance:none;text-align:left;cursor:pointer;background:var(--surface);border:1px solid var(--rule);border-radius:10px;padding:12px 16px 14px;display:flex;flex-direction:column;gap:2px;min-height:44px}
.tile:hover{border-color:var(--rule-2);background:var(--surface-2)}
.tile[aria-pressed=true]{border-color:var(--s);box-shadow:inset 0 0 0 1px var(--s)}
.tile:disabled{opacity:.5;cursor:default}
.tile-top{display:flex;align-items:center;gap:8px;font-size:13px;color:var(--ink-2);font-weight:600}
.tile-n{font-family:var(--mono);font-size:30px;font-weight:600;color:var(--s);letter-spacing:-.02em;line-height:1.2}
.tile-pct{font-family:var(--mono);font-size:12px;color:var(--muted)}
.runprops{margin-top:14px;font-size:13.5px;color:var(--ink-2)}
.runprops summary{cursor:pointer;display:inline-flex;align-items:center;min-height:32px;font-weight:600}
.runprops dl{display:grid;grid-template-columns:max-content 1fr;gap:4px 20px;margin:8px 0 0}
.runprops dt{color:var(--muted)}.runprops dd{margin:0;font-family:var(--mono);font-size:13px;overflow-wrap:anywhere}
.sec{padding-top:40px}
.sec-head{display:flex;flex-wrap:wrap;justify-content:space-between;align-items:baseline;gap:6px 20px;margin-bottom:14px}
.h2{margin:0;font-size:21px;letter-spacing:-.01em;font-weight:720}
.sec-note{margin:0;color:var(--ink-2);font-size:14px}
.att-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(min(380px,100%),1fr));gap:12px}
.cause{background:var(--surface);border:1px solid color-mix(in oklab,var(--failed) 32%,var(--rule));border-radius:12px;padding:18px 20px;display:flex;flex-direction:column;gap:6px;min-width:0}
.cause-top{display:flex;justify-content:space-between;align-items:center;gap:12px}
.cause-n{font-family:var(--mono);font-size:12.5px;color:var(--failed);font-weight:600;white-space:nowrap}
.err-type{font-family:var(--mono);font-size:12.5px;font-weight:600;color:var(--failed);overflow-wrap:anywhere}
.cause-msg{margin:0;font-size:15px;font-weight:600;overflow-wrap:anywhere}
.cause-where{font-family:var(--mono);font-size:12px;color:var(--muted);overflow-wrap:anywhere}
.cause ul{list-style:none;margin:6px 0 0;padding:10px 0 0;border-top:1px solid var(--rule);display:flex;flex-direction:column;gap:2px}
.linkbtn{appearance:none;background:none;border:0;padding:4px 0;cursor:pointer;color:var(--accent);text-align:left;font-size:14px;text-decoration:underline;text-underline-offset:3px;text-decoration-thickness:1px;min-height:32px;overflow-wrap:anywhere}
.linkbtn:hover{color:var(--ink)}
.also{display:flex;flex-wrap:wrap;align-items:center;gap:6px 18px;margin-top:12px;font-size:14px;color:var(--ink-2)}
.also-item{display:inline-flex;align-items:center;gap:8px}
.toolbar{position:sticky;top:0;z-index:5;background:color-mix(in oklab,var(--bg) 90%,transparent);backdrop-filter:blur(8px);-webkit-backdrop-filter:blur(8px);border-bottom:1px solid var(--rule);margin-top:14px}
.tb-row{display:flex;flex-wrap:wrap;align-items:center;gap:10px 12px;padding:12px 0}
.tb-actions{display:flex;gap:8px;margin-left:auto}
.search{position:relative;flex:1 1 240px;max-width:340px}
.search svg{position:absolute;left:11px;top:50%;transform:translateY(-50%);color:var(--muted)}
.search input{width:100%;height:40px;border:1px solid var(--rule-2);border-radius:8px;background:var(--surface);padding:0 12px 0 34px}
.search input::placeholder{color:var(--muted)}
.seg{display:flex;flex-wrap:wrap;border:1px solid var(--rule-2);border-radius:8px;overflow:hidden;background:var(--surface)}
.seg-btn{appearance:none;border:0;border-right:1px solid var(--rule);background:none;height:38px;padding:0 12px;display:inline-flex;align-items:center;gap:7px;cursor:pointer;font-size:13.5px;font-weight:550;color:var(--ink-2)}
.seg-btn:last-child{border-right:0}
.seg-btn:hover{background:var(--surface-2)}
.seg-btn[aria-pressed=true]{background:var(--ink);color:var(--bg)}
.seg-btn .n{font-family:var(--mono);font-size:12px;opacity:.75}
.ctl{display:inline-flex;align-items:center;gap:8px;font-size:13.5px;color:var(--ink-2)}
.ctl select{height:40px;border:1px solid var(--rule-2);border-radius:8px;background:var(--surface);padding:0 10px}
.ctl input[type=checkbox]{width:18px;height:18px;accent-color:var(--accent);margin:0}
.ghost{appearance:none;height:40px;padding:0 14px;border:1px solid var(--rule-2);border-radius:8px;background:var(--surface);cursor:pointer;font-size:13.5px;font-weight:550;color:var(--ink-2)}
.ghost:hover{background:var(--surface-2);color:var(--ink)}
.tb-sub{display:flex;flex-wrap:wrap;justify-content:space-between;align-items:center;gap:8px;padding:0 0 10px;font-size:13.5px;color:var(--muted)}
.stories{display:flex;flex-direction:column;gap:36px;padding-top:22px}
.story{display:flex;flex-direction:column;gap:10px}
.story-head{display:flex;flex-wrap:wrap;justify-content:space-between;align-items:flex-end;gap:10px 28px;padding:0 2px 2px}
.story-main{min-width:0;flex:1 1 420px}
.story-label{font-family:var(--mono);font-size:11.5px;text-transform:uppercase;letter-spacing:.08em;color:var(--muted);margin-bottom:4px}
.story-text{white-space:pre-line;margin:0;font-size:15px;color:var(--ink-2);overflow-wrap:anywhere;line-height:1.45}
.story-text::first-line{font-weight:680;color:var(--ink)}
.story-stats{display:flex;flex-wrap:wrap;gap:4px 14px;font-size:13px;color:var(--ink-2)}
.ss{display:inline-flex;align-items:center;gap:7px}
.ss b{font-family:var(--mono);font-weight:600}
.scn-list{display:flex;flex-direction:column;gap:8px}
#flat-list{padding-top:0}
.scn{background:var(--surface);border:1px solid var(--rule);border-radius:10px;overflow:hidden;scroll-margin-top:130px}
.scn[data-status=failed]{border-color:color-mix(in oklab,var(--failed) 34%,var(--rule))}
.scn-head{appearance:none;background:none;border:0;width:100%;text-align:left;cursor:pointer;display:grid;grid-template-columns:136px minmax(0,1fr) auto auto 20px;align-items:center;gap:16px;padding:12px 18px;min-height:58px}
.scn-head:hover{background:var(--surface-2)}
.scn-head:focus-visible{outline-offset:-2px}
.scn-main{display:flex;flex-direction:column;gap:2px;min-width:0}
.scenario-title{font-weight:640;font-size:15px;overflow-wrap:anywhere}
.scn-sub{font-size:12.5px;color:var(--muted);display:flex;flex-wrap:wrap;gap:2px 12px;overflow-wrap:anywhere}
.scn-sub:empty{display:none}
.rpt[data-grouped=true] .scn-story{display:none}
.badge{font-family:var(--mono);font-size:12px;padding:3px 8px;border-radius:6px;background:var(--sunken);color:var(--ink-2);white-space:nowrap}
.dur{font-family:var(--mono);font-size:13px;color:var(--ink-2);white-space:nowrap;text-align:right;min-width:64px}
.chev{color:var(--muted);transition:transform .15s}
.scn-head[aria-expanded=true] .chev{transform:rotate(180deg)}
.scn-body{border-top:1px solid var(--rule);padding:20px 18px 22px;display:flex;flex-wrap:wrap;gap:24px 36px}
.body-main{flex:999 1 560px;min-width:0;display:flex;flex-direction:column;gap:24px}
.body-side{flex:1 1 220px;min-width:0}
.h3{font-size:11.5px;letter-spacing:.08em;text-transform:uppercase;color:var(--muted);font-weight:680;margin:0 0 8px}
.note{margin:0;padding:12px 14px;border-radius:8px;background:var(--s-bg);color:var(--ink);font-size:14px;overflow-wrap:anywhere}
.note b{color:var(--s)}
.steps{list-style:none;margin:0;padding:0}
.step+.step{border-top:1px solid var(--rule)}
.step-row{display:grid;grid-template-columns:9px 54px minmax(0,1fr) auto;gap:12px;align-items:start;padding:9px 0}
.step-row .glyph{margin-top:7px}
.kw{font-family:var(--mono);font-size:11.5px;text-transform:uppercase;letter-spacing:.05em;color:var(--muted);padding-top:2px}
.step-text{overflow-wrap:anywhere}
.step[data-status=failed] .step-text{font-weight:640}
.step[data-status=notrun] .step-text{color:var(--muted)}
.step .dur{font-size:12.5px;color:var(--muted)}
.step-note{margin:0 0 10px 75px;font-size:13.5px;color:var(--ink-2);overflow-wrap:anywhere}
.err{margin:0 0 12px 75px;padding:14px 16px;border-radius:8px;background:var(--failed-bg);display:flex;flex-direction:column;gap:8px;min-width:0}
.body-main>div>.err,.ex-detail-in>div>.err{margin-left:0}
.err-msg{margin:0;font-size:14.5px;overflow-wrap:anywhere;white-space:pre-wrap}
.code{border:1px solid var(--rule);border-radius:8px;background:var(--code-bg);overflow:hidden;min-width:0}
.code-bar{display:flex;justify-content:space-between;align-items:center;gap:12px;padding:5px 6px 5px 12px;border-bottom:1px solid var(--rule);font-size:12px;color:var(--muted);background:var(--surface);min-height:41px}
.code-actions{display:flex;gap:6px}
.mini{appearance:none;font-size:12px;padding:0 10px;height:30px;border:1px solid var(--rule-2);background:var(--surface);color:var(--ink-2);border-radius:6px;cursor:pointer;white-space:nowrap}
.mini:hover{color:var(--ink);background:var(--surface-2)}
.mini[aria-pressed=true]{background:var(--ink);color:var(--bg);border-color:var(--ink)}
.pre{margin:0;padding:12px 14px;font-family:var(--mono);font-size:12.5px;line-height:1.65;color:var(--code-ink);max-height:280px;overflow:auto;white-space:pre;tab-size:4}
.rpt[data-wrap=true] .pre{white-space:pre-wrap;overflow-wrap:anywhere}
.attempts{display:flex;flex-wrap:wrap;gap:8px}
.attempt{display:inline-flex;align-items:center;gap:8px;padding:6px 10px;border:1px solid var(--rule);border-radius:8px;font-size:13px;background:var(--surface-2)}
.attempt .mono{font-size:12.5px;color:var(--muted)}
.attempt[data-final=true]{border-color:var(--rule-2);background:var(--surface)}
.attempt-d{display:contents}
.attempt-d summary{cursor:pointer;list-style:none}
.attempt-d summary::-webkit-details-marker{display:none}
.attempt-d summary::after{content:"";width:6px;height:6px;border-right:1.5px solid var(--muted);border-bottom:1.5px solid var(--muted);transform:rotate(45deg);margin:-3px 2px 0 2px}
.attempt-d[open] summary::after{transform:rotate(-135deg);margin-top:3px}
.attempt-d[open]>.code{flex-basis:100%}
.custom{border:1px solid var(--rule-2);border-radius:10px;overflow:hidden}
.custom-bar{display:flex;justify-content:space-between;align-items:center;gap:12px;flex-wrap:wrap;padding:8px 12px;background:var(--sunken);font-size:12.5px;color:var(--ink-2)}
.sandbox{display:inline-flex;align-items:center;gap:6px;font-family:var(--mono);font-size:11.5px;color:var(--muted)}
.custom-body{display:block;width:100%;height:20rem;border:0;background:#FFFFFF}
.side-dl{margin:0;display:flex;flex-direction:column;gap:12px}
.side-dl dt{font-size:11.5px;letter-spacing:.07em;text-transform:uppercase;color:var(--muted);font-weight:650}
.side-dl dd{margin:2px 0 0;font-size:13.5px;overflow-wrap:anywhere}
.tags{display:flex;flex-wrap:wrap;gap:6px;margin-top:4px}
.tag{appearance:none;cursor:pointer;font-family:var(--mono);font-size:12px;padding:0 9px;height:28px;border-radius:999px;border:1px solid var(--rule-2);background:var(--surface);color:var(--ink-2)}
.tag:hover{border-color:var(--accent);color:var(--accent)}
.ex-box{border:1px solid var(--rule);border-radius:8px;overflow-x:auto}
.examples{width:100%;min-width:640px;border-collapse:collapse;font-size:13.5px}
.examples th,.examples td{padding:8px 12px;text-align:left;vertical-align:middle;border-top:1px solid var(--rule)}
.examples thead th{border-top:0;background:var(--surface-2);font-size:12px;color:var(--muted);font-weight:650}
.examples td.mono{font-size:12.5px}
.ex-detail>td{padding:4px 12px 14px;border-top:1px dashed var(--rule-2);background:var(--surface-2)}
.ex-detail-in{display:flex;flex-direction:column;gap:16px;padding-top:8px}
.empty{padding:40px 20px;text-align:center;border:1px dashed var(--rule-2);border-radius:12px;color:var(--ink-2)}
.empty p{margin:0}
.empty .empty-title{margin:0 0 8px;font-weight:650;color:var(--ink)}
#no-results{margin-top:22px}
.more{display:flex;justify-content:space-between;align-items:center;gap:12px;flex-wrap:wrap;margin-top:16px;font-size:13.5px;color:var(--ink-2)}
.nr{background:var(--surface);border:1px solid var(--rule);border-radius:10px}
.nr-row{display:grid;grid-template-columns:minmax(0,1.2fr) minmax(0,1fr) minmax(0,1fr);gap:16px;padding:12px 18px;font-size:14px;align-items:baseline}
.nr-row+.nr-row{border-top:1px solid var(--rule)}
.nr-title{display:flex;gap:10px;align-items:center;font-weight:600;overflow-wrap:anywhere}
.nr-row .story-mini{color:var(--muted);font-size:13px;overflow-wrap:anywhere}
.nr-row .mono{font-size:12.5px;color:var(--ink-2);overflow-wrap:anywhere}
.foot{margin-top:56px;border-top:1px solid var(--rule);padding:20px 32px 40px;font-size:13px;color:var(--muted);display:flex;flex-wrap:wrap;justify-content:space-between;gap:8px}
@media (max-width:760px){
.wrap{padding:0 16px}
.foot{padding:20px 16px 40px}
.verdict{padding-top:28px;gap:22px}
.verdict-h{font-size:38px}
.search{max-width:none;flex-basis:100%}
.tb-actions{margin-left:0}
.scn-head{grid-template-columns:auto 1fr auto 20px;gap:8px 12px;padding:12px 14px}
.scn-head .scn-main{grid-column:1/-1;order:5}
.scn-head .retry-slot{justify-self:start}
.scn-body{padding:16px 14px}
.err,.step-note{margin-left:0}
.step-row{grid-template-columns:9px 46px minmax(0,1fr) auto;gap:10px}
.nr-row{grid-template-columns:1fr;gap:2px}
}
@media print{
.toolbar,.mini,.ghost,.chev,.more{display:none!important}
.rpt{background:#FFFFFF}
.scn,.cause,.tile{break-inside:avoid}
.pre{max-height:none;white-space:pre-wrap}
}
</style>
""";

    private const string EmbeddedScript = """
<script>
(function () {
  var root = document.querySelector('.rpt');
  var PAGE = 200;
  var state = { filter: 'all', query: '', tag: '', sort: 'story', group: true, limit: PAGE };
  var $ = function (sel) { return root.querySelector(sel); };
  var $$ = function (sel) { return Array.prototype.slice.call(root.querySelectorAll(sel)); };
  var search = $('#q'), tagSelect = $('#tag'), sortSelect = $('#sort'), groupBox = $('#group');
  var flat = $('#flat-list');
  var stories = $$('.story');
  var storyText = {};
  stories.forEach(function (s) { storyText[s.getAttribute('data-story')] = s.querySelector('.story-text').textContent; });

  var items = $$('.scn').map(function (el) {
    var tags = JSON.parse(el.getAttribute('data-tags') || '[]');
    var steps = Array.prototype.map.call(el.querySelectorAll('.step-text'), function (x) { return x.textContent; });
    return {
      el: el, home: el.parentNode, status: el.getAttribute('data-status'), story: +el.getAttribute('data-story'),
      i: +el.getAttribute('data-i'), ms: +el.getAttribute('data-ms'), sev: +el.getAttribute('data-sev'),
      tags: tags.map(function (t) { return t.toLowerCase(); }),
      hay: [el.querySelector('.scenario-title').textContent, storyText[el.getAttribute('data-story')]].concat(tags, steps).join('\n').toLowerCase()
    };
  });

  function compare(a, b) {
    if (state.group && a.story !== b.story) return a.story - b.story;
    if (state.sort === 'duration') return b.ms - a.ms || a.i - b.i;
    if (state.sort === 'status') return a.sev - b.sev || a.i - b.i;
    return a.i - b.i;
  }

  function apply() {
    var q = state.query.trim().toLowerCase();
    var base = items.filter(function (it) { return (!q || it.hay.indexOf(q) >= 0) && (!state.tag || it.tags.indexOf(state.tag) >= 0); });
    var visible = base.filter(function (it) { return state.filter === 'all' || it.status === state.filter; });
    var ordered = items.slice().sort(compare);
    var shown = 0;
    ordered.forEach(function (it) {
      (state.group ? it.home : flat).appendChild(it.el);
      var match = visible.indexOf(it) >= 0;
      it.el.hidden = !match || shown >= state.limit;
      if (match) shown++;
    });
    stories.forEach(function (s) { s.hidden = !state.group || !s.querySelector('.scn:not([hidden])'); });
    flat.hidden = state.group;
    root.setAttribute('data-grouped', state.group ? 'true' : 'false');

    $$('[data-filter]').forEach(function (b) {
      var k = b.getAttribute('data-filter');
      b.setAttribute('aria-pressed', k === state.filter ? 'true' : 'false');
      var n = b.querySelector('.n');
      if (n) n.textContent = base.filter(function (it) { return k === 'all' || it.status === k; }).length;
    });

    var filtered = state.filter !== 'all' || !!q || !!state.tag;
    $('#showing').textContent = filtered
      ? 'Showing ' + visible.length + ' of ' + items.length + ' scenarios'
      : items.length + ' scenarios across ' + stories.length + (stories.length === 1 ? ' story' : ' stories');
    $$('[data-clear]').forEach(function (b) { if (!b.closest('#no-results')) b.hidden = !filtered; });
    $('#no-results').hidden = visible.length > 0;
    var more = $('#more');
    more.hidden = visible.length <= state.limit;
    $('#more-label').textContent = 'Showing 1–' + Math.min(state.limit, visible.length) + ' of ' + visible.length + ' scenarios';
    more.querySelector('button').textContent = 'Show next ' + Math.min(PAGE, Math.max(visible.length - state.limit, 0));
  }

  function set(changes) {
    for (var k in changes) state[k] = changes[k];
    if (!('limit' in changes)) state.limit = PAGE;
    if (search) search.value = state.query;
    if (tagSelect) tagSelect.value = state.tag;
    apply();
  }

  function expand(el, open) {
    var head = el.querySelector('.scn-head');
    head.setAttribute('aria-expanded', open ? 'true' : 'false');
    el.querySelector('.scn-body').hidden = !open;
  }

  function copy(text, button) {
    var done = function () { button.textContent = 'Copied'; setTimeout(function () { button.textContent = 'Copy'; }, 1500); };
    if (navigator.clipboard && window.isSecureContext) { navigator.clipboard.writeText(text).then(done, function () {}); return; }
    var area = document.createElement('textarea');
    area.value = text; area.setAttribute('readonly', ''); area.style.position = 'fixed'; area.style.opacity = '0';
    document.body.appendChild(area); area.select();
    try { if (document.execCommand('copy')) done(); } catch (e) {}
    document.body.removeChild(area);
  }

  root.addEventListener('click', function (e) {
    var t = e.target.closest('button');
    if (!t || !root.contains(t)) return;
    if (t.classList.contains('scn-head')) { expand(t.parentNode, t.getAttribute('aria-expanded') !== 'true'); return; }
    if (t.classList.contains('ex-toggle')) {
      var open = t.getAttribute('aria-expanded') !== 'true';
      t.setAttribute('aria-expanded', open ? 'true' : 'false');
      t.textContent = open ? 'Hide steps' : 'Steps and output';
      document.getElementById(t.getAttribute('aria-controls')).hidden = !open;
      return;
    }
    if (t.hasAttribute('data-filter')) {
      var k = t.getAttribute('data-filter');
      set({ filter: t.classList.contains('tile') && state.filter === k && k !== 'all' ? 'all' : k });
      return;
    }
    if (t.hasAttribute('data-show')) { set({ filter: t.getAttribute('data-show'), query: '', tag: '' }); $('#scn-h').scrollIntoView({ behavior: 'smooth' }); return; }
    if (t.hasAttribute('data-tag')) { set({ tag: t.getAttribute('data-tag') }); return; }
    if (t.hasAttribute('data-clear')) { set({ filter: 'all', query: '', tag: '' }); return; }
    if (t.hasAttribute('data-more')) { set({ limit: state.limit + PAGE }); return; }
    if (t.hasAttribute('data-expand')) {
      var on = t.getAttribute('data-expand') === 'true';
      items.forEach(function (it) { if (!it.el.hidden || !on) expand(it.el, on); });
      return;
    }
    if (t.hasAttribute('data-go')) {
      set({ filter: 'all', query: '', tag: '', limit: items.length });
      var target = document.getElementById('scenario-' + t.getAttribute('data-go'));
      if (target) { expand(target, true); target.scrollIntoView({ behavior: 'smooth', block: 'start' }); }
      return;
    }
    if (t.hasAttribute('data-wrap')) {
      var wrap = root.getAttribute('data-wrap') !== 'true';
      root.setAttribute('data-wrap', wrap ? 'true' : 'false');
      $$('[data-wrap]').forEach(function (b) { b.setAttribute('aria-pressed', wrap ? 'true' : 'false'); });
      return;
    }
    if (t.hasAttribute('data-copy')) { copy(t.closest('.code').querySelector('.pre').textContent, t); }
  });

  if (search) search.addEventListener('input', function () { set({ query: search.value }); });
  if (tagSelect) tagSelect.addEventListener('change', function () { set({ tag: tagSelect.value }); });
  if (sortSelect) sortSelect.addEventListener('change', function () { set({ sort: sortSelect.value }); });
  if (groupBox) groupBox.addEventListener('change', function () { set({ group: groupBox.checked }); });
  if (items.length) apply();
})();
</script>
""";
}
