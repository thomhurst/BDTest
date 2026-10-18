using BDTest.Reports.Storage.AzureBlob;
namespace BDTest.ReportGenerator.RazorServer;

public class Config
{
    public AzureStorageConfig? AzureStorage { get; set; }
}