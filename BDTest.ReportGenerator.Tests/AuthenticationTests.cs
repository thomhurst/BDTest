using System.Net;
using System.Security.Claims;
using System.Text;
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
using NUnit.Framework;

namespace BDTest.ReportGenerator.Tests;

public class AuthenticationTests
{
    private const string ApiKey = "test-upload-key";
    private IHost _host;
    private TestServer _server;
    private HttpClient _client;
    private TestDataStore _store;

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
    [TestCase("")]
    [TestCase("wrong-key")]
    public async Task UploadRequiresCorrectKey(string key)
    {
        if (key != null)
        {
            _client.DefaultRequestHeaders.Add("X-BDTest-Api-Key", key);
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
        _client.DefaultRequestHeaders.Add("X-BDTest-Api-Key", ApiKey);

        using var response = await _client.PostAsync(path, UploadBody());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("/bdtest/report/uploaded-report/summary"));
        Assert.That(_store.Uploads, Is.EqualTo(1));
    }

    [Test]
    public async Task UploadKeyDoesNotGrantReportReadAccess()
    {
        _client.DefaultRequestHeaders.Add("X-BDTest-Api-Key", ApiKey);

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
        client.DefaultRequestHeaders.Add("X-BDTest-Api-Key", ApiKey);

        using var response = await client.PostAsync("/bdtest/data", UploadBody());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(_store.Uploads, Is.Zero);
    }

    [Test]
    public async Task MultipleKeysAreRejected()
    {
        _client.DefaultRequestHeaders.Add("X-BDTest-Api-Key", new[] { ApiKey, ApiKey });

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

    private Task<HttpResponseMessage> Upload() => _client.PostAsync("/bdtest/data", UploadBody());

    private static StringContent UploadBody() => new(
        JsonConvert.SerializeObject(new BDTestOutputModel
        {
            Id = "uploaded-report",
            TestTimer = new TestTimer()
        }), Encoding.UTF8, "application/json");

    private void SignIn()
    {
        var options = _server.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "reader"), new Claim(ClaimTypes.Name, "Reader") },
            CookieAuthenticationDefaults.AuthenticationScheme));
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties
        {
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1),
            Items = { [".AuthScheme"] = OpenIdConnectDefaults.AuthenticationScheme }
        }, CookieAuthenticationDefaults.AuthenticationScheme);

        _client.DefaultRequestHeaders.Add("Cookie", $"{options.Cookie.Name}={options.TicketDataFormat.Protect(ticket)}");
    }

    private static IHost CreateHost(string apiKey, TestDataStore store)
    {
        return new HostBuilder()
            .ConfigureWebHost(web => web.UseTestServer()
                .UseEnvironment("Development")
                .UseSetting(WebHostDefaults.ApplicationKey, typeof(Startup).Assembly.GetName().Name)
                .ConfigureAppConfiguration(builder => builder.AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["AzureAd:Instance"] = "https://login.microsoftonline.com/",
                    ["AzureAd:TenantId"] = "11111111-1111-1111-1111-111111111111",
                    ["AzureAd:ClientId"] = "22222222-2222-2222-2222-222222222222",
                    ["AzureAd:ClientSecret"] = "test-only-secret",
                    ["AzureStorage:ConnectionString"] = "unused",
                    ["ReportUpload:ApiKey"] = apiKey
                }))
                .UseStartup<Startup>()
                .ConfigureTestServices(services =>
                {
                    services.AddSingleton<IBDTestDataStore>(store);
                    // Exercise the real cookie/OIDC handlers without contacting Entra ID.
                    services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
                    {
                        options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(new OpenIdConnectConfiguration
                        {
                            Issuer = "https://login.microsoftonline.com/test/v2.0",
                            AuthorizationEndpoint = "https://login.microsoftonline.com/test/oauth2/v2.0/authorize"
                        });
                    });
                }))
            .Start();
    }

    private sealed class TestDataStore : IBDTestDataStore
    {
        public int ReportReads { get; private set; }
        public int Uploads { get; private set; }

        public Task<BDTestOutputModel> GetTestData(string id, CancellationToken cancellationToken)
        {
            ReportReads++;
            return Task.FromResult(new BDTestOutputModel { Id = id, TestTimer = new TestTimer() });
        }

        public Task StoreTestData(string id, BDTestOutputModel data)
        {
            Uploads++;
            return Task.CompletedTask;
        }

        public Task<IEnumerable<TestRunSummary>> GetAllTestRunRecords() =>
            Task.FromResult(Enumerable.Empty<TestRunSummary>());

        public Task StoreTestRunRecord(TestRunSummary summary) => Task.CompletedTask;
        public Task DeleteTestData(string id) => Task.CompletedTask;
        public Task DeleteTestRunRecord(string id) => Task.CompletedTask;
    }
}
