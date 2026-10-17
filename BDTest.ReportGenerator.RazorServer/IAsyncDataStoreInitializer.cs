namespace BDTest.ReportGenerator.RazorServer;

public interface IAsyncDataStoreInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
