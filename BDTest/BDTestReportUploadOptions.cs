namespace BDTest;

/// <summary>Configures report uploads. Values are validated and copied when an uploader is constructed.</summary>
public sealed class BDTestReportUploadOptions
{
    /// <summary>Creates options for a bare origin or the full external report prefix.</summary>
    public BDTestReportUploadOptions(Uri serverAddress)
    {
        if (serverAddress == null) throw new ArgumentNullException(nameof(serverAddress));
        if (!serverAddress.IsAbsoluteUri || (serverAddress.Scheme != Uri.UriSchemeHttp && serverAddress.Scheme != Uri.UriSchemeHttps)
            || serverAddress.Query.Length != 0 || serverAddress.Fragment.Length != 0 || serverAddress.UserInfo.Length != 0)
            throw new ArgumentException("Use an absolute HTTP(S) report address without credentials, query or fragment.", nameof(serverAddress));
        ServerAddress = serverAddress;
    }

    public Uri ServerAddress { get; }

    /// <summary>Optional per-request upload key. Null leaves caller-configured authentication in place.</summary>
    public string? UploadKey { get; set; }

    /// <summary>Maximum duration of each attempt, including response body reading. Defaults to two minutes.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Stream gzip-compressed JSON. Defaults to true; attachment bytes are never compressed.</summary>
    public bool UseGzip { get; set; } = true;

    /// <summary>Explicitly reads an upload key from the named environment variable; missing or blank values fail validation.</summary>
    public static BDTestReportUploadOptions FromEnvironment(Uri serverAddress, string variableName = "BDTEST_UPLOAD_KEY")
    {
        if (string.IsNullOrWhiteSpace(variableName)) throw new ArgumentException("An environment variable name is required.", nameof(variableName));
        var options = new BDTestReportUploadOptions(serverAddress)
        {
            UploadKey = Environment.GetEnvironmentVariable(variableName)
                ?? throw new InvalidOperationException($"Upload key environment variable '{variableName}' is not set.")
        };
        return options.ValidatedCopy();
    }

    internal BDTestReportUploadOptions ValidatedCopy()
    {
        if (UploadKey != null && (string.IsNullOrWhiteSpace(UploadKey) || UploadKey.Any(char.IsControl)))
            throw new ArgumentException("The upload key must be nonblank and contain no control characters.", nameof(UploadKey));
        if (Timeout != System.Threading.Timeout.InfiniteTimeSpan && (Timeout <= TimeSpan.Zero || Timeout.TotalMilliseconds > int.MaxValue))
            throw new ArgumentOutOfRangeException(nameof(Timeout), "Use a positive timeout up to Int32.MaxValue milliseconds, or Timeout.InfiniteTimeSpan.");
        return new BDTestReportUploadOptions(ServerAddress) { UploadKey = UploadKey, Timeout = Timeout, UseGzip = UseGzip };
    }
}
