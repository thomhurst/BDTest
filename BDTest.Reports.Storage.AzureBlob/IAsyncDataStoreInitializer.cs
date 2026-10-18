namespace BDTest.Reports.Storage.AzureBlob;

public interface IAsyncDataStoreInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
