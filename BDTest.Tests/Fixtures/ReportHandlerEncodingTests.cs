using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Web;
using BDTest.Razor.Reports.Extensions;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;
using BDTest.Razor.Reports.Models.ViewModels;
using BDTest.Test;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class ReportHandlerEncodingTests
{
    private static readonly string[] Values =
    [
        "release-1.2",
        "O'Brien's build",
        "');alert(1);//",
        "\");alert(1);//",
        "\" onmouseover=\"alert(1)",
        "</script><script>alert(1)</script>",
        "quotes '\" & < > \\ and Unicode café 雪",
        "line one\nline two\r\nline three\u2028line four\u2029",
        "",
        null
    ];

    [Test]
    public async Task Delete_values_are_data_and_never_executable_handler_source(
        [ValueSource(nameof(Values))] string value)
    {
        var record = ReportJson.DeserializeObject<TestRunSummary>("{}");
        record.RecordId = value;
        record.Tag = value;
        record.Environment = value;
        record.BranchName = value;
        record.StartedAtDateTime = new DateTime(2026, 10, 17, 12, 30, 0);
        record.FinishedAtDateTime = record.StartedAtDateTime.AddSeconds(1);

        var html = await RenderAsync("/Views/BDTest/TestRunList.cshtml", new[] { record }, isAdmin: true);
        var form = ReadElement(html, "<form[^>]*/report/delete/[^>]*>.*?</form>");
        Assert.That((string)form.Attribute("action"),
            Is.EqualTo("/bdtest/report/delete/" + Uri.EscapeDataString(record.RecordId ?? "")));
        Assert.That((string)form.Attribute("method"), Is.EqualTo("post"));
        var button = ReadElement(html, "<button[^>]*>Delete</button>");
        Assert.That((string)button.Attribute("type"), Is.EqualTo("submit"));
        Assert.That((string)button.Attribute("data-bdtest-action"), Is.EqualTo("delete"));
        Assert.That((string)button.Attribute("data-test-id"), Is.EqualTo(record.RecordId ?? ""));
        Assert.That((string)button.Attribute("data-tag"), Is.EqualTo(value ?? ""));
        Assert.That((string)button.Attribute("data-environment"), Is.EqualTo(value ?? ""));
        Assert.That((string)button.Attribute("data-branch"), Is.EqualTo(value ?? ""));
        Assert.That((string)button.Attribute("data-date"), Is.EqualTo(record.StartedAtDateTime.ToString()));
        Assert.That(button.Attributes().Select(x => x.Name.LocalName), Is.EquivalentTo(new[]
        {
            "type", "class", "data-bdtest-action", "data-test-id", "data-tag", "data-environment", "data-branch", "data-date"
        }));
    }

    [Test]
    public async Task Non_admins_do_not_receive_delete_buttons()
    {
        var record = ReportJson.DeserializeObject<TestRunSummary>("{}");
        record.RecordId = "test-run-123";
        var html = await RenderAsync("/Views/BDTest/TestRunList.cshtml", new[] { record });
        Assert.That(Regex.IsMatch(html, "<button[^>]*>Delete</button>"), Is.False);
    }

    private static readonly string[] UrlQueries =
    [
        "",
        "?filter=Passed&scenarioId=previous-scenario",
        "?filter=');alert(1);//",
        "?filter=\" onmouseover=\"alert(1)",
        "?filter=</script><script>alert(1)</script>",
        "?filter=café%20雪&other=%27%22%26%3C%3E%5C",
        "?filter=&other=one+two&scenarioId=old&scenarioId=duplicate"
    ];

    [Test]
    public async Task Copy_link_url_is_data_and_never_executable_handler_source(
        [ValueSource(nameof(UrlQueries))] string query)
    {
        var html = await RenderAsync<object>("/Views/Shared/_CopyLinkButton.cshtml", null, query: query);
        var button = ReadElement(html, "<button[^>]*>.*?Copy link</button>");
        Assert.That((string)button.Attribute("data-bdtest-action"), Is.EqualTo("copy-url"));
        Assert.That((string)button.Attribute("data-url"),
            Is.EqualTo("/bdtest/report/O'Brien/test-runs" + query));
        Assert.That(button.Attributes().Select(x => x.Name.LocalName),
            Is.EquivalentTo(new[] { "type", "class", "data-bdtest-action", "data-url" }));
    }

    [Test]
    public async Task Scenario_copy_url_is_data_and_preserves_the_selected_scenario(
        [ValueSource(nameof(UrlQueries))] string query)
    {
        var scenario = ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(new
        {
            Guid = "selected-scenario",
            Steps = Array.Empty<object>(),
            CustomTestInformation = Array.Empty<object>()
        }));
        var html = await RenderAsync("/Views/Shared/_ScenarioPanel.cshtml",
            new ScenarioModalViewModel { Scenario = scenario }, query: query);
        var button = ReadElement(html, "<button[^>]*data-bdtest-action=\"copy-url\"[^>]*>.*?</button>");
        Assert.That((string)button.Attribute("type"), Is.EqualTo("button"));
        Assert.That(button.Attributes().Select(x => x.Name.LocalName),
            Is.EquivalentTo(new[] { "class", "type", "data-bdtest-action", "data-url", "aria-label" }));

        var localUrl = (string)button.Attribute("data-url");
        Assert.That(localUrl, Does.StartWith("/bdtest/"));
        var url = new Uri(new Uri("https://reports.example.test"), localUrl);
        Assert.That(url.GetLeftPart(UriPartial.Path),
            Is.EqualTo("https://reports.example.test/bdtest/report/O'Brien/test-runs"));
        var actualQuery = HttpUtility.ParseQueryString(url.Query);
        var expectedQuery = HttpUtility.ParseQueryString(query);
        expectedQuery["scenarioId"] = scenario.Guid;
        Assert.That(actualQuery.AllKeys, Is.EquivalentTo(expectedQuery.AllKeys));
        foreach (var key in expectedQuery.AllKeys)
        {
            Assert.That(actualQuery.GetValues(key), Is.EqualTo(expectedQuery.GetValues(key)));
        }
    }

    private static XElement ReadElement(string html, string pattern)
    {
        var match = Regex.Match(html, pattern, RegexOptions.Singleline);
        Assert.That(match.Success, Is.True, "The expected report control was not rendered.");
        return XElement.Parse(match.Value, LoadOptions.PreserveWhitespace);
    }

    internal static async Task<string> RenderAsync<T>(string path, T model, bool isAdmin = false, string query = "", string pathBase = "", string host = "reports.example.test", string requestPath = "/bdtest/report/O'Brien/test-runs", IBDTestCustomHeaderLinksProvider customHeaderLinksProvider = null, Action<ViewDataDictionary> configureViewData = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddControllersWithViews().AddBdTestReportControllersAndViews();
        builder.Services.AddAuthorization(options => options.AddPolicy("EncodingTests", policy =>
            policy.RequireAssertion(_ => isAdmin)));
        await using var app = builder.Build();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        services.GetRequiredService<BDTestReportServerOptions>().AdminAuthorizationPolicy = "EncodingTests";
        services.GetRequiredService<BDTestReportServerOptions>().CustomHeaderLinksProvider = customHeaderLinksProvider;
        var httpContext = new DefaultHttpContext { RequestServices = services };
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString(host);
        httpContext.Request.PathBase = pathBase;
        httpContext.Request.Path = requestPath;
        httpContext.Request.QueryString = new QueryString(query);
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var view = services.GetRequiredService<ICompositeViewEngine>().GetView(null, path, isMainPage: false);
        Assert.That(view.Success, Is.True, $"Razor view not found: {path}");
        using var writer = new StringWriter();
        var viewData = new ViewDataDictionary<T>(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model };
        configureViewData?.Invoke(viewData);
        var viewContext = new ViewContext(actionContext, view.View, viewData,
            new TempDataDictionary(httpContext, services.GetRequiredService<ITempDataProvider>()),
            writer, new HtmlHelperOptions());
        await view.View.RenderAsync(viewContext);
        return writer.ToString();
    }
}
