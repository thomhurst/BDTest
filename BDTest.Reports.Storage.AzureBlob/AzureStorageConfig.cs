using Azure.Core;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;

namespace BDTest.Reports.Storage.AzureBlob;

/// <summary>Connection and container options for the Azure Blob report store.</summary>
public class AzureStorageConfig
{
    public string? ConnectionString { get; set; }
    public Uri? ServiceUri { get; set; }
    /// <summary>Set in code when using ServiceUri; credentials cannot be bound from configuration.</summary>
    public TokenCredential? Credential { get; set; }
    public string SummariesContainerName { get; set; } = "test-runs-summaries";
    public string ReportsContainerName { get; set; } = "test-data";

    internal BlobServiceClient CreateClient()
    {
        var validation = new AzureStorageConfigValidator().Validate(Options.DefaultName, this);
        if (validation.Failed)
            throw new OptionsValidationException(Options.DefaultName, typeof(AzureStorageConfig), validation.Failures);
        return string.IsNullOrWhiteSpace(ConnectionString)
            ? new BlobServiceClient(ServiceUri!, Credential!)
            : new BlobServiceClient(ConnectionString);
    }

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

internal sealed class AzureStorageConfigValidator : IValidateOptions<AzureStorageConfig>
{
    public ValidateOptionsResult Validate(string? name, AzureStorageConfig options)
    {
        var failures = new List<string>();
        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            if (!options.HasValidConnectionString())
                failures.Add("AzureStorage:ConnectionString must be a valid Azure Blob Storage connection string.");
            if (options.ServiceUri != null || options.Credential != null)
                failures.Add("Configure ConnectionString OR ServiceUri and Credential, not both.");
        }
        else if (options.Credential == null || options.ServiceUri is not { IsAbsoluteUri: true } uri
            || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment) || !string.IsNullOrEmpty(uri.UserInfo))
        {
            failures.Add("AzureStorage:ConnectionString is required unless ServiceUri (an absolute HTTPS URI without credentials, query or fragment) and Credential are configured.");
        }
        if (!ValidContainer(options.ReportsContainerName)) failures.Add("ReportsContainerName must be a valid Azure container name (3-63 lowercase letters, digits or single interior hyphens).");
        if (!ValidContainer(options.SummariesContainerName)) failures.Add("SummariesContainerName must be a valid Azure container name (3-63 lowercase letters, digits or single interior hyphens).");
        if (options.ReportsContainerName == options.SummariesContainerName)
            failures.Add("ReportsContainerName and SummariesContainerName must be different to prevent report/index overwrites.");
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static bool ValidContainer(string? name) => name is { Length: >= 3 and <= 63 }
        && name[0] != '-' && name[^1] != '-' && !name.Contains("--", StringComparison.Ordinal)
        && name.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');
}
