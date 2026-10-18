using System.Text.RegularExpressions;
using System.Xml.Linq;
using BDTest.Razor.Reports.Extensions;
using BDTest.Razor.Reports.Models.ViewModels;
using BDTest.Test;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class CustomHtmlReportTests
{
    private WebApplication _application;

    [OneTimeSetUp]
    public void CreateApplication()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(BDTestExtensions).Assembly.GetName().Name
        });
        builder.Services.AddControllersWithViews().AddBdTestReportControllersAndViews();
        _application = builder.Build();
    }

    [OneTimeTearDown]
    public async Task DisposeApplication() => await _application.DisposeAsync();

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
    public async Task CustomHtmlIsIsolatedFromTheReport(
        [ValueSource(nameof(Payloads))] string payload)
    {
        var html = await RenderScenario(false, payload);
        var match = Regex.Match(html, @"<iframe\b[^>]*>");
        Assert.That(match.Success, Is.True, "Custom output must be in an iframe on the scenario page.");

        var frame = XElement.Parse(match.Value + "</iframe>");
        Assert.That(frame.Attribute("sandbox")?.Value, Is.EqualTo(string.Empty),
            "No script, same-origin, form, popup, or top-navigation sandbox permissions may be granted.");
        Assert.That(frame.Attribute("title")?.Value, Is.Not.Empty);
        Assert.That(frame.Attribute("referrerpolicy")?.Value, Is.EqualTo("no-referrer"));

        var document = frame.Attribute("srcdoc")?.Value;
        Assert.That(document, Does.EndWith(payload + "</body></html>"),
            "Razor must attribute-encode the complete document without losing user formatting.");
        var policy = Regex.Match(document, "<meta http-equiv=\"Content-Security-Policy\" content=\"([^\"]*)\">");
        Assert.That(policy.Success, Is.True);
        Assert.That(policy.Index, Is.LessThan(document.IndexOf("<body>", StringComparison.Ordinal)));
        Assert.That(policy.Groups[1].Value, Does.Contain("default-src 'none'"));
        Assert.That(policy.Groups[1].Value, Does.Contain("base-uri 'none'"));
        Assert.That(policy.Groups[1].Value, Does.Contain("form-action 'none'"));
        Assert.That(policy.Groups[1].Value, Does.Contain("style-src 'unsafe-inline'"));
        Assert.That(policy.Groups[1].Value, Does.Contain("img-src data: https: http:"));
        Assert.That(html.Remove(match.Index, match.Length), Does.Not.Contain(payload),
            "Uploaded HTML must never be emitted into the parent report document.");
    }

    [Test]
    public async Task SidePanelNeverEmbedsCustomHtml(
        [ValueSource(nameof(Payloads))] string payload)
    {
        // The Scenarios side panel links to the full page instead of rendering uploaded HTML.
        var html = await RenderScenario(true, payload);
        Assert.That(html, Does.Not.Contain("<iframe"));
        Assert.That(html, Does.Not.Contain(payload));
    }

    [Test]
    public async Task EmptyCustomOutputDoesNotCreateAFrame(
        [Values(false, true)] bool modal,
        [Values(null, "")] string payload)
    {
        Assert.That(await RenderScenario(modal, payload), Does.Not.Contain("<iframe"));
    }

    private async Task<string> RenderScenario(bool modal, string payload)
    {
        // Use the same JSON deserialization boundary as uploaded report data.
        var scenario = ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(new
        {
            Guid = "custom-html-test",
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
        Assert.That(view.Success, Is.True);
        using var writer = new StringWriter();
        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        {
            Model = modal ? new ScenarioModalViewModel { Scenario = scenario } : scenario
        };
        var tempData = new TempDataDictionary(httpContext, scope.ServiceProvider.GetRequiredService<ITempDataProvider>());
        await view.View.RenderAsync(new ViewContext(actionContext, view.View, viewData, tempData, writer, new HtmlHelperOptions()));
        return writer.ToString();
    }
}
