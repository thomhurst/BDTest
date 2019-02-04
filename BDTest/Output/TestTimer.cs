using System;
using System.Diagnostics;
using Newtonsoft.Json;

namespace BDTest.Output
{
    public class TestTimer
    {

        public TestTimer()
        {
            ElapsedTime = TestsFinishedAt - TestsStartedAt;
        }

        [JsonProperty] public DateTime TestsStartedAt { get; set; } = Process.GetCurrentProcess().StartTime;

        [JsonProperty] public DateTime TestsFinishedAt { get; set; } = DateTime.Now;

        [JsonProperty] public TimeSpan ElapsedTime { get; set; }
    }
}
