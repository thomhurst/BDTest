using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BDTest.Maps;
using BDTest.Razor.Reports.Extensions;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
public class ReportDeletionTests
{
    private const string ListPath = "/reports/bdtest/report/test-runs";
    private const string DeletePath = "/reports/bdtest/report/delete/run-618";
    private readonly RecordingRepository _repository = new();
    private readonly AdminAuthorizer _authorizer = new();
    private WebApplication _application;
    private HttpClient _client;

    [OneTimeSetUp]
    public async Task StartReportServer()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddControllersWithViews().AddBdTestReportControllersAndViews();
        builder.Services.AddSingleton<IDataRepository>(_repository);
        _application = builder.Build();
        _application.Services.GetRequiredService<BDTestReportServerOptions>().AdminAuthorizer = _authorizer;
        _application.UsePathBase("/reports");
        _application.UseRouting();
        _application.MapControllers();
        await _application.StartAsync();
    }

    [SetUp]
    public void Reset()
    {
        _repository.DeletedIds.Clear();
        _authorizer.IsAdmin = true;
        _client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = new Uri(_application.Urls.Single())
        };
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
        _authorizer.IsAdmin = false;
        using var response = await DeleteAsync(method, action, token);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
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

    private async Task<(string Action, string Token)> ReadDeleteForm()
    {
        var html = await _client.GetStringAsync(ListPath);
        var match = Regex.Match(html, @"<form\b[^>]*>.*?Delete</button>\s*</form>", RegexOptions.Singleline);
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

    private sealed class AdminAuthorizer : IAdminAuthorizer
    {
        public bool IsAdmin { get; set; }
        public Task<bool> IsAdminAsync(HttpContext httpContext) => Task.FromResult(IsAdmin);
    }

    private sealed class RecordingRepository : IDataRepository
    {
        public List<string> DeletedIds { get; } = [];
        public Task DeleteReport(string id)
        {
            DeletedIds.Add(id);
            return Task.CompletedTask;
        }

        public Task<IEnumerable<TestRunSummary>> GetAllTestRunRecords()
        {
            var record = ReportJson.DeserializeObject<TestRunSummary>("{}");
            record.RecordId = "run-618";
            return Task.FromResult<IEnumerable<TestRunSummary>>([record]);
        }

        public Task<BDTestOutputModel> GetData(string id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task StoreData(BDTestOutputModel model, string id) => throw new NotSupportedException();
    }
}
