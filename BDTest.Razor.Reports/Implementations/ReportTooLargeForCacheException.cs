namespace BDTest.Razor.Reports.Implementations;

internal sealed class ReportTooLargeForCacheException : InvalidOperationException
{
    public ReportTooLargeForCacheException()
        : base("The report exceeds ReportCacheSizeLimitBytes. Increase the limit or configure a persistent DataStore.")
    {
    }
}
