using System;
using System.Diagnostics;

namespace BDTest.Tests.Extensions
{
    public static class StopwatchExtensions
    {
        public static TimeSpan StopAndGetElapsed(this Stopwatch stopwatch)
        {
            stopwatch.Stop();
            return stopwatch.Elapsed;
        }
    }
}