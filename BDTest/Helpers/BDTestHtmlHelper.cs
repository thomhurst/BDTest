using BDTest.Maps;
using BDTest.Output;
using BDTest.Test;
using System.Text;

namespace BDTest.Helpers;

public static class BDTestHtmlHelper
{
    public static string GetTestHtmlReport()
    {
        return GetTestHtmlReport(new BDTestRunDescriptor());
    }

    public static string GetTestHtmlReport(BDTestRunDescriptor bdTestRunDescriptor)
    {
        var scenarios = BDTestUtil.GetScenarios();
        var testTimer = GetTestTimer(scenarios);
        
        var dataToOutput = new BDTestOutputModel
        {
            Id = BDTestUtil.GetCurrentReportId,
            Environment = bdTestRunDescriptor?.Environment ?? "Not Specified",
            Tag = bdTestRunDescriptor?.Tag ?? "Not Specified", 
            BranchName = bdTestRunDescriptor?.BranchName ?? "Not Specified",
            MachineName = Environment.MachineName,
            Scenarios = scenarios,
            TestTimer = testTimer,
            NotRun = BDTestUtil.GetNotRunScenarios(),
            Version = BDTestVersionHelper.CurrentVersion
        };

        return GenerateHtmlReport(dataToOutput);
    }

    private static TestTimer GetTestTimer(IEnumerable<Scenario> scenarios)
    {
        var enumerable = scenarios.ToList();

        if (enumerable.Count == 0)
        {
            return new TestTimer();
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
        var html = new StringBuilder();
        
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("    <meta charset=\"UTF-8\">");
        html.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        html.AppendLine("    <title>BDTest Report</title>");
        html.AppendLine(GetEmbeddedStyles());
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        
        // Header section
        html.AppendLine("    <div class=\"header\">");
        html.AppendLine("        <h1>BDTest Report</h1>");
        html.AppendLine($"        <div class=\"report-meta\">");
        html.AppendLine($"            <span><strong>Environment:</strong> {EscapeHtml(data.Environment)}</span>");
        html.AppendLine($"            <span><strong>Tag:</strong> {EscapeHtml(data.Tag)}</span>");
        html.AppendLine($"            <span><strong>Branch:</strong> {EscapeHtml(data.BranchName)}</span>");
        html.AppendLine($"            <span><strong>Machine:</strong> {EscapeHtml(data.MachineName)}</span>");
        html.AppendLine($"            <span><strong>Version:</strong> {EscapeHtml(data.Version)}</span>");
        html.AppendLine($"        </div>");
        html.AppendLine("    </div>");

        // Summary section
        html.AppendLine(GenerateSummarySection(data));

        // Scenarios section
        html.AppendLine(GenerateScenariosSection(data.Scenarios));

        // Not run tests section
        if (data.NotRun.Any())
        {
            html.AppendLine(GenerateNotRunSection(data.NotRun));
        }

        html.AppendLine(GetEmbeddedScript());
        html.AppendLine("</body>");
        html.AppendLine("</html>");

        return html.ToString();
    }

    private static string GenerateSummarySection(BDTestOutputModel data)
    {
        var scenarios = data.Scenarios;
        var totalCount = scenarios.Count;
        var passedCount = scenarios.Count(s => s.Status == Status.Passed);
        var failedCount = scenarios.Count(s => s.Status == Status.Failed);
        var skippedCount = scenarios.Count(s => s.Status == Status.Skipped);
        var inconclusiveCount = scenarios.Count(s => s.Status == Status.Inconclusive);
        var notImplementedCount = scenarios.Count(s => s.Status == Status.NotImplemented);

        var duration = data.TestTimer?.TestsFinishedAt - data.TestTimer?.TestsStartedAt;
        var durationString = duration?.ToString(@"hh\:mm\:ss\.fff") ?? "Unknown";

        var html = new StringBuilder();
        html.AppendLine("    <div class=\"summary\">");
        html.AppendLine("        <h2>Test Summary</h2>");
        html.AppendLine("        <div class=\"summary-grid\">");
        html.AppendLine($"            <div class=\"summary-item total\"><span class=\"count\">{totalCount}</span><span class=\"label\">Total</span></div>");
        html.AppendLine($"            <div class=\"summary-item passed\"><span class=\"count\">{passedCount}</span><span class=\"label\">Passed</span></div>");
        html.AppendLine($"            <div class=\"summary-item failed\"><span class=\"count\">{failedCount}</span><span class=\"label\">Failed</span></div>");
        html.AppendLine($"            <div class=\"summary-item skipped\"><span class=\"count\">{skippedCount}</span><span class=\"label\">Skipped</span></div>");
        html.AppendLine($"            <div class=\"summary-item inconclusive\"><span class=\"count\">{inconclusiveCount}</span><span class=\"label\">Inconclusive</span></div>");
        html.AppendLine($"            <div class=\"summary-item not-implemented\"><span class=\"count\">{notImplementedCount}</span><span class=\"label\">Not Implemented</span></div>");
        html.AppendLine("        </div>");
        html.AppendLine($"        <div class=\"duration\"><strong>Duration:</strong> {durationString}</div>");
        
        if (data.TestTimer?.TestsStartedAt != null)
        {
            html.AppendLine($"        <div class=\"timestamp\"><strong>Started:</strong> {data.TestTimer.TestsStartedAt:yyyy-MM-dd HH:mm:ss}</div>");
        }
        if (data.TestTimer?.TestsFinishedAt != null)
        {
            html.AppendLine($"        <div class=\"timestamp\"><strong>Finished:</strong> {data.TestTimer.TestsFinishedAt:yyyy-MM-dd HH:mm:ss}</div>");
        }
        
        html.AppendLine("    </div>");
        return html.ToString();
    }

    private static string GenerateScenariosSection(List<Scenario> scenarios)
    {
        var html = new StringBuilder();
        html.AppendLine("    <div class=\"scenarios\">");
        html.AppendLine("        <h2>Test Scenarios</h2>");
        
        // Filter controls
        html.AppendLine("        <div class=\"filters\">");
        html.AppendLine("            <button class=\"filter-btn active\" data-status=\"all\">All</button>");
        html.AppendLine("            <button class=\"filter-btn\" data-status=\"passed\">Passed</button>");
        html.AppendLine("            <button class=\"filter-btn\" data-status=\"failed\">Failed</button>");
        html.AppendLine("            <button class=\"filter-btn\" data-status=\"skipped\">Skipped</button>");
        html.AppendLine("            <button class=\"filter-btn\" data-status=\"inconclusive\">Inconclusive</button>");
        html.AppendLine("            <button class=\"filter-btn\" data-status=\"notImplemented\">Not Implemented</button>");
        html.AppendLine("        </div>");

        html.AppendLine("        <div class=\"scenarios-list\">");
        
        foreach (var scenario in scenarios.OrderBy(s => s.GetStoryText()).ThenBy(s => s.GetScenarioText()))
        {
            html.AppendLine(GenerateScenarioItem(scenario));
        }
        
        html.AppendLine("        </div>");
        html.AppendLine("    </div>");
        return html.ToString();
    }

    private static string GenerateScenarioItem(Scenario scenario)
    {
        var statusClass = scenario.Status.ToString().ToLowerInvariant();
        var html = new StringBuilder();
        
        html.AppendLine($"        <div class=\"scenario-item {statusClass}\" data-status=\"{statusClass}\">");
        html.AppendLine($"            <div class=\"scenario-header\">");
        html.AppendLine($"                <div class=\"scenario-status {statusClass}\">{scenario.Status}</div>");
        html.AppendLine($"                <div class=\"scenario-info\">");
        html.AppendLine($"                    <div class=\"scenario-title\">{EscapeHtml(scenario.GetScenarioText())}</div>");
        html.AppendLine($"                    <div class=\"scenario-story\">{EscapeHtml(scenario.GetStoryText())}</div>");
        html.AppendLine($"                    <div class=\"scenario-meta\">");
        html.AppendLine($"                        <span class=\"duration\">{scenario.TimeTaken.TotalMilliseconds:F0}ms</span>");
        if (scenario.RetryCount > 0)
        {
            html.AppendLine($"                        <span class=\"retry-count\">Retries: {scenario.RetryCount}</span>");
        }
        html.AppendLine($"                    </div>");
        html.AppendLine($"                </div>");
        html.AppendLine($"                <button class=\"toggle-details\" onclick=\"toggleScenario('{scenario.Guid}')\">▼</button>");
        html.AppendLine($"            </div>");
        
        html.AppendLine($"            <div class=\"scenario-details\" id=\"details-{scenario.Guid}\" style=\"display: none;\">");
        
        // Steps
        if (scenario.Steps?.Any() == true)
        {
            html.AppendLine($"                <div class=\"steps\">");
            html.AppendLine($"                    <h4>Steps:</h4>");
            foreach (var step in scenario.Steps)
            {
                var stepStatusClass = step.Exception != null ? "failed" : "passed";
                html.AppendLine($"                    <div class=\"step {stepStatusClass}\">");
                html.AppendLine($"                        <div class=\"step-text\">{EscapeHtml(step.StepText)}</div>");
                if (step.Exception != null)
                {
                    html.AppendLine($"                        <div class=\"step-exception\">");
                    html.AppendLine($"                            <strong>Exception:</strong> {EscapeHtml(step.Exception.Message)}<br>");
                    if (!string.IsNullOrEmpty(step.Exception.StackTrace))
                    {
                        html.AppendLine($"                            <pre class=\"stack-trace\">{EscapeHtml(step.Exception.StackTrace)}</pre>");
                    }
                    html.AppendLine($"                        </div>");
                    }
                html.AppendLine($"                    </div>");
            }
            html.AppendLine($"                </div>");
        }
        
        // Output
        if (!string.IsNullOrEmpty(scenario.Output))
        {
            html.AppendLine($"                <div class=\"output\">");
            html.AppendLine($"                    <h4>Output:</h4>");
            html.AppendLine($"                    <pre>{EscapeHtml(scenario.Output)}</pre>");
            html.AppendLine($"                </div>");
        }
        
        // Custom HTML output
        if (!string.IsNullOrEmpty(scenario.CustomHtmlOutputForReport))
        {
            html.AppendLine($"                <div class=\"custom-html\">");
            html.AppendLine($"                    <h4>Custom Output:</h4>");
            html.AppendLine($"                    <div class=\"custom-html-content\">{scenario.CustomHtmlOutputForReport}</div>");
            html.AppendLine($"                </div>");
        }
        
        html.AppendLine($"            </div>");
        html.AppendLine($"        </div>");
        
        return html.ToString();
    }

    private static string GenerateNotRunSection(List<BuildableTest> notRun)
    {
        var html = new StringBuilder();
        html.AppendLine("    <div class=\"not-run\">");
        html.AppendLine("        <h2>Tests Not Run</h2>");
        html.AppendLine("        <div class=\"not-run-list\">");
        
        foreach (var test in notRun)
        {
            html.AppendLine($"        <div class=\"not-run-item\">");
            html.AppendLine($"            <div class=\"test-name\">{EscapeHtml(test.ScenarioText?.Scenario ?? "Unknown Scenario")}</div>");
            html.AppendLine($"            <div class=\"test-story\">{EscapeHtml(test.StoryText?.Story ?? "Unknown Story")}</div>");
            html.AppendLine($"        </div>");
        }
        
        html.AppendLine("        </div>");
        html.AppendLine("    </div>");
        return html.ToString();
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

    private static string GetEmbeddedStyles()
    {
        return @"
    <style>
        * {
            box-sizing: border-box;
            margin: 0;
            padding: 0;
        }
        
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, sans-serif;
            line-height: 1.6;
            color: #333;
            background-color: #f5f5f5;
        }
        
        .header {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 2rem;
            text-align: center;
        }
        
        .header h1 {
            margin-bottom: 1rem;
            font-size: 2.5rem;
        }
        
        .report-meta {
            display: flex;
            justify-content: center;
            flex-wrap: wrap;
            gap: 2rem;
        }
        
        .report-meta span {
            font-size: 0.9rem;
        }
        
        .summary {
            background: white;
            margin: 2rem;
            padding: 2rem;
            border-radius: 8px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }
        
        .summary h2 {
            margin-bottom: 1.5rem;
            color: #2c3e50;
        }
        
        .summary-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
            gap: 1rem;
            margin-bottom: 1rem;
        }
        
        .summary-item {
            text-align: center;
            padding: 1rem;
            border-radius: 6px;
            color: white;
        }
        
        .summary-item .count {
            display: block;
            font-size: 2rem;
            font-weight: bold;
        }
        
        .summary-item .label {
            display: block;
            font-size: 0.9rem;
            opacity: 0.9;
        }
        
        .summary-item.total { background-color: #3498db; }
        .summary-item.passed { background-color: #27ae60; }
        .summary-item.failed { background-color: #e74c3c; }
        .summary-item.skipped { background-color: #f39c12; }
        .summary-item.inconclusive { background-color: #95a5a6; }
        .summary-item.not-implemented { background-color: #9b59b6; }
        
        .duration, .timestamp {
            margin-top: 0.5rem;
            color: #7f8c8d;
        }
        
        .scenarios {
            background: white;
            margin: 2rem;
            padding: 2rem;
            border-radius: 8px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }
        
        .scenarios h2 {
            margin-bottom: 1.5rem;
            color: #2c3e50;
        }
        
        .filters {
            margin-bottom: 1.5rem;
            display: flex;
            flex-wrap: wrap;
            gap: 0.5rem;
        }
        
        .filter-btn {
            padding: 0.5rem 1rem;
            border: 1px solid #ddd;
            background: white;
            border-radius: 4px;
            cursor: pointer;
            transition: all 0.2s;
        }
        
        .filter-btn:hover, .filter-btn.active {
            background: #3498db;
            color: white;
            border-color: #3498db;
        }
        
        .scenario-item {
            border: 1px solid #e1e8ed;
            border-radius: 6px;
            margin-bottom: 1rem;
            overflow: hidden;
        }
        
        .scenario-header {
            display: flex;
            align-items: center;
            padding: 1rem;
            background: #fafafa;
            cursor: pointer;
        }
        
        .scenario-status {
            padding: 0.25rem 0.75rem;
            border-radius: 4px;
            font-size: 0.8rem;
            font-weight: bold;
            text-transform: uppercase;
            margin-right: 1rem;
            min-width: 100px;
            text-align: center;
        }
        
        .scenario-status.passed { background-color: #d4edda; color: #155724; }
        .scenario-status.failed { background-color: #f8d7da; color: #721c24; }
        .scenario-status.skipped { background-color: #fff3cd; color: #856404; }
        .scenario-status.inconclusive { background-color: #d1ecf1; color: #0c5460; }
        .scenario-status.notimplemented { background-color: #e2e3e5; color: #383d41; }
        
        .scenario-info {
            flex: 1;
        }
        
        .scenario-title {
            font-weight: bold;
            margin-bottom: 0.25rem;
        }
        
        .scenario-story {
            color: #6c757d;
            font-size: 0.9rem;
            margin-bottom: 0.25rem;
        }
        
        .scenario-meta {
            font-size: 0.8rem;
            color: #6c757d;
        }
        
        .scenario-meta span {
            margin-right: 1rem;
        }
        
        .toggle-details {
            background: none;
            border: none;
            font-size: 1.2rem;
            cursor: pointer;
            padding: 0.5rem;
            color: #6c757d;
        }
        
        .scenario-details {
            padding: 1rem;
            border-top: 1px solid #e1e8ed;
            background: white;
        }
        
        .scenario-details h4 {
            margin-bottom: 0.5rem;
            color: #495057;
        }
        
        .steps {
            margin-bottom: 1rem;
        }
        
        .step {
            padding: 0.5rem;
            margin-bottom: 0.5rem;
            border-radius: 4px;
            border-left: 4px solid #28a745;
        }
        
        .step.failed {
            border-left-color: #dc3545;
            background-color: #f8f9fa;
        }
        
        .step-text {
            font-weight: 500;
        }
        
        .step-exception {
            margin-top: 0.5rem;
            color: #dc3545;
            font-size: 0.9rem;
        }
        
        .stack-trace {
            background: #f8f9fa;
            padding: 0.5rem;
            border-radius: 4px;
            font-size: 0.8rem;
            overflow-x: auto;
            margin-top: 0.25rem;
        }
        
        .output, .custom-html {
            margin-bottom: 1rem;
        }
        
        .output pre {
            background: #f8f9fa;
            padding: 1rem;
            border-radius: 4px;
            overflow-x: auto;
            font-size: 0.9rem;
        }
        
        .custom-html-content {
            border: 1px solid #e1e8ed;
            padding: 1rem;
            border-radius: 4px;
            background: #f8f9fa;
        }
        
        .not-run {
            background: white;
            margin: 2rem;
            padding: 2rem;
            border-radius: 8px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }
        
        .not-run h2 {
            margin-bottom: 1.5rem;
            color: #2c3e50;
        }
        
        .not-run-item {
            padding: 1rem;
            border: 1px solid #e1e8ed;
            border-radius: 4px;
            margin-bottom: 0.5rem;
        }
        
        .test-name {
            font-weight: bold;
            margin-bottom: 0.25rem;
        }
        
        .test-story {
            color: #6c757d;
            font-size: 0.9rem;
        }
        
        @media (max-width: 768px) {
            .header {
                padding: 1rem;
            }
            
            .header h1 {
                font-size: 2rem;
            }
            
            .report-meta {
                flex-direction: column;
                gap: 0.5rem;
            }
            
            .summary, .scenarios, .not-run {
                margin: 1rem;
                padding: 1rem;
            }
            
            .scenario-header {
                flex-direction: column;
                align-items: flex-start;
            }
            
            .scenario-status {
                margin-bottom: 0.5rem;
                margin-right: 0;
            }
        }
    </style>";
    }

    private static string GetEmbeddedScript()
    {
        return @"
    <script>
        function toggleScenario(guid) {
            const details = document.getElementById('details-' + guid);
            const button = details.previousElementSibling.querySelector('.toggle-details');
            
            if (details.style.display === 'none' || details.style.display === '') {
                details.style.display = 'block';
                button.textContent = '▲';
            } else {
                details.style.display = 'none';
                button.textContent = '▼';
            }
        }
        
        document.addEventListener('DOMContentLoaded', function() {
            const filterBtns = document.querySelectorAll('.filter-btn');
            const scenarios = document.querySelectorAll('.scenario-item');
            
            filterBtns.forEach(btn => {
                btn.addEventListener('click', function() {
                    // Update active button
                    filterBtns.forEach(b => b.classList.remove('active'));
                    this.classList.add('active');
                    
                    const status = this.getAttribute('data-status');
                    
                    scenarios.forEach(scenario => {
                        if (status === 'all' || scenario.getAttribute('data-status') === status) {
                            scenario.style.display = 'block';
                        } else {
                            scenario.style.display = 'none';
                        }
                    });
                });
            });
        });
    </script>";
    }
}