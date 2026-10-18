#nullable enable
namespace BDTest.Razor.Reports.Storage;

/// <summary>Newest start time first, then ordinal report ID. Filters use ordinal equality.</summary>
public sealed class RunIndexQuery
{
    public int PageSize { get; init; } = 50;
    public string? ContinuationToken { get; init; }
    public string? BranchName { get; init; }
    public string? Environment { get; init; }
    public string? Tag { get; init; }
}

public sealed class RunIndexPage
{
    public IReadOnlyList<RunIndexEntry> Entries { get; init; } = Array.Empty<RunIndexEntry>();
    public string? ContinuationToken { get; init; }
}
