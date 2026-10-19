using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Web;
using BDTest.Razor.Reports.Extensions;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;
using BDTest.Razor.Reports.Models.ViewModels;
using BDTest.Test;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

public class ReportHandlerEncodingTests
{
    public static IEnumerable<string> GetUrlQueries() => UrlQueries;

    public static IEnumerable<string?> GetValues() => Values;

    private static readonly string?[] Values =
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
    [MatrixDataSource]
    public async Task Delete_values_are_data_and_never_executable_handler_source(
        [MatrixMethod<ReportHandlerEncodingTests>(nameof(GetValues))] string? value)
    {
        var record = ReportJson.DeserializeObject<TestRunSummary>("{}");
        record.RecordId = value;
        record.Tag = value;
        record.Environment = value;
        record.BranchName = value;
        record.StartedAtDateTime = new DateTime(2026, 10, 17, 12, 30, 0);
        record.FinishedAtDateTime = record.StartedAtDateTime.AddSeconds(1);

        var html = await RenderAsync("/Views/BDTest/TestRunList.cshtml", new[] { record }, isAdmin: true);
        if (string.IsNullOrEmpty(value))
        {
            await Assert.That(html).DoesNotContain("data-bdtest-action=\"delete\"");
            return;
        }
        var form = await ReadElement(html, "<form[^>]*data-bdtest-delete[^>]*>.*?</form>");
        var action = (string?)form.Attribute("action");
        await Assert.That(action).StartsWith("/bdtest/reports/");
        await Assert.That(Uri.UnescapeDataString(Required(action)["/bdtest/reports/".Length..])).IsEqualTo(record.RecordId);
        await Assert.That((string?)form.Attribute("method")).IsEqualTo("post");
        var button = await ReadElement(html, "<button[^>]*>Delete</button>");
        await Assert.That((string?)button.Attribute("type")).IsEqualTo("submit");
        await Assert.That((string?)button.Attribute("data-bdtest-action")).IsEqualTo("delete");
        await Assert.That((string?)button.Attribute("data-test-id")).IsEqualTo(record.RecordId ?? "");
        await Assert.That((string?)button.Attribute("data-tag")).IsEqualTo(value ?? "");
        await Assert.That((string?)button.Attribute("data-environment")).IsEqualTo(value ?? "");
        await Assert.That((string?)button.Attribute("data-branch")).IsEqualTo(value ?? "");
        await Assert.That((string?)button.Attribute("data-date")).IsEqualTo(record.StartedAtDateTime.ToString());

        await Assert.That(button.Attributes().Select(x => x.Name.LocalName)).IsEquivalentTo(new[]
        {
            "type", "class", "data-bdtest-action", "data-test-id", "data-tag", "data-environment", "data-branch", "data-date"
        }, global::TUnit.Assertions.Enums.CollectionOrdering.Any);
    }

