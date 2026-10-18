using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BDTest.Maps;
using BDTest.Razor.Reports.Extensions;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
public class ReportDeletionTests
{
    private const string ListPath = "/reports/bdtest/report/test-runs";
    private const string DeletePath = "/reports/bdtest/report/delete/run-618";
    private readonly RecordingRepository _repository = new();
    private WebApplication _application;
    private HttpClient _client;

    [OneTimeSetUp]
    public async Task StartReportServer()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddBDTestReports(options =>
        {
            options.AllowAnonymousReports = true;
            options.AdminAuthorizationPolicy = "DeleteTests";
        });
        builder.Services.AddSingleton<IDataRepository>(_repository);
        builder.Services.AddAuthentication("ReportTests")
            .AddScheme<AuthenticationSchemeOptions, ReportTestAuthenticationHandler>("ReportTests", _ => { })
            .AddScheme<AuthenticationSchemeOptions, AdminAuthenticationHandler>("AdminTests", _ => { });
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("DeleteTests", policy => policy.RequireAuthenticatedUser().RequireRole("BDTest.Admin"));
            options.AddPolicy("SeparateAdmin", policy => policy.AddAuthenticationSchemes("AdminTests")
                .RequireAuthenticatedUser().RequireRole("BDTest.Admin"));
        });
        _application = builder.Build();
        _application.UseBDTestReportServer();
        _application.UsePathBase("/reports");
        _application.UseRouting();
        _application.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers["X-Rendered-User"] = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                return Task.CompletedTask;
            });
            await next();
        });
        _application.MapControllers();
        await _application.StartAsync();
    }

    [SetUp]
    public void Reset()
    {
        _repository.DeletedIds.Clear();
        _application.Services.GetRequiredService<IOptions<BDTestReportOptions>>().Value.AdminAuthorizationPolicy = "DeleteTests";
        _client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = new Uri(_application.Urls.Single())
        };
        _client.DefaultRequestHeaders.Add("X-Test-Role", "BDTest.Admin");
    }

    [TearDown]
    public void DisposeClient() => _client.Dispose();

    [OneTimeTearDown]
    public async Task StopReportServer() => await _application.DisposeAsync();

    [TestCase("GET")]
    [TestCase("HEAD")]
    public async Task Safe_methods_cannot_delete_reports(string method)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), DeletePath);
        using var response = await _client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.MethodNotAllowed));
        Assert.That(_repository.DeletedIds, Is.Empty);
    }

    [TestCase("POST", null)]
    [TestCase("POST", "invalid")]
    [TestCase("DELETE", null)]
    [TestCase("DELETE", "invalid")]
    public async Task Missing_or_invalid_antiforgery_tokens_cannot_delete_reports(string method, string token)
    {
        // Fetch the page first so the client has the antiforgery cookie, as an admin browser would.
        using var page = await _client.GetAsync(ListPath);
        page.EnsureSuccessStatusCode();
        using var response = await DeleteAsync(method, DeletePath, token);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(_repository.DeletedIds, Is.Empty);
    }

    [TestCase("POST")]
    [TestCase("DELETE")]
    public async Task Valid_token_does_not_bypass_admin_authorization(string method)
    {
        var (action, token) = await ReadDeleteForm();
        _client.DefaultRequestHeaders.Remove("X-Test-Role");
        _client.DefaultRequestHeaders.Add("X-Test-Role", "Reader");
        using var response = await DeleteAsync(method, action, token);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(_repository.DeletedIds, Is.Empty);
    }

    [Test]
    public async Task Form_token_without_its_cookie_cannot_delete_reports()
    {
        var (action, token) = await ReadDeleteForm();
        using var clientWithoutCookie = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = _client.BaseAddress
        };
        clientWithoutCookie.DefaultRequestHeaders.Add("X-Test-Role", "BDTest.Admin");
        using var response = await clientWithoutCookie.PostAsync(action,
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(_repository.DeletedIds, Is.Empty);
    }

    [TestCase("POST")]
    [TestCase("DELETE")]
    public async Task Admin_with_valid_token_deletes_once_and_redirects_to_list(string method)
    {
        var (action, token) = await ReadDeleteForm();
        using var response = await DeleteAsync(method, action, token);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(response.Headers.Location?.OriginalString, Is.EqualTo(ListPath));
        Assert.That(_repository.DeletedIds, Is.EqualTo(new[] { "run-618" }));

        using var redirectedPage = await _client.GetAsync(response.Headers.Location);
        redirectedPage.EnsureSuccessStatusCode();
        Assert.That(_repository.DeletedIds, Is.EqualTo(new[] { "run-618" }),
            "Following the redirect must not repeat the deletion.");
    }

    [Test]
    public async Task Unknown_admin_policy_hides_delete_buttons_without_breaking_reads()
    {
        _application.Services.GetRequiredService<IOptions<BDTestReportOptions>>().Value.AdminAuthorizationPolicy = "MissingPolicy";
        using var response = await _client.GetAsync(ListPath);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Not.Contain("data-bdtest-action=\"delete\""));
        using var deletion = await _client.DeleteAsync(DeletePath);
        Assert.That(deletion.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(_repository.DeletedIds, Is.Empty);
    }

    [TestCase("POST")]
    [TestCase("DELETE")]
    public async Task Separate_admin_scheme_shows_working_delete_form_and_preserves_reader(string method)
    {
        _application.Services.GetRequiredService<IOptions<BDTestReportOptions>>().Value.AdminAuthorizationPolicy = "SeparateAdmin";
        _client.DefaultRequestHeaders.Remove("X-Test-Role");
        _client.DefaultRequestHeaders.Add("X-Test-Role", "Reader");
        _client.DefaultRequestHeaders.Add("X-Test-Admin", "true");
        using var page = await _client.GetAsync(ListPath);
        Assert.That(page.Headers.GetValues("X-Rendered-User"), Is.EqualTo(new[] { "test-user" }));
        var (action, token) = await ReadDeleteForm();
        using var response = await DeleteAsync(method, action, token);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(_repository.DeletedIds, Is.EqualTo(new[] { "run-618" }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Separate_admin_scheme_without_admin_identity_hides_delete_buttons(bool authenticateReader)
    {
        _application.Services.GetRequiredService<IOptions<BDTestReportOptions>>().Value.AdminAuthorizationPolicy = "SeparateAdmin";
        _client.DefaultRequestHeaders.Remove("X-Test-Role");
        if (authenticateReader) _client.DefaultRequestHeaders.Add("X-Test-Role", "BDTest.Admin");
        using var response = await _client.GetAsync(ListPath);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Not.Contain("data-bdtest-action=\"delete\""));
    }

    private async Task<(string Action, string Token)> ReadDeleteForm()
    {
        var html = await _client.GetStringAsync(ListPath);
        var match = Regex.Match(html, @"<form\b[^>]*/report/delete/[^>]*>.*?Delete</button>\s*</form>", RegexOptions.Singleline);
        Assert.That(match.Success, Is.True, "The admin delete button must submit an antiforgery-protected form.");
        var form = XElement.Parse(match.Value);
        Assert.That((string)form.Attribute("method"), Is.EqualTo("post"));
        Assert.That((string)form.Attribute("action"), Is.EqualTo(DeletePath));
        var token = form.Elements("input").Single(x => (string)x.Attribute("name") == "__RequestVerificationToken");
        Assert.That((string)token.Attribute("value"), Is.Not.Empty);
        return ((string)form.Attribute("action"), (string)token.Attribute("value"));
    }

    private async Task<HttpResponseMessage> DeleteAsync(string method, string action, string token)
    {
        var fields = new Dictionary<string, string>();
        if (token != null)
        {
            fields.Add("__RequestVerificationToken", token);
        }

        using var request = new HttpRequestMessage(new HttpMethod(method), action)
        {
            Content = new FormUrlEncodedContent(fields)
        };
        return await _client.SendAsync(request);
    }

    private sealed class RecordingRepository : IDataRepository
    {
        public List<string> DeletedIds { get; } = [];
        public Task DeleteReport(string id, CancellationToken cancellationToken = default)
        {
            DeletedIds.Add(id);
            return Task.CompletedTask;
        }

        public Task<BDTestOutputModel> GetDataForBulkRead(string id, CancellationToken cancellationToken) => GetData(id, cancellationToken);
        public async Task<Stream> OpenReadAsync(string id, CancellationToken cancellationToken)
        {
            var model = await GetData(id, cancellationToken);
            return model == null ? null : new MemoryStream(System.Text.Encoding.UTF8.GetBytes(BDTest.Helpers.BDTestJsonHelper.Serialize(model)));
        }
        public Task<BDTest.Razor.Reports.Storage.RunIndexPage> ListAsync(BDTest.Razor.Reports.Storage.RunIndexQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IEnumerable<TestRunSummary>> GetAllTestRunRecords(CancellationToken cancellationToken = default)
        {
            var record = ReportJson.DeserializeObject<TestRunSummary>("{}");
            record.RecordId = "run-618";
            return Task.FromResult<IEnumerable<TestRunSummary>>([record]);
        }

        public Task<BDTestOutputModel> GetData(string id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task StoreData(string id, BDTestOutputModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

internal sealed class AdminAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers["X-Test-Admin"] != "true") return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "admin-user"),
            new Claim(ClaimTypes.Role, "BDTest.Admin")], Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
