using System;
using System.Diagnostics;
using Newtonsoft.Json;

namespace BDTest.Output;

public class TestTimer
{
    [JsonProperty] public DateTime TestsStartedAt { get; set; } = InternalTestTimeData.TestsStartedAt ?? InternalTestTimeData.ProcessStartTime;

    [JsonProperty] public DateTime TestsFinishedAt { get; set; } = DateTime.Now;

    public TimeSpan ElapsedTime => TestsFinishedAt - TestsStartedAt;
}

internal static class InternalTestTimeData
{
    [JsonIgnore] internal static DateTime? TestsStartedAt { get; set; }
    [JsonIgnore] internal static DateTime ProcessStartTime => Process.GetCurrentProcess().StartTime;
}