    [Test]
    public async Task Non_admins_do_not_receive_delete_buttons()
    {
        var record = ReportJson.DeserializeObject<TestRunSummary>("{}");
        record.RecordId = "test-run-123";
        var html = await RenderAsync("/Views/BDTest/TestRunList.cshtml", new[] { record });
        await Assert.That(Regex.IsMatch(html, "<button[^>]*>Delete</button>")).IsFalse();
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
    [MatrixDataSource]
    public async Task Copy_link_url_is_data_and_never_executable_handler_source(
        [MatrixMethod<ReportHandlerEncodingTests>(nameof(GetUrlQueries))] string query)
    {
        var html = await RenderAsync<object>("/Views/Shared/_CopyLinkButton.cshtml", null, query: query);
        var button = await ReadElement(html, "<button[^>]*>.*?Copy link</button>");
        await Assert.That((string?)button.Attribute("data-bdtest-action")).IsEqualTo("copy-url");
        await Assert.That((string?)button.Attribute("data-url")).IsEqualTo("/bdtest/report/O'Brien/test-runs" + query);

        await Assert.That(button.Attributes().Select(x => x.Name.LocalName)).IsEquivalentTo(new[] { "type", "class", "data-bdtest-action", "data-url" }, global::TUnit.Assertions.Enums.CollectionOrdering.Any);
    }

    [Test]
    [MatrixDataSource]
    public async Task Scenario_copy_url_is_data_and_preserves_the_selected_scenario(
        [MatrixMethod<ReportHandlerEncodingTests>(nameof(GetUrlQueries))] string query)
    {
        var scenario = ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(new
        {
            Guid = "selected-scenario",
            Steps = Array.Empty<object>(),
            CustomTestInformation = Array.Empty<object>()
        }));
        var html = await RenderAsync("/Views/Shared/_ScenarioPanel.cshtml",
            new ScenarioModalViewModel { Scenario = scenario }, query: query);
        var button = await ReadElement(html, "<button[^>]*data-bdtest-action=\"copy-url\"[^>]*>.*?</button>");
        await Assert.That((string?)button.Attribute("type")).IsEqualTo("button");

        await Assert.That(button.Attributes().Select(x => x.Name.LocalName)).IsEquivalentTo(new[] { "class", "type", "data-bdtest-action", "data-url", "aria-label" }, global::TUnit.Assertions.Enums.CollectionOrdering.Any);

        var localUrl = (string?)button.Attribute("data-url");
        await Assert.That(localUrl).StartsWith("/bdtest/");
        var url = new Uri(new Uri("https://reports.example.test"), localUrl);
        await Assert.That(url.GetLeftPart(UriPartial.Path)).IsEqualTo("https://reports.example.test/bdtest/report/O'Brien/test-runs");
        var actualQuery = HttpUtility.ParseQueryString(url.Query);
        var expectedQuery = HttpUtility.ParseQueryString(query);
        expectedQuery["scenarioId"] = scenario.Guid;

        await Assert.That(actualQuery.AllKeys).IsEquivalentTo(expectedQuery.AllKeys, global::TUnit.Assertions.Enums.CollectionOrdering.Any);
        foreach (var key in expectedQuery.AllKeys)
        {
            await Assert.That(Required(actualQuery.GetValues(key))).IsEquivalentTo(Required(expectedQuery.GetValues(key)), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }
    }

    private static async Task<XElement> ReadElement(string html, string pattern)
    {
        var match = Regex.Match(html, pattern, RegexOptions.Singleline);
        await Assert.That(match.Success).IsTrue().Because("The expected report control was not rendered.");
        return XElement.Parse(match.Value, LoadOptions.PreserveWhitespace);
    }

    internal static async Task<string> RenderAsync<T>(string path, T? model, bool isAdmin = false, string query = "", string pathBase = "", string host = "reports.example.test", string requestPath = "/bdtest/report/O'Brien/test-runs", IBDTestCustomHeaderLinksProvider? customHeaderLinksProvider = null, Action<ViewDataDictionary>? configureViewData = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddBDTestReports(options => options.AdminAuthorizationPolicy = "EncodingTests");
        if (customHeaderLinksProvider != null) builder.Services.AddSingleton(customHeaderLinksProvider);
        builder.Services.AddAuthorization(options => options.AddPolicy("EncodingTests", policy =>
            policy.RequireAssertion(_ => isAdmin)));
        builder.Logging.ClearProviders();
        await using var app = builder.Build();
        app.MapBDTestReports();
        await app.StartAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var httpContext = new DefaultHttpContext { RequestServices = services };
        httpContext.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), "Render report view"));
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString(host);
        httpContext.Request.PathBase = pathBase;
        httpContext.Request.Path = requestPath;
        httpContext.Request.QueryString = new QueryString(query);
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var view = services.GetRequiredService<ICompositeViewEngine>().GetView(null, path, isMainPage: false);
        await Assert.That(view.Success).IsTrue().Because($"Razor view not found: {path}");
        using var writer = new StringWriter();
        var viewData = new ViewDataDictionary<T?>(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model };
        configureViewData?.Invoke(viewData);
        var viewContext = new ViewContext(actionContext, Required(view.View), viewData,
            new TempDataDictionary(httpContext, services.GetRequiredService<ITempDataProvider>()),
            writer, new HtmlHelperOptions());
        await Required(view.View).RenderAsync(viewContext);
        return writer.ToString();
    }
}
