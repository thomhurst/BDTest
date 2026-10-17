using System.Diagnostics;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace BDTest.Output;

public class TestTimer
{
    [JsonProperty] public DateTime TestsStartedAt { get; set; } = InternalTestTimeData.TestsStartedAt ?? InternalTestTimeData.ProcessStartTime;

    [JsonProperty] public DateTime TestsFinishedAt { get; set; } = DateTime.UtcNow;

    // Deserialized reports must not inherit the receiving process's timestamps.
    // Supplied JSON values are applied after this callback.
    [OnDeserializing]
    private void ResetTimestamps(StreamingContext context)
    {
        TestsStartedAt = default;
        TestsFinishedAt = default;
    }

    // A report's calendar span is derived from its timestamps (including historical reports).
    // Execution durations on scenarios and steps are measured separately with Stopwatch.
    public TimeSpan ElapsedTime => TestsStartedAt == default || TestsFinishedAt == default
        ? TimeSpan.Zero
        : TestsFinishedAt - TestsStartedAt;
}

internal static class InternalTestTimeData
{
    [JsonIgnore] internal static DateTime? TestsStartedAt { get; set; }
    [JsonIgnore] internal static DateTime ProcessStartTime => Process.GetCurrentProcess().StartTime.ToUniversalTime();
}