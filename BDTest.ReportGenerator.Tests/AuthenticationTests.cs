using BDTest.Razor.Reports.Storage;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Web;
using Microsoft.AspNetCore.Http;
using BDTest.Maps;
using BDTest.Output;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;
using BDTest.ReportGenerator.RazorServer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Protocols;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BDTest.ReportGenerator.Tests;

public class AuthenticationTests
{
    private const string ApiKey = "test-upload-key";
    private IHost _host = null!; // Initialized by SetUp before each test.
    private TestServer _server = null!; // Initialized by SetUp before each test.
    private HttpClient _client = null!; // Initialized by SetUp before each test.
    private TestDataStore _store = null!; // Initialized by SetUp before each test.

    [Before(HookType.Test)]
    public void SetUp()
    {
        _store = new TestDataStore();
        _host = CreateHost(ApiKey, _store);
        _server = _host.GetTestServer();
        _client = _server.CreateClient();
        _client.BaseAddress = new Uri("https://localhost");
    }

    [After(HookType.Test)]
    public void TearDown()
    {
        _client.Dispose();
        _host.Dispose();
    }

    [Test]
    [Arguments("/")]
    [Arguments("/bdtest/report/test-runs")]
    [Arguments("/bdtest/report/private-report")]
    [Arguments("/bdtest/report/private-report/summary")]
    [Arguments("/bdtest/report/private-report/raw-json-data")]
    [Arguments("/bdtest/report/trends")]
    [Arguments("/bdtest/report/delete/private-report")]
    public async Task AnonymousReadersAreChallenged(string path)
    {
        using var response = await _client.GetAsync(path);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect).Because(await response.Content.ReadAsStringAsync());
        await Assert.That(response.Headers.Location?.Host).IsEqualTo("login.microsoftonline.com");
        await Assert.That(await response.Content.ReadAsStringAsync()).DoesNotContain("private-report");
        await Assert.That(_store.ReportReads).IsZero();
    }

    [Test]
    [Arguments("/bdtest/report/private-report/summary")]
    [Arguments("/bdtest/report/private-report/raw-json-data")]
    [Arguments("/bdtest/report/test-runs")]
    public async Task SignedInReadersCanReadReports(string path)
    {
        SignIn();

        using var response = await _client.GetAsync(path);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK).Because(await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(body).Contains(path.EndsWith("/test-runs") ? "BDTest" : "private-report");
    }

    [Test]
    [Arguments(null)]
    [Arguments("application/json")]
    [Arguments("text/plain")]
    public async Task RawReportDownloadIsAJsonObjectRegardlessOfAcceptHeader(string? accept)
    {
        SignIn();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/bdtest/report/private-report/raw-json-data");
        if (accept != null) request.Headers.Accept.ParseAdd(accept);

        using var response = await _client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("application/json");
        var json = JObject.Parse(await response.Content.ReadAsStringAsync());
        await Assert.That(json.Value<string>("id")).IsEqualTo("private-report");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("wrong-key")]
    public async Task UploadRequiresCorrectKey(string? key)
    {
        if (key != null)
        {
            _client.DefaultRequestHeaders.Add("X-BDTest-Upload-Key", key);
        }

        using var response = await Upload();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(response.Headers.Location).IsNull();
        await Assert.That(_store.Uploads).IsZero();
    }

    [Test]
    public async Task AlternateUploadRouteCannotBypassKey()
    {
        using var response = await _client.PostAsync("/BDTEST/DATA/", UploadBody());

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(_store.Uploads).IsZero();
    }

    [Test]
    public async Task SignedInReaderCannotBypassUploadKey()
    {
        SignIn();

        using var response = await Upload();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(_store.Uploads).IsZero();
    }

    [Test]
    [Arguments("/bdtest/data")]
    [Arguments("/BDTEST/DATA/")]
    public async Task UploadKeyAllowsMachineUploadWithoutSignIn(string path)
    {
        _client.DefaultRequestHeaders.Add("X-BDTest-Upload-Key", ApiKey);

        using var response = await _client.PostAsync(path, UploadBody());

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK).Because(await response.Content.ReadAsStringAsync());
        await Assert.That(await response.Content.ReadAsStringAsync()).Matches(@"/bdtest/report/[0-9a-f]{32}/summary$");
        await Assert.That(_store.Uploads).IsEqualTo(1);
    }

    [Test]
    public async Task UploadKeyDoesNotGrantReportReadAccess()
    {
        _client.DefaultRequestHeaders.Add("X-BDTest-Upload-Key", ApiKey);

        using var response = await _client.GetAsync("/bdtest/report/private-report/raw-json-data");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect).Because(await response.Content.ReadAsStringAsync());
        await Assert.That(_store.ReportReads).IsZero();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    public async Task MissingServerKeyDisablesUploads(string? configuredKey)
    {
        using var host = CreateHost(configuredKey, _store);
        using var client = host.GetTestClient();
        client.BaseAddress = new Uri("https://localhost");
        client.DefaultRequestHeaders.Add("X-BDTest-Upload-Key", ApiKey);

        using var response = await client.PostAsync("/bdtest/data", UploadBody());

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(_store.Uploads).IsZero();
    }

    [Test]
    public async Task MultipleKeysAreRejected()
    {
        _client.DefaultRequestHeaders.Add("X-BDTest-Upload-Key", new[] { ApiKey, ApiKey });

        using var response = await Upload();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(_store.Uploads).IsZero();
    }

    [Test]
    public async Task PingRemainsAnonymous()
    {
        using var response = await _client.GetAsync("/bdtest/ping");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK).Because(await response.Content.ReadAsStringAsync());
    }

    [Test]
    [Arguments("Alex Morgan")]
    [Arguments("Jordan Lee")]
    public async Task ReportPersonalizationUsesTheSignedInReader(string name)
    {
        SignIn(name);

        using var response = await _client.GetAsync("/bdtest/report/private-report/summary");
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK).Because(body);
        await Assert.That(body).Contains($"<li class=\"menu-head\">{name}</li>");
        await Assert.That(body).DoesNotContain("fbcdn.net");
        await Assert.That(body).DoesNotContain("Tom Longhurst");
        await Assert.That(body).DoesNotContain("Hi, Tom!");
        await Assert.That(body).DoesNotContain("navbarCustomLinksDropdown");
    }

    [Test]
    public async Task Unexpected_host_is_rejected_by_the_sample_host_filter()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/bdtest/ping");
        request.Headers.Host = "attacker.example";
        using var response = await _client.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    [Arguments("127.0.0.1")]
    [Arguments("192.0.2.10")]
    public async Task Trusted_proxy_applies_https_and_path_prefix_before_authentication(string proxy)
    {
        var context = await _server.SendAsync(context =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(proxy);
            context.Request.Scheme = "http";
            context.Request.Host = new HostString("localhost");
            context.Request.Path = "/bdtest/report/test-runs";
            context.Request.Headers["X-Forwarded-For"] = "198.51.100.20";
            context.Request.Headers["X-Forwarded-Proto"] = "https";
            context.Request.Headers["X-Forwarded-Host"] = "localhost:8443";
            context.Request.Headers["X-Forwarded-Prefix"] = "/reports";
        });

        await Assert.That(context.Response.StatusCode).IsEqualTo(302);
        var location = new Uri(context.Response.Headers.Location.ToString());
        await Assert.That(location.Host).IsEqualTo("login.microsoftonline.com");
        await Assert.That(HttpUtility.ParseQueryString(location.Query)["redirect_uri"]).IsEqualTo("https://localhost:8443/reports/signin-oidc");
    }

    [Test]
    [Arguments("")]
    [Arguments("/team")]
    [Arguments("/team/")]
    public async Task Root_redirect_preserves_proxy_prefix(string prefix)
    {
        SignIn();
        var context = await _server.SendAsync(context =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Loopback;
            context.Request.Scheme = "https";
            context.Request.Host = new HostString("localhost");
            context.Request.Path = "/";
            context.Request.Headers.Cookie = _client.DefaultRequestHeaders.GetValues("Cookie").Single();
            context.Request.Headers["X-Forwarded-Prefix"] = prefix.TrimEnd('/');
        });

        await Assert.That(context.Response.StatusCode).IsEqualTo(302);
        await Assert.That(context.Response.Headers.Location.ToString()).IsEqualTo(prefix.TrimEnd('/') + "/bdtest/report/test-runs");
    }

    [Test]
    public async Task Untrusted_proxy_cannot_override_the_origin_or_path_prefix()
    {
        var context = await _server.SendAsync(context =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.30");
            context.Request.Scheme = "https";
            context.Request.Host = new HostString("localhost");
            context.Request.Path = "/bdtest/report/test-runs";
            context.Request.Headers["X-Forwarded-Proto"] = "http";
            context.Request.Headers["X-Forwarded-Host"] = "attacker.example";
            context.Request.Headers["X-Forwarded-Prefix"] = "/evil";
        });
        var location = new Uri(context.Response.Headers.Location.ToString());
        await Assert.That(location.Host).IsEqualTo("login.microsoftonline.com");
        await Assert.That(HttpUtility.ParseQueryString(location.Query)["redirect_uri"]).IsEqualTo("https://localhost/signin-oidc");
    }

    [Test]
    public async Task Trusted_proxy_cannot_supply_a_host_outside_AllowedHosts()
    {
        var context = await _server.SendAsync(context =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Loopback;
            context.Request.Scheme = "https";
            context.Request.Host = new HostString("localhost");
            context.Request.Path = "/bdtest/report/test-runs";
            context.Request.Headers["X-Forwarded-Host"] = "attacker.example";
        });
        var location = new Uri(context.Response.Headers.Location.ToString());
        await Assert.That(HttpUtility.ParseQueryString(location.Query)["redirect_uri"]).IsEqualTo("https://localhost/signin-oidc");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ProductionErrorsRenderTheErrorPage(bool upload)
    {
        using var host = CreateHost(ApiKey, new TestDataStore { FailRequests = true }, Environments.Production);
        using var client = host.GetTestClient();
        client.BaseAddress = new Uri("https://localhost");
        if (upload)
        {
            client.DefaultRequestHeaders.Add("X-BDTest-Upload-Key", ApiKey);
        }
        else
        {
            SignIn(host.GetTestServer(), client, "Reader");
        }

        using var response = upload
            ? await client.PostAsync("/bdtest/data", UploadBody())
            : await client.GetAsync("/bdtest/report/failing-report/summary");
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.InternalServerError).Because(body);
        await Assert.That(response.Headers.Location).IsNull();
        await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("text/html");
        await Assert.That(body).Contains("Something went wrong on the server");
        await Assert.That(body).Matches(@"Reference [^<\s]+</span>");
        await Assert.That(body).DoesNotContain("Test storage failure");
        await Assert.That(response.Headers.CacheControl?.NoStore).IsTrue();
    }

    [Test]
    public async Task ErrorPageRemainsAnonymous()
    {
        using var response = await _client.GetAsync("/bdtest/error");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains("Something went wrong on the server");
    }

    [Test]
    public async Task Identity_sign_in_route_challenges_with_OpenIdConnect()
    {
        SignIn();
        using var response = await _client.GetAsync("/MicrosoftIdentity/Account/SignIn");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
        await Assert.That(response.Headers.Location?.Host).IsEqualTo("login.microsoftonline.com");
    }

    [Test]
    [Arguments("css/bdtest-report.css", "text/css")]
    [Arguments("js/BDTest.js", "text/javascript")]
    [Arguments("favicon.ico", "image/x-icon")]
    public async Task Report_static_assets_are_anonymous_and_cached(string path, string contentType)
    {
        using var response = await _client.GetAsync($"/bdtest/assets/{path}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo(contentType);
        await Assert.That(await response.Content.ReadAsByteArrayAsync()).IsNotEmpty();
        await Assert.That(response.Headers.CacheControl?.Public).IsTrue();
        await Assert.That(response.Headers.CacheControl?.MaxAge).IsEqualTo(TimeSpan.FromDays(1));
    }

    private Task<HttpResponseMessage> Upload() => _client.PostAsync("/bdtest/data", UploadBody());

    private static StringContent UploadBody() => new(
        ReportJson.SerializeObject(new BDTestOutputModel
        {
            Id = "uploaded-report",
            TestTimer = new TestTimer()
        }), Encoding.UTF8, "application/json");

    private void SignIn(string name = "Reader") => SignIn(_server, _client, name);

    private static void SignIn(TestServer server, HttpClient client, string name)
    {
        var options = server.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "reader"), new Claim(ClaimTypes.Name, name) },
            CookieAuthenticationDefaults.AuthenticationScheme));
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties
        {
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1),
            Items = { [".AuthScheme"] = OpenIdConnectDefaults.AuthenticationScheme }
        }, CookieAuthenticationDefaults.AuthenticationScheme);

        client.DefaultRequestHeaders.Add("Cookie", $"{options.Cookie.Name}={options.TicketDataFormat.Protect(ticket)}");
    }

    private static IHost CreateHost(string? apiKey, TestDataStore store, string environment = "Development")
    {
        var builder = Program.CreateBuilder(["--environment", environment]);
        builder.WebHost.UseTestServer();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "sample-appsettings.json"))
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureAd:Instance"] = "https://login.microsoftonline.com/",
                ["AzureAd:TenantId"] = "11111111-1111-1111-1111-111111111111",
                ["AzureAd:ClientId"] = "22222222-2222-2222-2222-222222222222",
                ["AzureAd:ClientSecret"] = "test-only-secret",
                ["AzureStorage:ConnectionString"] = "UseDevelopmentStorage=true",
                ["BDTest:UploadApiKey"] = apiKey,
                ["ReverseProxy:KnownProxies:0"] = "192.0.2.10"
            });
        builder.Services.RemoveAll<IReportStore>();
        builder.Services.RemoveAll<IRunIndexStore>();
        builder.Services.AddSingleton<IBDTestDataStore>(store);
        // Exercise the real cookie/OIDC handlers without contacting Entra ID.
        builder.Services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
        {
            options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(new OpenIdConnectConfiguration
            {
                Issuer = "https://login.microsoftonline.com/test/v2.0",
                AuthorizationEndpoint = "https://login.microsoftonline.com/test/oauth2/v2.0/authorize"
            });
        });
        var app = Program.BuildApplication(builder);
        app.Start();
        return app;
    }

    private sealed class TestDataStore : IBDTestDataStore
    {
        public bool FailRequests { get; init; }
        public int ReportReads { get; private set; }
        public int Uploads { get; private set; }

        public Task<BDTestOutputModel?> GetTestData(string id, CancellationToken cancellationToken)
        {
            ReportReads++;
            if (FailRequests)
            {
                throw new InvalidOperationException("Test storage failure");
            }

            return Task.FromResult<BDTestOutputModel?>(new BDTestOutputModel { Id = id, TestTimer = new TestTimer() });
        }

        public Task StoreTestData(string id, BDTestOutputModel? data)
        {
            Uploads++;
            if (FailRequests)
            {
                throw new InvalidOperationException("Test storage failure");
            }

            return Task.CompletedTask;
        }

        public Task<IEnumerable<TestRunSummary>> GetAllTestRunRecords() =>
            Task.FromResult(Enumerable.Empty<TestRunSummary>());

        public Task StoreTestRunRecord(TestRunSummary summary) => Task.CompletedTask;
        public Task DeleteTestData(string id) => Task.CompletedTask;
        public Task DeleteTestRunRecord(string id) => Task.CompletedTask;
    }
}
