using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using BDTest.Razor.Reports.Controllers;
using BDTest.Razor.Reports.Extensions;
using BDTest.Razor.Reports.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class ReportAuthorizationTests
{
    // Enumerate every report route, including aliases and POST-based comparisons.
    private static IEnumerable<TestCaseData> ReadRoutes()
    {
        foreach (var controller in new[] { typeof(BDTestController), typeof(AttachmentsController) })
        foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (action.Name == nameof(BDTestController.DeleteReport)
                || controller == typeof(BDTestController) && action.Name is nameof(BDTestController.Ping) or nameof(BDTestController.Index) or nameof(BDTestController.Error)
                || controller == typeof(AttachmentsController) && action.Name == nameof(AttachmentsController.Upload)) continue;
            var methods = action.GetCustomAttributes<HttpMethodAttribute>().SelectMany(attribute => attribute.HttpMethods).ToArray();
            foreach (var route in action.GetCustomAttributes().OfType<IRouteTemplateProvider>().Where(route => route.Template != null))
            {
                var path = route.Template.StartsWith('/') ? route.Template : "/bdtest/" + route.Template;
                path = System.Text.RegularExpressions.Regex.Replace(path, "\\{[^}]+\\}", "missing");
                foreach (var prefix in new[] { "/bdtest", "/team/reports" })
                foreach (var method in methods)
                    yield return new TestCaseData(method, path.Replace("/bdtest/", prefix + "/"), prefix)
                        .SetName($"Report_read_denied_{method}_{prefix}_{path}");
            }
        }
    }

    [TestCaseSource(nameof(ReadRoutes))]
    public async Task Every_read_route_fails_closed_and_enforces_custom_policy(string method, string path, string prefix)
    {
        await using var host = await ReportHost.Start(prefix: prefix);
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        using var denied = await host.Client.SendAsync(request);
        Assert.That(denied.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(host.Logs.Messages.Any(message => message.Contains("ReportsAuthorizationPolicy")), Is.True);

        host.Options.ReportsAuthorizationPolicy = "Readers";
        using var anonymousRequest = new HttpRequestMessage(new HttpMethod(method), path);
        using var anonymous = await host.Client.SendAsync(anonymousRequest);
        Assert.That(anonymous.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        host.Client.DefaultRequestHeaders.Add("X-Test-Role", "Other");
        using var otherRequest = new HttpRequestMessage(new HttpMethod(method), path);
        using var other = await host.Client.SendAsync(otherRequest);
        Assert.That(other.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Omitting_options_middleware_still_denies_reads()
    {
        await using var host = await ReportHost.Start(useOptionsMiddleware: false, addAuthentication: false);
        using var response = await host.Client.GetAsync("/bdtest/report/test-runs");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(host.Logs.Messages.Any(message => message.Contains("ReportsAuthorizationPolicy")), Is.True);
    }

    [Test]
    public async Task Explicit_allow_anonymous_policy_is_supported()
    {
        await using var host = await ReportHost.Start(options => options.ReportsAuthorizationPolicy = "PublicReports");
        using var response = await host.Client.GetAsync("/bdtest/report/test-runs");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Forged_admin_cookie_cannot_reach_deletion()
    {
        await using var host = await ReportHost.Start(options =>
        {
            options.AllowAnonymousReports = true;
            options.AdminAuthorizationPolicy = "Administrators";
        });
        host.Client.DefaultRequestHeaders.Add("Cookie", "admin=true");
        using var response = await host.Client.DeleteAsync("/bdtest/reports/missing");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Anonymous_opt_in_serves_reports_but_does_not_enable_deletion()
    {
        await using var host = await ReportHost.Start(options => options.AllowAnonymousReports = true);
        using var page = await host.Client.GetAsync("/bdtest/report/test-runs");
        Assert.That(page.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var delete = await host.Client.DeleteAsync("/bdtest/reports/missing");
        Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Anonymous_endpoint_conventions_do_not_enable_deletion()
    {
        await using var host = await ReportHost.Start(options => options.ReportsAuthorizationPolicy = "Readers",
            allowAnonymousEndpoints: true);
        using var page = await host.Client.GetAsync("/bdtest/report/test-runs");
        Assert.That(page.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var delete = await host.Client.DeleteAsync("/bdtest/reports/missing");
        Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Configured_policy_takes_precedence_over_anonymous_opt_in()
    {
        await using var host = await ReportHost.Start(options =>
        {
            options.AllowAnonymousReports = true;
            options.ReportsAuthorizationPolicy = "Readers";
        });
        using var denied = await host.Client.GetAsync("/bdtest/report/test-runs");
        Assert.That(denied.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        host.Client.DefaultRequestHeaders.Add("X-Test-Role", "Reader");
        using var allowed = await host.Client.GetAsync("/bdtest/report/test-runs");
        Assert.That(allowed.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var raw = await host.Client.GetAsync("/bdtest/report/missing/raw-json-data");
        Assert.That(raw.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Explicit_policy_authentication_scheme_is_honored()
    {
        await using var host = await ReportHost.Start(options => options.ReportsAuthorizationPolicy = "SchemeReaders");
        host.Client.DefaultRequestHeaders.Add("X-Test-Role", "Reader");
        using var page = await host.Client.GetAsync("/bdtest/report/test-runs");
        Assert.That(page.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Unknown_policy_fails_closed_with_diagnostic()
    {
        await using var host = await ReportHost.Start(options => options.ReportsAuthorizationPolicy = "MissingPolicy");
        using var response = await host.Client.GetAsync("/bdtest/report/test-runs");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(host.Logs.Messages.Any(message => message.Contains("MissingPolicy")), Is.True);
    }

    [Test]
    public async Task Explicit_host_endpoint_policy_configures_report_access()
    {
        await using var host = await ReportHost.Start(requireEndpointPolicy: true);
        using var denied = await host.Client.GetAsync("/bdtest/report/test-runs");
        Assert.That(denied.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        host.Client.DefaultRequestHeaders.Add("X-Test-Role", "Reader");
        using var allowed = await host.Client.GetAsync("/bdtest/report/test-runs");
        Assert.That(allowed.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Host_endpoint_authorization_still_applies_when_anonymous_reports_are_enabled()
    {
        await using var host = await ReportHost.Start(options => options.AllowAnonymousReports = true, requireEndpointPolicy: true);
        using var response = await host.Client.GetAsync("/bdtest/report/test-runs");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Upload_key_is_independent_of_report_policy_and_ping_stays_public()
    {
        await using var host = await ReportHost.Start(options =>
        {
            options.ReportsAuthorizationPolicy = "Readers";
            options.UploadApiKey = "test-secret";
        }, requireEndpointPolicy: true);
        using var ping = await host.Client.GetAsync("/bdtest/ping");
        Assert.That(ping.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var missingKey = await host.Client.PostAsync("/bdtest/data", new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.That(missingKey.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        host.Client.DefaultRequestHeaders.Add("X-BDTest-Upload-Key", "test-secret");
        using var upload = await host.Client.PostAsync("/bdtest/data", new StringContent("{\"Scenarios\":[]}", Encoding.UTF8, "application/json"));
        Assert.That(upload.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        host.Client.DefaultRequestHeaders.Add("X-BDTest-Attachment-Token", new string('a', 64));
        using var attachment = await host.Client.PostAsync("/bdtest/data/" + new string('a', 32) + "/attachments/" + new string('b', 32), new ByteArrayContent([]));
        Assert.That(attachment.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        using var read = await host.Client.GetAsync("/bdtest/report/test-runs");
        Assert.That(read.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    private sealed class ReportHost(WebApplication app, HttpClient client, RecordingLogger logs) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;
        public RecordingLogger Logs { get; } = logs;
        public BDTestReportOptions Options => app.Services.GetRequiredService<IOptions<BDTestReportOptions>>().Value;

        public static async Task<ReportHost> Start(Action<BDTestReportOptions> configure = null, bool requireEndpointPolicy = false, bool useOptionsMiddleware = true, bool addAuthentication = true, string prefix = "/bdtest", bool allowAnonymousEndpoints = false)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            var logs = new RecordingLogger();
            builder.Logging.AddProvider(logs);
            if (addAuthentication)
                builder.Services.AddAuthentication("ReportTests")
                    .AddScheme<AuthenticationSchemeOptions, ReportTestAuthenticationHandler>("ReportTests", _ => { });
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("PublicReports", policy => policy.RequireAssertion(_ => true));
                options.AddPolicy("Administrators", policy => policy.RequireAuthenticatedUser().RequireRole("BDTest.Admin"));
                options.AddPolicy("Readers", policy => policy.RequireAuthenticatedUser().RequireRole("Reader"));
                options.AddPolicy("SchemeReaders", policy => policy.AddAuthenticationSchemes("ReportTests").RequireRole("Reader"));
            });
            builder.Services.AddBDTestReports(options => configure?.Invoke(options));
            var app = builder.Build();
            if (useOptionsMiddleware) app.UseBDTestReportServer();
            app.UseRouting();
            if (addAuthentication) app.UseAuthentication();
            app.UseAuthorization();
            var endpoints = app.MapBDTestReports(prefix);
            if (allowAnonymousEndpoints) endpoints.AllowAnonymous();
            if (requireEndpointPolicy)
            {
                endpoints.RequireAuthorization("Readers");
                endpoints.Add(endpoint =>
                {
                    var action = endpoint.Metadata.OfType<ControllerActionDescriptor>().SingleOrDefault();
                    if (action?.ControllerName == "BDTest" && action.ActionName is "Ping" or "Index" or "Error"
                        || action?.ControllerName == "Attachments" && action.ActionName == "Upload")
                        endpoint.Metadata.Add(new AllowAnonymousAttribute());
                });
            }
            await app.StartAsync();
            return new ReportHost(app, new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            {
                BaseAddress = new Uri(app.Urls.Single())
            }, logs);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
        }
    }

    private sealed class RecordingLogger : ILoggerProvider, ILogger
    {
        public System.Collections.Concurrent.ConcurrentBag<string> Messages { get; } = [];
        public ILogger CreateLogger(string categoryName) => this;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (IsEnabled(logLevel)) Messages.Add(formatter(state, exception));
        }
        public void Dispose() { }
    }
}

internal sealed class ReportTestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var role = Request.Headers["X-Test-Role"].ToString();
        if (role.Length == 0) return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "test-user"), new Claim(ClaimTypes.Role, role)], Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
