namespace BDTest.ReportGenerator.RazorServer
{
    public class Config
    {
        public AzureStorageConfig AzureStorage { get; set; }
    }

    public class AzureStorageConfig
    {
        public string ConnectionString { get; set; }
    }
}