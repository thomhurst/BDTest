using System;
using System.Collections.Concurrent;
using BDTest.Test;

namespace BDTest.Maps
{
    public static class TestHolder
    {
        internal static ConcurrentDictionary<Guid, BuildableTest> NotRun { get; } = new ConcurrentDictionary<Guid, BuildableTest>();
        internal static ConcurrentDictionary<Guid, Scenario> StoppedEarly { get; } = new ConcurrentDictionary<Guid, Scenario>();
        public static ConcurrentBag<Scenario> Scenarios { get; } = new ConcurrentBag<Scenario>();
    }
}
