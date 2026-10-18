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
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class ReportOptionsTests
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
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

        await repository.StoreData(report.Id, report);

        Assert.That(Required(await repository.GetData(report.Id, default)).Id, Is.EqualTo(report.Id));
        Assert.That((await repository.ListAsync(new RunIndexQuery(), default)).Entries.Select(entry => entry.RecordId),
            Does.Contain(report.Id));
        await using var raw = await repository.OpenReadAsync(report.Id, default);
        using var reader = new StreamReader(Required(raw));
        Assert.That(await reader.ReadToEndAsync(), Does.Contain(report.Id));
    }

    [TestCase(nameof(BDTestReportOptions.ReportCacheSizeLimitBytes), "0")]
    [TestCase(nameof(BDTestReportOptions.ReportCacheSizeLimitBytes), "-1")]
    [TestCase(nameof(BDTestReportOptions.ReportCacheRetention), "00:00:00")]
    [TestCase(nameof(BDTestReportOptions.ReportCacheRetention), "-00:00:01")]
    [TestCase(nameof(BDTestReportOptions.UploadsPerMinute), "0")]
    [TestCase(nameof(BDTestReportOptions.UploadsPerMinute), "-1")]
    [TestCase(nameof(BDTestReportOptions.MaxCompressedUploadBytes), "0")]
    [TestCase(nameof(BDTestReportOptions.MaxUploadBytes), "0")]
    [TestCase(nameof(BDTestReportOptions.MaximumAttachmentBytes), "-1")]
    [TestCase(nameof(BDTestReportOptions.MaximumReportAttachmentBytes), "-1")]
    [TestCase(nameof(BDTestReportOptions.MaximumAttachmentsPerReport), "-1")]
    public async Task Invalid_configuration_fails_host_startup_without_resolving_report_services(string property, string value)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { [$"BDTest:{property}"] = value });
        builder.Services.AddBDTestReports().BindConfiguration("BDTest");
        await using var host = builder.Build();

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(async () => await host.StartAsync());

        Assert.That(Required(exception).Message, Does.Contain(property.StartsWith("Maximum") ? "Attachment limits" : property));
    }

    [TestCase("/bdtest")]
    [TestCase("/team/reports")]
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

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Headers.GetValues("Content-Security-Policy").Single(),
            Does.Contain("img-src 'self' data: https: http://images.example:8080;"));
        var id = response.Headers.GetValues("X-BDTest-Report-Id").Single();
        var html = await client.GetStringAsync($"{prefix}/report/{id}/summary");
        Assert.That(html, Does.Contain("constructor injected header"));
        Assert.That(html, Does.Contain("constructor injected sidebar"));
        Assert.That(html, Does.Contain("constructor injected user"));
        Assert.That(app.Services.GetRequiredService<InjectedExtensions>().Received, Is.EqualTo(1));
        var store = app.Services.GetRequiredService<InjectedStore>();
        Assert.That(store.Marker.Text, Is.EqualTo("constructor injected"));
        await using var persisted = await store.OpenReadAsync(id, default);
        Assert.That(persisted, Is.Not.Null);
        var options = app.Services.GetRequiredService<IOptions<BDTestReportOptions>>().Value;
        Assert.That(options.ReportCacheSizeLimitBytes, Is.EqualTo(131072));
        Assert.That(options.ReportCacheRetention, Is.EqualTo(TimeSpan.FromMinutes(15)));
        Assert.That(options.UploadsPerMinute, Is.EqualTo(7));
        Assert.That(options.MaximumAttachmentsPerReport, Is.Zero);
        Assert.That(options.AdditionalImageSources, Is.EqualTo(new[] { "http://images.example:8080" }));
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
        public IEnumerable<CustomLinkData> GetCustomLinks(BDTestOutputModel? report) => [new(marker.Text + " sidebar", null)];
        public Uri? GetProfilePictureUri(HttpContext httpContext) => null;
        public string GetNameOfUser(HttpContext httpContext) => marker.Text + " user";
        public List<CustomLinkData> GetLinksForUser(HttpContext httpContext) => [];
        public Task OnReceiveTestDataAsync(BDTestOutputModel report) { Received++; return Task.CompletedTask; }
    }
}
