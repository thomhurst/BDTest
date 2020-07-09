using System;
using System.Diagnostics;
using Newtonsoft.Json;

namespace BDTest.Output
{
    public class TestTimer
    {
        [JsonProperty] public DateTime TestsStartedAt { get; set; } = Process.GetCurrentProcess().StartTime;

        [JsonProperty] public DateTime TestsFinishedAt { get; set; } = DateTime.Now;

        public TimeSpan ElapsedTime => TestsFinishedAt - TestsStartedAt;
    }
}
