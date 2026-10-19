using Azure.Core;
using BDTest.Razor.Reports.Extensions;
using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Storage;
using BDTest.Reports.Storage.AzureBlob;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace BDTest.ReportGenerator.Tests;

public class AzureBlobRegistrationTests
{
    [Test]
    public async Task Configuration_binding_registers_one_store_for_all_interfaces_without_network_calls()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Storage:ConnectionString"] = "UseDevelopmentStorage=true",
            ["Storage:ReportsContainerName"] = "custom-reports",
            ["Storage:SummariesContainerName"] = "custom-summaries"
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddBDTestReports();
        services.AddAzureBlobReportStore().BindConfiguration("Storage");
        using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<AzureStorageDataStore>();
        using (Assert.Multiple())
        {
            await Assert.That(provider.GetRequiredService<IReportStore>()).IsSameReferenceAs(store);
            await Assert.That(provider.GetRequiredService<IRunIndexStore>()).IsSameReferenceAs(store);
            await Assert.That(provider.GetRequiredService<IBDTestDataStore>()).IsSameReferenceAs(store);
            await Assert.That(provider.GetRequiredService<IBDTestAttachmentStore>()).IsSameReferenceAs(store);
            var options = provider.GetRequiredService<IOptions<AzureStorageConfig>>().Value;
            await Assert.That(options.ReportsContainerName).IsEqualTo("custom-reports");
            await Assert.That(options.SummariesContainerName).IsEqualTo("custom-summaries");
            await Assert.That(provider.GetServices<IHostedService>().OfType<ReportStorageStartupService>().Count()).IsEqualTo(1);
        }
    }

    [Test]
    public async Task Token_credential_registration_constructs_clients_without_requesting_a_token()
    {
        var services = new ServiceCollection();
        services.AddAzureBlobReportStore(options =>
        {
            options.ServiceUri = new Uri("https://example.blob.core.windows.net");
            options.Credential = new NoNetworkCredential();
        });
        using var provider = services.BuildServiceProvider();
        await Assert.That(provider.GetRequiredService<IReportStore>()).IsTypeOf<AzureStorageDataStore>();
    }

    [Test]
    [Arguments("ab")]
    [Arguments("UPPER")]
    [Arguments("bad--name")]
    [Arguments("-leading")]
    [Arguments("trailing-")]
    [Arguments("../escape")]
    [Arguments("")]
    [Arguments(null)]
    public async Task Invalid_container_names_are_rejected(string? name)
    {
        foreach (var summaries in new[] { false, true })
        {
            var services = new ServiceCollection();
            services.AddAzureBlobReportStore(options =>
            {
                options.ConnectionString = "UseDevelopmentStorage=true";
                if (summaries) options.SummariesContainerName = name!;
                else options.ReportsContainerName = name!;
            });
            using var provider = services.BuildServiceProvider();
            await Assert.That(() => provider.GetRequiredService<IReportStore>()).Throws<OptionsValidationException>();
        }
    }

    [Test]
    [Arguments("missing")]
    [Arguments("invalid-connection")]
    [Arguments("uri-only")]
    [Arguments("credential-only")]
    [Arguments("both")]
    [Arguments("http")]
    [Arguments("relative")]
    [Arguments("query")]
    [Arguments("same-container")]
    [Arguments("long-container")]
    public async Task Invalid_options_fail_host_startup_before_storage_access(string invalid)
    {
        // Exercise MVC registration with the same dependency validation used in CI.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddBDTestReports();
        builder.Services.AddAzureBlobReportStore(options =>
        {
            options.ConnectionString = "UseDevelopmentStorage=true";
            if (invalid == "invalid-connection") options.ConnectionString = "invalid";
            if (invalid == "same-container") options.SummariesContainerName = options.ReportsContainerName;
            if (invalid == "long-container") options.ReportsContainerName = new string('a', 64);
            if (invalid is "missing" or "uri-only" or "credential-only" or "http" or "relative" or "query") options.ConnectionString = null;
            if (invalid is "uri-only" or "both" or "http" or "relative" or "query")
                options.ServiceUri = invalid switch
                {
                    "http" => new Uri("http://example.blob.core.windows.net"),
                    "relative" => new Uri("relative", UriKind.Relative),
                    "query" => new Uri("https://example.blob.core.windows.net?sig=secret"),
                    _ => new Uri("https://example.blob.core.windows.net")
                };
            if (invalid is "credential-only" or "both" or "http" or "relative" or "query") options.Credential = new NoNetworkCredential();
        });
        using var host = builder.Build();
        await Assert.That(() => host.StartAsync()).Throws<OptionsValidationException>();
    }

    private sealed class NoNetworkCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Registration must not contact Azure.");
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Registration must not contact Azure.");
    }
}
