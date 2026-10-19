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
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[NotInParallel]
[Arguments("/bdtest")]
[Arguments("/team/reports")]
public class ReportDeletionTests(string prefix)
{
    private string ListPath => "/reports" + prefix + "/report/test-runs";
    private string DeletePath => "/reports" + prefix + "/reports/run-618";
    private readonly RecordingRepository _repository = new();
    // Initialized by the asynchronous setup hook before each test.
    private WebApplication _application = null!;
    private HttpClient _client = null!;

    [Before(HookType.Test)]
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
        _application.MapBDTestReports(prefix);
        await _application.StartAsync();
    }

    [Before(HookType.Test)]
    public void Reset()
    {
        _repository.DeletedIds.Clear();
        _application.Services.GetRequiredService<IOptions<BDTestReportOptions>>().Value.AdminAuthorizationPolicy = "DeleteTests";
        _repository.Failure = null;
        _client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = new Uri(_application.Urls.Single())
        };
        _client.DefaultRequestHeaders.Add("X-Test-Role", "BDTest.Admin");
    }

    [After(HookType.Test)]
    public void DisposeClient() => _client.Dispose();

    [After(HookType.Test)]
    public async Task StopReportServer() => await _application.DisposeAsync();

    [Test]
    [Arguments("GET")]
    [Arguments("HEAD")]
    [Arguments("POST")]
    [Arguments("PUT")]
    [Arguments("PATCH")]
    [Arguments("OPTIONS")]
    public async Task Other_methods_cannot_delete_reports(string method)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), DeletePath);
        using var response = await _client.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.MethodNotAllowed);
        await Assert.That(_repository.DeletedIds).IsEmpty();
    }

    [Test]
    [Arguments("DELETE", null)]
    [Arguments("DELETE", "invalid")]
    public async Task Missing_or_invalid_antiforgery_tokens_cannot_delete_reports(string method, string? token)
    {
        // Fetch the page first so the client has the antiforgery cookie, as an admin browser would.
        using var page = await _client.GetAsync(ListPath);
        page.EnsureSuccessStatusCode();
        using var response = await DeleteAsync(method, DeletePath, token!);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(_repository.DeletedIds).IsEmpty();
    }

    [Test]
    public async Task Valid_token_does_not_bypass_admin_authorization()
    {
        var (action, token) = await ReadDeleteForm();
        _client.DefaultRequestHeaders.Remove("X-Test-Role");
        _client.DefaultRequestHeaders.Add("X-Test-Role", "Reader");
        using var response = await DeleteAsync("DELETE", action, token);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(_repository.DeletedIds).IsEmpty();
    }

    [Test]
    public async Task Header_token_without_its_cookie_cannot_delete_reports()
    {
        var (action, token) = await ReadDeleteForm();
        using var clientWithoutCookie = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = _client.BaseAddress
        };
        clientWithoutCookie.DefaultRequestHeaders.Add("X-Test-Role", "BDTest.Admin");
        using var request = new HttpRequestMessage(HttpMethod.Delete, action);
        request.Headers.Add("RequestVerificationToken", token);
        using var response = await clientWithoutCookie.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(_repository.DeletedIds).IsEmpty();
    }

    [Test]
    public async Task Admin_with_valid_header_token_gets_204_on_repeated_deletes()
    {
        var (action, token) = await ReadDeleteForm();
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var response = await DeleteAsync("DELETE", action, token);
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
            await Assert.That(response.Headers.Location).IsNull();
            await Assert.That(await response.Content.ReadAsStringAsync()).IsEmpty();
        }
        await Assert.That(_repository.DeletedIds).IsEquivalentTo(new[] { "run-618", "run-618" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    [Arguments("POST")]
    [Arguments("DELETE")]
    public async Task Legacy_delete_route_is_removed(string method)
    {
        var (_, token) = await ReadDeleteForm();
        using var response = await DeleteAsync(method, "/reports" + prefix + "/report/delete/run-618", token);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(_repository.DeletedIds).IsEmpty();
    }

    [Test]
    [Arguments("bad id")]
    [Arguments("bad\\id")]
    [Arguments("café")]
    [Arguments("bad<id>")]
    public async Task Invalid_id_returns_400_without_touching_storage(string id)
    {
        var (_, token) = await ReadDeleteForm();
        using var response = await DeleteAsync("DELETE", "/reports" + prefix + "/reports/" + Uri.EscapeDataString(id), token);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(_repository.DeletedIds).IsEmpty();
    }

    [Test]
    public async Task Storage_failure_returns_500_without_redirecting()
    {
        var (action, token) = await ReadDeleteForm();
        _repository.Failure = new IOException("storage unavailable");
        using var response = await DeleteAsync("DELETE", action, token);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.InternalServerError);
        await Assert.That(response.Headers.Location).IsNull();
    }

    [Test]
    public async Task Unknown_admin_policy_hides_delete_buttons_without_breaking_reads()
    {
        _application.Services.GetRequiredService<IOptions<BDTestReportOptions>>().Value.AdminAuthorizationPolicy = "MissingPolicy";
        using var response = await _client.GetAsync(ListPath);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(await response.Content.ReadAsStringAsync()).DoesNotContain("data-bdtest-action=\"delete\"");
        using var deletion = await _client.DeleteAsync(DeletePath);
        await Assert.That(deletion.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(_repository.DeletedIds).IsEmpty();
    }

    [Test]
    public async Task Separate_admin_scheme_shows_working_delete_form_and_preserves_reader()
    {
        _application.Services.GetRequiredService<IOptions<BDTestReportOptions>>().Value.AdminAuthorizationPolicy = "SeparateAdmin";
        _client.DefaultRequestHeaders.Remove("X-Test-Role");
        _client.DefaultRequestHeaders.Add("X-Test-Role", "Reader");
        _client.DefaultRequestHeaders.Add("X-Test-Admin", "true");
        using var page = await _client.GetAsync(ListPath);
        await Assert.That(page.Headers.GetValues("X-Rendered-User")).IsEquivalentTo(new[] { "test-user" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        var (action, token) = await ReadDeleteForm();
        using var response = await DeleteAsync("DELETE", action, token);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(_repository.DeletedIds).IsEquivalentTo(new[] { "run-618" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Separate_admin_scheme_without_admin_identity_hides_delete_buttons(bool authenticateReader)
    {
        _application.Services.GetRequiredService<IOptions<BDTestReportOptions>>().Value.AdminAuthorizationPolicy = "SeparateAdmin";
        _client.DefaultRequestHeaders.Remove("X-Test-Role");
        if (authenticateReader) _client.DefaultRequestHeaders.Add("X-Test-Role", "BDTest.Admin");
        using var response = await _client.GetAsync(ListPath);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(await response.Content.ReadAsStringAsync()).DoesNotContain("data-bdtest-action=\"delete\"");
    }

    private async Task<(string Action, string Token)> ReadDeleteForm()
    {
        var html = await _client.GetStringAsync(ListPath);
        var match = Regex.Match(html, @"<form\b[^>]*data-bdtest-delete[^>]*>.*?Delete</button>\s*</form>", RegexOptions.Singleline);
        await Assert.That(match.Success).IsTrue().Because("The admin delete button must expose an antiforgery-protected DELETE action.");
        var form = XElement.Parse(match.Value);
        await Assert.That((string?)form.Attribute("method")).IsEqualTo("post");
        await Assert.That((string?)form.Attribute("action")).IsEqualTo(DeletePath);
        var token = form.Elements("input").Single(x => (string?)x.Attribute("name") == "__RequestVerificationToken");
        await Assert.That((string?)token.Attribute("value")).IsNotEmpty();
        return (Required((string?)form.Attribute("action")), Required((string?)token.Attribute("value")));
    }

    private async Task<HttpResponseMessage> DeleteAsync(string method, string action, string token)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), action);
        if (token != null) request.Headers.Add("RequestVerificationToken", token);
        return await _client.SendAsync(request);
    }

    private sealed class RecordingRepository : IDataRepository
    {
        public List<string> DeletedIds { get; } = [];
        public Exception? Failure { get; set; }
        public Task DeleteReport(string id, CancellationToken cancellationToken = default)
        {
            if (Failure != null) throw Failure;
            DeletedIds.Add(id);
            return Task.CompletedTask;
        }

        public Task<BDTest.Contracts.BDTestReport?> GetDataForBulkRead(string id, CancellationToken cancellationToken) => GetData(id, cancellationToken);
        public async Task<Stream?> OpenReadAsync(string id, CancellationToken cancellationToken)
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

        public Task<BDTest.Contracts.BDTestReport?> GetData(string id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task StoreData(string id, BDTest.Contracts.BDTestReport model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
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
