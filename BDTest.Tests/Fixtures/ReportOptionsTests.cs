using System.Net;
using System.Text;
using BDTest.Maps;
using BDTest.Razor.Reports.Extensions;
using BDTest.Razor.Reports.Implementations;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;
using BDTest.Razor.Reports.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

public class ReportOptionsTests
{
    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task Memory_store_resolves_through_storage_interfaces(bool shared, bool legacy)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddBDTestReports();
        if (shared) builder.Services.AddSingleton<MemoryCacheBdTestDataStore>();
        if (legacy)
        {
            if (shared)
                builder.Services.AddSingleton<IBDTestDataStore>(provider => provider.GetRequiredService<MemoryCacheBdTestDataStore>());
            else
                builder.Services.AddSingleton<IBDTestDataStore, MemoryCacheBdTestDataStore>();
        }
        else if (shared)
        {
            builder.Services.AddSingleton<IReportStore>(provider => provider.GetRequiredService<MemoryCacheBdTestDataStore>());
            builder.Services.AddSingleton<IRunIndexStore>(provider => provider.GetRequiredService<MemoryCacheBdTestDataStore>());
        }
        else
        {
            builder.Services.AddSingleton<IReportStore, MemoryCacheBdTestDataStore>();
            builder.Services.AddSingleton<IRunIndexStore, MemoryCacheBdTestDataStore>();
        }
        await using var app = builder.Build();
        await app.StartAsync();
        var repository = app.Services.GetRequiredService<IDataRepository>();
        var report = new BDTestOutputModel { Id = "memory-store" };

        await repository.StoreData(report.Id, (report).ToReport());

