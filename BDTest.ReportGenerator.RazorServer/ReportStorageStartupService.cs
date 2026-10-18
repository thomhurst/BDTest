using BDTest.Razor.Reports.Interfaces;
using BDTest.Razor.Reports.Models;

namespace BDTest.ReportGenerator.RazorServer;

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
            // Set the store before resolving the repository so cache warm-up uses the initialized store.
            services.GetRequiredService<BDTestReportServerOptions>().DataStore = store;
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
