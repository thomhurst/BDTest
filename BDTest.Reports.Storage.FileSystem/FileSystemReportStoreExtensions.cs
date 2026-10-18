using BDTest.Razor.Reports.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace BDTest.Reports.Storage.FileSystem;

public static class FileSystemReportStoreExtensions
{
    /// <summary>Registers one file-system store for reports and their run index, with startup option validation.</summary>
    public static OptionsBuilder<FileSystemReportStoreOptions> AddBDTestFileSystemStorage(
        this IServiceCollection services, Action<FileSystemReportStoreOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = services.AddOptions<FileSystemReportStoreOptions>();
        if (configure != null) options.Configure(configure);
        options.Validate(FileSystemReportStoreOptions.IsValid,
            "Specify an absolute DirectoryPath and a Retention greater than zero and no more than 36500 days.")
            .ValidateOnStart();
        services.TryAddSingleton<FileSystemReportStore>();
        services.AddSingleton<IReportStore>(provider => provider.GetRequiredService<FileSystemReportStore>());
        services.AddSingleton<IRunIndexStore>(provider => provider.GetRequiredService<FileSystemReportStore>());
        return options;
    }
}