        await Assert.That(Required(await repository.GetData(report.Id, default)).Id).IsEqualTo(report.Id);
        await Assert.That((await repository.ListAsync(new RunIndexQuery(), default)).Entries.Select(entry => entry.RecordId)).Contains(report.Id);
        await using var raw = await repository.OpenReadAsync(report.Id, default);
        using var reader = new StreamReader(Required(raw));
        await Assert.That(await reader.ReadToEndAsync()).Contains(report.Id);
    }

    [Test]
    [Arguments(nameof(BDTestReportOptions.ReportPageCacheSizeLimitBytes), "0")]
    [Arguments(nameof(BDTestReportOptions.ReportPageCacheSizeLimitBytes), "-1")]
    [Arguments(nameof(BDTestReportOptions.ReportCacheSizeLimitBytes), "0")]
    [Arguments(nameof(BDTestReportOptions.ReportCacheSizeLimitBytes), "-1")]
    [Arguments(nameof(BDTestReportOptions.ReportCacheRetention), "00:00:00")]
    [Arguments(nameof(BDTestReportOptions.ReportCacheRetention), "-00:00:01")]
    [Arguments(nameof(BDTestReportOptions.UploadsPerMinute), "0")]
    [Arguments(nameof(BDTestReportOptions.UploadsPerMinute), "-1")]
    [Arguments(nameof(BDTestReportOptions.MaxCompressedUploadBytes), "0")]
    [Arguments(nameof(BDTestReportOptions.MaxUploadBytes), "0")]
    [Arguments(nameof(BDTestReportOptions.MaximumAttachmentBytes), "-1")]
    [Arguments(nameof(BDTestReportOptions.MaximumReportAttachmentBytes), "-1")]
    [Arguments(nameof(BDTestReportOptions.MaximumAttachmentsPerReport), "-1")]
    public async Task Invalid_configuration_fails_host_startup_without_resolving_report_services(string property, string value)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { [$"BDTest:{property}"] = value });
        builder.Services.AddBDTestReports().BindConfiguration("BDTest");
        await using var host = builder.Build();

        var exception = await Assert.That(async () => await host.StartAsync()).ThrowsExactly<OptionsValidationException>();

        await Assert.That(Required(exception).Message).Contains(property.StartsWith("Maximum") ? "Attachment limits" : property);
    }

    [Test]
    [Arguments("/bdtest")]
    [Arguments("/team/reports")]
    public async Task Bound_options_and_constructor_injected_extensions_serve_reports(string prefix)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BDTest:UploadApiKey"] = "injected-key",
            ["BDTest:AllowAnonymousReports"] = "true",
            ["BDTest:ReportCacheSizeLimitBytes"] = "131072",
            ["BDTest:ReportCacheRetention"] = "00:15:00",
            ["BDTest:UploadsPerMinute"] = "7",
            ["BDTest:MaximumAttachmentsPerReport"] = "0",
            ["BDTest:AdditionalImageSources:0"] = "http://images.example:8080/"
        });
        builder.Services.AddBDTestReports().BindConfiguration("BDTest");
        builder.Services.AddSingleton(new Marker("constructor injected"));
        builder.Services.AddSingleton<InjectedExtensions>();
        builder.Services.AddSingleton<IBDTestCustomHeaderLinksProvider>(provider => provider.GetRequiredService<InjectedExtensions>());
        builder.Services.AddSingleton<IBDTestCustomSidebarLinksProvider>(provider => provider.GetRequiredService<InjectedExtensions>());
        builder.Services.AddSingleton<IUserPersonalizer>(provider => provider.GetRequiredService<InjectedExtensions>());
        builder.Services.AddSingleton<IBDTestDataReceiver>(provider => provider.GetRequiredService<InjectedExtensions>());
        builder.Services.AddSingleton<InjectedStore>();
        builder.Services.AddSingleton<IReportStore>(provider => provider.GetRequiredService<InjectedStore>());
        builder.Services.AddSingleton<IRunIndexStore>(provider => provider.GetRequiredService<InjectedStore>());
        await using var app = builder.Build();
        app.UseBDTestReportServer();
        app.MapBDTestReports(prefix);
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        using var request = new HttpRequestMessage(HttpMethod.Post, prefix + "/data")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-BDTest-Upload-Key", "injected-key");

        using var response = await client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(response.Headers.GetValues("Content-Security-Policy").Single()).Contains("img-src 'self' data: https: http://images.example:8080;");
        var id = response.Headers.GetValues("X-BDTest-Report-Id").Single();
        var html = await client.GetStringAsync($"{prefix}/report/{id}/summary");
        await Assert.That(html).Contains("constructor injected header");
        await Assert.That(html).Contains("constructor injected sidebar");
        await Assert.That(html).Contains("constructor injected user");
        await Assert.That(app.Services.GetRequiredService<InjectedExtensions>().Received).IsEqualTo(1);
        var store = app.Services.GetRequiredService<InjectedStore>();
        await Assert.That(store.Marker.Text).IsEqualTo("constructor injected");
        await using var persisted = await store.OpenReadAsync(id, default);
        await Assert.That(persisted).IsNotNull();
        var options = app.Services.GetRequiredService<IOptions<BDTestReportOptions>>().Value;
        await Assert.That(options.ReportCacheSizeLimitBytes).IsEqualTo(131072);
        await Assert.That(options.ReportCacheRetention).IsEqualTo(TimeSpan.FromMinutes(15));
        await Assert.That(options.UploadsPerMinute).IsEqualTo(7);
        await Assert.That(options.MaximumAttachmentsPerReport).IsZero();
        await Assert.That(options.AdditionalImageSources).IsEquivalentTo(new[] { "http://images.example:8080" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    private sealed record Marker(string Text);

    private sealed class InjectedStore(IMemoryCache cache, IOptions<BDTestReportOptions> options, Marker marker)
        : MemoryCacheBdTestDataStore(cache, options)
    {
        public Marker Marker { get; } = marker;
    }

    private sealed class InjectedExtensions(Marker marker) : IBDTestCustomHeaderLinksProvider,
        IBDTestCustomSidebarLinksProvider, IUserPersonalizer, IBDTestDataReceiver
    {
        public int Received { get; private set; }
        public IEnumerable<CustomLinkData> GetCustomLinks() => [new(marker.Text + " header", null)];
        public IEnumerable<CustomLinkData> GetCustomLinks(BDTest.Contracts.BDTestReport? report) => [new(marker.Text + " sidebar", null)];
        public Uri? GetProfilePictureUri(HttpContext httpContext) => null;
        public string GetNameOfUser(HttpContext httpContext) => marker.Text + " user";
        public List<CustomLinkData> GetLinksForUser(HttpContext httpContext) => [];
        public Task OnReceiveTestDataAsync(BDTest.Contracts.BDTestReport report) { Received++; return Task.CompletedTask; }
    }
}
