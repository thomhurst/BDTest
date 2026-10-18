namespace BDTest.Reports.Storage.FileSystem;

/// <summary>Options for a dedicated local report storage directory.</summary>
public sealed class FileSystemReportStoreOptions
{
    /// <summary>Absolute path outside the web root. Created on first use.</summary>
    public string DirectoryPath { get; set; } = "";

    /// <summary>Maximum age since each report or index entry was last written. Defaults to 30 days.</summary>
    public TimeSpan Retention { get; set; } = TimeSpan.FromDays(30);

    internal static bool IsValid(FileSystemReportStoreOptions options) =>
        !string.IsNullOrWhiteSpace(options.DirectoryPath) &&
        Path.IsPathFullyQualified(options.DirectoryPath) &&
        options.DirectoryPath.IndexOfAny(Path.GetInvalidPathChars()) < 0 &&
        options.Retention > TimeSpan.Zero && options.Retention <= TimeSpan.FromDays(36500);
}
