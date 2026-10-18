using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using BDTest.Razor.Reports.Extensions;
using BDTest.Test;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class ReportRoutingTests
{
    [TestCase(null, "")]
    [TestCase("/bdtest", "")]
    [TestCase("/reports", "")]
    [TestCase("/team/reports/", "/proxy")]
    public async Task Mapping_keeps_navigation_assets_uploads_and_headers_inside_the_prefix(string prefix, string pathBase)
    {
        await using var server = await Server.Start(prefix, pathBase);
        var reportBase = pathBase + (prefix ?? "/bdtest").TrimEnd('/');
        using var root = await server.Client.GetAsync(pathBase + "/");
        Assert.That(await root.Content.ReadAsStringAsync(), Is.EqualTo("Host home"));
        Assert.That(root.Headers.Contains("Content-Security-Policy"), Is.False);
        using var host = await server.Client.GetAsync(pathBase + "/host-routing-test");
        Assert.That(await host.Content.ReadAsStringAsync(), Is.EqualTo("Host controller"));

        using var landing = await server.Client.GetAsync(reportBase);
        Assert.That(landing.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(landing.Headers.Location!.OriginalString, Is.EqualTo(reportBase + "/report/test-runs"));
        using var unknown = await server.Client.GetAsync(reportBase + "/missing");
        Assert.That(unknown.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(unknown.Headers.Contains("Content-Security-Policy"), Is.True);

        using var run = BDTestRun.Start();
        var fixture = new RoutingFixture(run);
        var scenario = await fixture.When(() => { }).Then(() => { }).BDTestAsync();
        var uri = await BDTestReportServer.SendDataAndGetReportUriAsync(
            new Uri(server.Client.BaseAddress!, reportBase), new BDTestRunDescriptor(), server.Client, run);
        Assert.That(uri.AbsolutePath, Does.StartWith(reportBase + "/report/"));
        var id = uri.Segments[^2].TrimEnd('/');
        foreach (var page in new[] { "summary", "all-scenarios", "stories", "top-defects", "timings", "warnings", "scenario/" + scenario.Guid })
        {
            using var response = await server.Client.GetAsync(reportBase + "/report/" + id + "/" + page);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Headers.Contains("Content-Security-Policy"), Is.True);
            await AssertLinksAndAssets(response, server.Client, reportBase);
        }
        foreach (var page in new[] { "test-runs", "history", "history/flaky", "history/timing" })
        {
            using var response = await server.Client.GetAsync(reportBase + "/report/" + page);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            await AssertLinksAndAssets(response, server.Client, reportBase);
        }
        foreach (var outside in new[] { "/ping", "/data", "/BDTest/TestRuns", "/_content/BDTest.Razor.Reports/css/bdtest-report.css" })
        {
            using var response = await server.Client.GetAsync(pathBase + outside);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }
        if (prefix != null && prefix != "/bdtest")
        {
            using var response = await server.Client.GetAsync(pathBase + "/bdtest/ping");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Report_mapping_does_not_publish_host_controllers_without_explicit_host_mapping(bool protect)
    {
        await using var server = await Server.Start(protect: protect, mapHost: false);
        var endpoints = server.App.Services.GetRequiredService<EndpointDataSource>().Endpoints;
        Assert.That(endpoints.Select(endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>())
            .Where(action => action != null).Select(action => action.ControllerTypeInfo.Assembly),
            Has.All.EqualTo(typeof(BDTestExtensions).Assembly));
        using var host = await server.Client.GetAsync("/host-routing-test");
        Assert.That(host.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        using var report = await server.Client.GetAsync("/reports/ping");
        Assert.That(report.StatusCode, Is.EqualTo(protect ? HttpStatusCode.Unauthorized : HttpStatusCode.OK));
    }

    [Test]
    public async Task MapControllers_alone_does_not_expose_report_controllers()
    {
        await using var server = await Server.Start(map: false);
        foreach (var path in new[] { "/bdtest/ping", "/bdtest/report/test-runs", "/ping", "/report/test-runs", "/BDTest/TestRuns" })
        {
            using var response = await server.Client.GetAsync(path);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }
        Assert.That(await server.Client.GetStringAsync("/"), Is.EqualTo("Host home"));
    }

    [Test]
    public async Task Endpoint_conventions_protect_every_report_endpoint_without_affecting_host_controllers()
    {
        await using var server = await Server.Start(protect: true);
        var endpoints = server.App.Services.GetRequiredService<EndpointDataSource>().Endpoints;
        foreach (var endpoint in endpoints)
        {
            var controller = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();
            if (endpoint.Metadata.GetMetadata<ISuppressMatchingMetadata>()?.SuppressMatching == true)
            {
                if (controller?.ControllerTypeInfo.Assembly == typeof(BDTestExtensions).Assembly)
                    Assert.That(endpoint.Metadata.GetMetadata<ISuppressLinkGenerationMetadata>()?.SuppressLinkGeneration, Is.True);
                continue;
            }
            if (controller?.ControllerTypeInfo.AsType() == typeof(ReportRoutingHostController))
                Assert.That(endpoint.Metadata.GetMetadata<ReportMarker>(), Is.Null);
            else if (controller?.ControllerTypeInfo.Assembly == typeof(BDTestExtensions).Assembly
                || endpoint is RouteEndpoint route && route.RoutePattern.RawText!.StartsWith("/reports/assets", StringComparison.Ordinal))
                Assert.That(endpoint.Metadata.GetMetadata<ReportMarker>(), Is.Not.Null);
        }
        foreach (var path in new[] { "/reports", "/reports/ping", "/reports/error", "/reports/report/test-runs", "/reports/assets/js/BDTest.js" })
        {
            using var response = await server.Client.GetAsync(path);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), path);
        }
        using var upload = await server.Client.PostAsync("/reports/data", new StringContent("{}"));
        Assert.That(upload.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        using var attachment = await server.Client.PostAsync("/reports/data/report/attachments/file", new StringContent("content"));
        Assert.That(attachment.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(await server.Client.GetStringAsync("/host-routing-test"), Is.EqualTo("Host controller"));
    }

    [TestCase("/")]
    [TestCase("")]
    [TestCase("reports")]
    [TestCase("//reports")]
    [TestCase("/reports/{id}")]
    public async Task Invalid_prefixes_fail_before_mapping_endpoints(string prefix)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddBDTestReports();
        await using var app = builder.Build();
        Assert.Throws<ArgumentException>(() => app.MapBDTestReports(prefix));
    }

    [TestCase("/bdtest", null)]
    [TestCase("/bdtest", "")]
    [TestCase("/reports", null)]
    [TestCase("/reports", "")]
    public async Task Reports_with_missing_scenario_ids_render_without_broken_detail_links(string prefix, string missingId)
    {
        await using var server = await Server.Start(prefix, "/proxy", mapHost: false);
        var reportBase = "/proxy" + prefix;
        string reportId = null;
        foreach (var status in new[] { "Passed", "Failed" })
        {
            var json = ReportJson.SerializeObject(new
            {
                TestTimer = new { TestsStartedAt = DateTime.UtcNow, TestsFinishedAt = DateTime.UtcNow.AddSeconds(1) },
                Scenarios = new[]
                {
                    new { Guid = missingId, ScenarioText = "Missing ID scenario", StoryText = "Routing", Status = status,
                        Steps = Array.Empty<object>(), CustomTestInformation = Array.Empty<object>() },
                    new { Guid = "valid-scenario", ScenarioText = "Valid ID scenario", StoryText = "Routing", Status = "Passed",
                        Steps = Array.Empty<object>(), CustomTestInformation = Array.Empty<object>() }
                }
            });
            using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            using var upload = await server.Client.PostAsync(reportBase + "/data", content);
            Assert.That(upload.StatusCode, Is.EqualTo(HttpStatusCode.OK), await upload.Content.ReadAsStringAsync());
            reportId = upload.Headers.GetValues("X-BDTest-Report-Id").Single();
        }

        foreach (var page in new[] { reportId + "/summary", reportId + "/all-scenarios", reportId + "/top-defects",
                     reportId + "/timings", reportId + "/scenario/valid-scenario", "history/flaky", "history/timing" })
        {
            using var response = await server.Client.GetAsync(reportBase + "/report/" + page);
            var html = await response.Content.ReadAsStringAsync();
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), page);
            if (!page.Contains("/scenario/", StringComparison.Ordinal))
                Assert.That(html, Does.Contain("Missing ID scenario"), page);
            Assert.That(html, Does.Not.Match("href=\"[^\"]*/scenario/\""), page);
            if (page.EndsWith("/summary", StringComparison.Ordinal))
                Assert.That(html, Does.Contain(reportBase + "/report/" + reportId + "/scenario/valid-scenario"));
        }
    }

    private static async Task AssertLinksAndAssets(HttpResponseMessage response, HttpClient client, string prefix)
    {
        var html = await response.Content.ReadAsStringAsync();
        var links = Regex.Matches(html, "(?:href|src|action)=\"([^\"]+)\"")
            .Select(match => HttpUtility.HtmlDecode(match.Groups[1].Value))
            .Where(link => link.StartsWith('/')).ToArray();
        Assert.That(links, Is.Not.Empty);
        Assert.That(links, Has.All.StartsWith(prefix + "/"));
        foreach (var asset in links.Where(link => link.Contains("/assets/", StringComparison.Ordinal)).Distinct())
        {
            using var result = await client.GetAsync(asset);
            Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK), asset);
            Assert.That((await result.Content.ReadAsByteArrayAsync()).Length, Is.GreaterThan(0));
            Assert.That(result.Headers.Contains("Content-Security-Policy"), Is.True);
        }
    }

    private sealed record ReportMarker;

    private sealed class RoutingFixture(BDTestRun run) : AbstractContextBDTestBase<object>
    {
        public override BDTestRun TestRun => run;
        protected override string BDTestExecutionId => "routing-fixture";
    }

    private sealed class Server(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public WebApplication App { get; } = app;
        public HttpClient Client { get; } = client;

        internal static async Task<Server> Start(string prefix = "/reports", string pathBase = "", bool map = true, bool protect = false, bool mapHost = true)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Logging.ClearProviders();
            builder.Services.AddControllersWithViews().AddApplicationPart(typeof(ReportRoutingHostController).Assembly);
            builder.Services.AddBDTestReports(options =>
            {
                options.UploadApiKey = "routing-test-key";
                options.AllowAnonymousReports = true;
            });
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
                options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; });
            builder.Services.AddAuthorization(options => options.AddPolicy("Reports", policy => policy.RequireAuthenticatedUser()));
            var app = builder.Build();
            app.UsePathBase(pathBase);
            app.UseBDTestReportServer();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            if (map)
            {
                var reports = prefix == null ? app.MapBDTestReports() : app.MapBDTestReports(prefix);
                if (protect)
                {
                    reports.RequireAuthorization("Reports");
                    reports.Add(endpoint => endpoint.Metadata.Add(new ReportMarker()));
                }
            }
            if (mapHost)
            {
                app.MapControllers();
                app.MapControllerRoute("host", "{controller}/{action}");
            }
            app.MapGet("/", () => "Host home");
            await app.StartAsync();
            var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(app.Urls.Single()) };
            client.DefaultRequestHeaders.Add("X-BDTest-Upload-Key", "routing-test-key");
            return new Server(app, client);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.DisposeAsync();
        }
    }
}

public sealed class ReportRoutingHostController : Controller
{
    [HttpGet("/host-routing-test")]
    public IActionResult Home() => Content("Host controller");
}
