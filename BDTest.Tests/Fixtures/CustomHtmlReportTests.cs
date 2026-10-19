using System.Text.RegularExpressions;
using System.Xml.Linq;
using BDTest.Razor.Reports.Extensions;
using BDTest.Razor.Reports.Models.ViewModels;
using BDTest.Test;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

public class CustomHtmlReportTests
{
    private WebApplication _application = null!; // Assigned by the per-test setup hook.

    [Before(HookType.Test)]
    public async Task CreateApplication()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(BDTestExtensions).Assembly.GetName().Name
        });
        builder.Services.AddBDTestReports();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        _application = builder.Build();
        _application.MapBDTestReports();
        await _application.StartAsync();
    }

    [After(HookType.Test)]
    public async Task DisposeApplication() => await _application.DisposeAsync();

    public static IEnumerable<string> GetPayloads() => Payloads;

    private static readonly string[] Payloads =
    {
        "<script>parent.document.body.innerHTML = 'compromised';</script>",
        "<img src=x onerror=alert(document.cookie)>",
        "<svg onload=alert(1)></svg>",
        "\"><script>alert(1)</script><iframe sandbox=\"allow-scripts allow-same-origin\" srcdoc=\"",
        "</iframe><script>alert(1)</script>&quot; onload=alert(1) &amp; &#34;",
        "<a href=\"javascript:alert(1)\" target=\"_top\">click</a>",
        "<form action=\"https://example.com\"><input name=secret></form>",
        "<style>body { color: red; }</style><h2>Results</h2><table><tr><td>A &amp; B</td></tr></table>"
    };

    [Test]
    [MatrixDataSource]
    public async Task CustomHtmlIsIsolatedFromTheReport(
        [MatrixMethod<CustomHtmlReportTests>(nameof(GetPayloads))] string payload)
    {
        var html = await RenderScenario(false, payload);
        var match = Regex.Match(html, @"<iframe\b[^>]*>");
        await Assert.That(match.Success).IsTrue().Because("Custom output must be in an iframe on the scenario page.");

        var frame = XElement.Parse(match.Value + "</iframe>");
        await Assert.That(frame.Attribute("sandbox")?.Value).IsEqualTo(string.Empty).Because("No script, same-origin, form, popup, or top-navigation sandbox permissions may be granted.");
        await Assert.That(frame.Attribute("title")?.Value).IsNotEmpty();
        await Assert.That(frame.Attribute("referrerpolicy")?.Value).IsEqualTo("no-referrer");

        var document = Required(frame.Attribute("srcdoc")).Value;
        await Assert.That(document).EndsWith(payload + "</body></html>").Because("Razor must attribute-encode the complete document without losing user formatting.");
        var policy = Regex.Match(document, "<meta http-equiv=\"Content-Security-Policy\" content=\"([^\"]*)\">");
        await Assert.That(policy.Success).IsTrue();
        await Assert.That(policy.Index).IsLessThan(document.IndexOf("<body>", StringComparison.Ordinal));
        await Assert.That(policy.Groups[1].Value).Contains("default-src 'none'");
        await Assert.That(policy.Groups[1].Value).Contains("base-uri 'none'");
        await Assert.That(policy.Groups[1].Value).Contains("form-action 'none'");
        await Assert.That(policy.Groups[1].Value).Contains("style-src 'unsafe-inline'");
        await Assert.That(policy.Groups[1].Value).Contains("img-src data: https: http:");
        await Assert.That(html.Remove(match.Index, match.Length)).DoesNotContain(payload).Because("Uploaded HTML must never be emitted into the parent report document.");
    }

    [Test]
    [MatrixDataSource]
    public async Task SidePanelNeverEmbedsCustomHtml(
        [MatrixMethod<CustomHtmlReportTests>(nameof(GetPayloads))] string payload)
    {
        // The Scenarios side panel links to the full page instead of rendering uploaded HTML.
        var html = await RenderScenario(true, payload);
        await Assert.That(html).DoesNotContain("<iframe");
        await Assert.That(html).DoesNotContain(payload);
    }

    [Test]
    [MatrixDataSource]
    public async Task EmptyCustomOutputDoesNotCreateAFrame(
        [Matrix(false, true)] bool modal,
        [Matrix(null, "")] string? payload)
    {
        await Assert.That(await RenderScenario(modal, payload)).DoesNotContain("<iframe");
    }

    private async Task<string> RenderScenario(bool modal, string? payload)
    {
        // Use the same JSON deserialization boundary as uploaded report data.
        var scenario = ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(new
        {
            Guid = "custom-html-test",
            ReportId = "custom-html-run",
            Steps = Array.Empty<object>(),
            CustomTestInformation = Array.Empty<object>(),
            CustomHtmlOutputForReport = payload
        }));
        using var scope = _application.Services.CreateScope();
        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("reports.example.com");
        httpContext.Request.Path = "/bdtest/report/scenario";
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var engine = scope.ServiceProvider.GetRequiredService<IRazorViewEngine>();
        var view = engine.GetView(null, modal
            ? "/Views/Shared/_ScenarioPanel.cshtml"
            : "/Views/BDTest/Scenario.cshtml", false);
        await Assert.That(view.Success).IsTrue();
        using var writer = new StringWriter();
        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        {
            Model = modal ? new ScenarioModalViewModel { Scenario = scenario } : scenario
        };
        var tempData = new TempDataDictionary(httpContext, scope.ServiceProvider.GetRequiredService<ITempDataProvider>());
        var razorView = Required(view.View);
        await razorView.RenderAsync(new ViewContext(actionContext, razorView, viewData, tempData, writer, new HtmlHelperOptions()));
        return writer.ToString();
    }
}
