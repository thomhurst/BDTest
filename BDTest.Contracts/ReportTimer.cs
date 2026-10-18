using System.Text.Json.Serialization;

namespace BDTest.Contracts;

/// <summary>The calendar span of a report, separate from measured execution durations.</summary>
public sealed class ReportTimer
{
    public DateTimeOffset TestsStartedAt { get; }
    public DateTimeOffset TestsFinishedAt { get; }

    public TimeSpan ElapsedTime => TestsStartedAt == default || TestsFinishedAt == default
        ? TimeSpan.Zero : TestsFinishedAt - TestsStartedAt;

    [JsonConstructor]
    public ReportTimer(
        DateTimeOffset testsStartedAt,
        DateTimeOffset testsFinishedAt)
    {
        TestsStartedAt = testsStartedAt;
        TestsFinishedAt = testsFinishedAt;
    }
}
