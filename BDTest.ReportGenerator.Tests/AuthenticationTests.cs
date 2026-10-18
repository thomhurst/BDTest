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
using NUnit.Framework;

namespace BDTest.ReportGenerator.Tests;

public class AuthenticationTests
{
    private const string ApiKey = "test-upload-key";
    private IHost _host = null!; // Initialized by SetUp before each test.
    private TestServer _server = null!; // Initialized by SetUp before each test.
    private HttpClient _client = null!; // Initialized by SetUp before each test.
    private TestDataStore _store = null!; // Initialized by SetUp before each test.

    [SetUp]
    public void SetUp()
    {
        _store = new TestDataStore();
        _host = CreateHost(ApiKey, _store);
        _server = _host.GetTestServer();
        _client = _server.CreateClient();
        _client.BaseAddress = new Uri("https://localhost");
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _host.Dispose();
    }

    [TestCase("/")]
    [TestCase("/bdtest/report/test-runs")]
    [TestCase("/bdtest/report/private-report")]
    [TestCase("/bdtest/report/private-report/summary")]
    [TestCase("/bdtest/report/private-report/raw-json-data")]
    [TestCase("/bdtest/report/trends")]
    [TestCase("/bdtest/report/delete/private-report")]
    public async Task AnonymousReadersAreChallenged(string path)
    {
        using var response = await _client.GetAsync(path);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect), await response.Content.ReadAsStringAsync());
        Assert.That(response.Headers.Location?.Host, Is.EqualTo("login.microsoftonline.com"));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Not.Contain("private-report"));
        Assert.That(_store.ReportReads, Is.Zero);
    }

    [TestCase("/bdtest/report/private-report/summary")]
    [TestCase("/bdtest/report/private-report/raw-json-data")]
    [TestCase("/bdtest/report/test-runs")]
    public async Task SignedInReadersCanReadReports(string path)
    {
        SignIn();

        using var response = await _client.GetAsync(path);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadAsStringAsync();
        Assert.That(body, Does.Contain(path.EndsWith("/test-runs") ? "BDTest" : "private-report"));
    }

    [TestCase(null)]
    [TestCase("application/json")]
    [TestCase("text/plain")]
    public async Task RawReportDownloadIsAJsonObjectRegardlessOfAcceptHeader(string? accept)
    {
        SignIn();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/bdtest/report/private-report/raw-json-data");
        if (accept != null) request.Headers.Accept.ParseAdd(accept);

        using var response = await _client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
        var json = JObject.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(json.Value<string>("id"), Is.EqualTo("private-report"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("wrong-key")]
    public async Task UploadRequiresCorrectKey(string? key)
    {
        if (key != null)
        {
            _client.DefaultRequestHeaders.Add("X-BDTest-Upload-Key", key);
        }

        using var response = await Upload();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(response.Headers.Location, Is.Null);
        Assert.That(_store.Uploads, Is.Zero);
    }

    [Test]
    public async Task AlternateUploadRouteCannotBypassKey()
    {
        using var response = await _client.PostAsync("/BDTEST/DATA/", UploadBody());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(_store.Uploads, Is.Zero);
    }

    [Test]
    public async Task SignedInReaderCannotBypassUploadKey()
    {
        SignIn();

        using var response = await Upload();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(_store.Uploads, Is.Zero);
    }

    [TestCase("/bdtest/data")]
    [TestCase("/BDTEST/DATA/")]
    public async Task UploadKeyAllowsMachineUploadWithoutSignIn(string path)
    {
        _client.DefaultRequestHeaders.Add("X-BDTest-Upload-Key", ApiKey);

        using var response = await _client.PostAsync(path, UploadBody());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Match(@"/bdtest/report/[0-9a-f]{32}/summary$"));
        Assert.That(_store.Uploads, Is.EqualTo(1));
    }

    [Test]
    public async Task UploadKeyDoesNotGrantReportReadAccess()
    {
        _client.DefaultRequestHeaders.Add("X-BDTest-Upload-Key", ApiKey);

        using var response = await _client.GetAsync("/bdtest/report/private-report/raw-json-data");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect), await response.Content.ReadAsStringAsync());
        Assert.That(_store.ReportReads, Is.Zero);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public async Task MissingServerKeyDisablesUploads(string configuredKey)
    {
        using var host = CreateHost(configuredKey, _store);
        using var client = host.GetTestClient();
        client.BaseAddress = new Uri("https://localhost");
        client.DefaultRequestHeaders.Add("X-BDTest-Upload-Key", ApiKey);

        using var response = await client.PostAsync("/bdtest/data", UploadBody());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(_store.Uploads, Is.Zero);
    }

    [Test]
    public async Task MultipleKeysAreRejected()
    {
        _client.DefaultRequestHeaders.Add("X-BDTest-Upload-Key", new[] { ApiKey, ApiKey });

        using var response = await Upload();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(_store.Uploads, Is.Zero);
    }

    [Test]
    public async Task PingRemainsAnonymous()
    {
        using var response = await _client.GetAsync("/bdtest/ping");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
    }

    [TestCase("Alex Morgan")]
    [TestCase("Jordan Lee")]
    public async Task ReportPersonalizationUsesTheSignedInReader(string name)
    {
        SignIn(name);

        using var response = await _client.GetAsync("/bdtest/report/private-report/summary");
        var body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), body);
        Assert.That(body, Does.Contain($"<li class=\"menu-head\">{name}</li>"));
        Assert.That(body, Does.Not.Contain("fbcdn.net"));
        Assert.That(body, Does.Not.Contain("Tom Longhurst"));
        Assert.That(body, Does.Not.Contain("Hi, Tom!"));
        Assert.That(body, Does.Not.Contain("navbarCustomLinksDropdown"));
    }

    [Test]
    public async Task Unexpected_host_is_rejected_by_the_sample_host_filter()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/bdtest/ping");
        request.Headers.Host = "attacker.example";
        using var response = await _client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [TestCase("127.0.0.1")]
    [TestCase("192.0.2.10")]
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

        Assert.That(context.Response.StatusCode, Is.EqualTo(302));
        var location = new Uri(context.Response.Headers.Location.ToString());
        Assert.That(location.Host, Is.EqualTo("login.microsoftonline.com"));
        Assert.That(HttpUtility.ParseQueryString(location.Query)["redirect_uri"],
            Is.EqualTo("https://localhost:8443/reports/signin-oidc"));
    }

    [TestCase("")]
    [TestCase("/team")]
    [TestCase("/team/")]
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

        Assert.That(context.Response.StatusCode, Is.EqualTo(302));
        Assert.That(context.Response.Headers.Location.ToString(),
            Is.EqualTo(prefix.TrimEnd('/') + "/bdtest/report/test-runs"));
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
        Assert.That(location.Host, Is.EqualTo("login.microsoftonline.com"));
        Assert.That(HttpUtility.ParseQueryString(location.Query)["redirect_uri"], Is.EqualTo("https://localhost/signin-oidc"));
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
        Assert.That(HttpUtility.ParseQueryString(location.Query)["redirect_uri"], Is.EqualTo("https://localhost/signin-oidc"));
    }

    [TestCase(false)]
    [TestCase(true)]
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

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError), body);
        Assert.That(response.Headers.Location, Is.Null);
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/html"));
        Assert.That(body, Does.Contain("Something went wrong on the server"));
        Assert.That(body, Does.Match(@"Reference [^<\s]+</span>"));
        Assert.That(body, Does.Not.Contain("Test storage failure"));
        Assert.That(response.Headers.CacheControl?.NoStore, Is.True);
    }

    [Test]
    public async Task ErrorPageRemainsAnonymous()
    {
        using var response = await _client.GetAsync("/bdtest/error");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadAsStringAsync(),
            Does.Contain("Something went wrong on the server"));
    }

    [Test]
    public async Task Identity_sign_in_route_challenges_with_OpenIdConnect()
    {
        SignIn();
        using var response = await _client.GetAsync("/MicrosoftIdentity/Account/SignIn");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(response.Headers.Location?.Host, Is.EqualTo("login.microsoftonline.com"));
    }

    [TestCase("css/bdtest-report.css", "text/css")]
    [TestCase("js/BDTest.js", "text/javascript")]
    [TestCase("favicon.ico", "image/x-icon")]
    public async Task Report_static_assets_are_anonymous_and_cached(string path, string contentType)
    {
        using var response = await _client.GetAsync($"/bdtest/assets/{path}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo(contentType));
        Assert.That(await response.Content.ReadAsByteArrayAsync(), Is.Not.Empty);
        Assert.That(response.Headers.CacheControl?.Public, Is.True);
        Assert.That(response.Headers.CacheControl?.MaxAge, Is.EqualTo(TimeSpan.FromDays(1)));
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
