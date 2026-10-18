using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace BDTest.Reports.Storage.AzureBlob;

public static class AzureBlobReportStoreExtensions
{
    /// <summary>
    /// Registers persistent report, run-index and attachment storage. Call AddBDTestReports as well.
    /// Options are validated and containers initialized before the host starts accepting requests.
    /// </summary>
    public static OptionsBuilder<AzureStorageConfig> AddAzureBlobReportStore(this IServiceCollection services,
        Action<AzureStorageConfig>? configure = null)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<AzureStorageConfig>, AzureStorageConfigValidator>());
        var options = services.AddOptions<AzureStorageConfig>();
        if (configure != null) options.Configure(configure);
        options.ValidateOnStart();
        services.TryAddSingleton(provider => new AzureStorageDataStore(provider.GetRequiredService<IOptions<AzureStorageConfig>>().Value));
        services.AddSingleton<IReportStore>(provider => provider.GetRequiredService<AzureStorageDataStore>());
        services.AddSingleton<IRunIndexStore>(provider => provider.GetRequiredService<AzureStorageDataStore>());
        services.AddSingleton<IBDTestDataStore>(provider => provider.GetRequiredService<AzureStorageDataStore>());
        services.AddSingleton<IBDTestAttachmentStore>(provider => provider.GetRequiredService<AzureStorageDataStore>());
        services.AddHostedService<ReportStorageStartupService>();
        return options;
    }
}
