using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using BDTest.Razor.Reports.Interfaces;

namespace BDTest.Reports.Storage.AzureBlob;

public sealed class ReportStorageStartupService(
    IServiceProvider services,
    ILogger<ReportStorageStartupService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var store = services.GetRequiredService<IBDTestDataStore>();
            if (store is IAsyncDataStoreInitializer initializer)
            {
                await initializer.InitializeAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            await services.GetRequiredService<IDataRepository>().GetAllTestRunRecords(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogInformation("Report storage initialization and cache warm-up completed.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Report storage initialization or cache warm-up failed.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
