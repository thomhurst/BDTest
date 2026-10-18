using Azure.Storage.Blobs;

namespace BDTest.ReportGenerator.RazorServer;

public class AzureStorageConfig
{
    public string? ConnectionString { get; set; }

    internal bool HasValidConnectionString()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString)) return false;
        try
        {
            // Client construction parses the connection string without contacting storage.
            _ = new BlobServiceClient(ConnectionString);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            return false;
        }
    }
}
